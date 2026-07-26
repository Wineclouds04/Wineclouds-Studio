using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinecloudsStudio.Shared.Logging;

namespace WinecloudsStudio.Modules.Reserved.ModuleF;

internal sealed class GitHubUpdateService
{
    private const string LatestManifestUrl =
        "https://github.com/Wineclouds04/Wineclouds-Studio/releases/latest/download/update.json";
    private const string InstallerPrefix = "WinecloudsStudio-Setup-";
    private const string InstallerSuffix = "-win-x64.exe";

    private static readonly HttpClient s_httpClient = CreateHttpClient();
    private static readonly JsonSerializerOptions s_jsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true
        };
    private readonly Uri _manifestUri;

    public GitHubUpdateService()
        : this(new Uri(LatestManifestUrl, UriKind.Absolute))
    {
    }

    internal GitHubUpdateService(Uri manifestUri)
    {
        _manifestUri = manifestUri
            ?? throw new ArgumentNullException(nameof(manifestUri));
    }

    public static Version CurrentVersion
    {
        get
        {
            Version version =
                Assembly.GetEntryAssembly()?.GetName().Version
                ?? new Version(0, 0, 0, 0);
            return NormalizeVersion(version);
        }
    }

    public async Task<UpdateReleaseInfo> GetLatestReleaseAsync(
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            _manifestUri);
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/json"));

        using HttpResponseMessage response = await s_httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                "最新 GitHub Release 缺少 update.json，"
                + "请使用项目的自动发布流程重新发布。");
        }

        response.EnsureSuccessStatusCode();
        await using Stream responseStream =
            await response.Content.ReadAsStreamAsync(cancellationToken);
        UpdateManifestDocument manifest =
            await JsonSerializer.DeserializeAsync<UpdateManifestDocument>(
                responseStream,
                s_jsonOptions,
                cancellationToken)
            ?? throw new InvalidDataException(
                "更新清单内容为空。");

        return ValidateManifest(manifest);
    }

    public async Task<string> DownloadInstallerAsync(
        UpdateReleaseInfo release,
        IProgress<UpdateDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        ValidateGitHubHttpsUri(
            release.Installer.DownloadUri,
            "安装包");
        string expectedHash = ParseSha256Digest(
            release.Installer.Digest);
        string safeFileName = Path.GetFileName(
            release.Installer.Name);
        if (!safeFileName.Equals(
                release.Installer.Name,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "更新清单中的安装包文件名不安全。");
        }

        string updateDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "WinecloudsStudio",
            "Updates",
            release.TagName);
        Directory.CreateDirectory(updateDirectory);

        string installerPath = Path.Combine(
            updateDirectory,
            safeFileName);
        string partialPath = installerPath + ".download";

        if (File.Exists(installerPath)
            && await HasExpectedHashAsync(
                installerPath,
                expectedHash,
                cancellationToken))
        {
            long existingLength =
                new FileInfo(installerPath).Length;
            progress?.Report(new UpdateDownloadProgress(
                existingLength,
                existingLength));
            return installerPath;
        }

        File.Delete(installerPath);
        File.Delete(partialPath);

        try
        {
            using HttpResponseMessage response =
                await s_httpClient.GetAsync(
                    release.Installer.DownloadUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
            response.EnsureSuccessStatusCode();

            long? totalBytes =
                response.Content.Headers.ContentLength;
            if (totalBytes is null or <= 0
                && release.Installer.Size > 0)
            {
                totalBytes = release.Installer.Size;
            }

            await using Stream source =
                await response.Content.ReadAsStreamAsync(
                    cancellationToken);
            await using var destination = new FileStream(
                partialPath,
                new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                    BufferSize = 128 * 1024,
                    Options =
                        FileOptions.Asynchronous
                        | FileOptions.SequentialScan
                });

            var buffer = new byte[128 * 1024];
            long receivedBytes = 0;
            while (true)
            {
                int read = await source.ReadAsync(
                    buffer,
                    cancellationToken);
                if (read == 0)
                {
                    break;
                }

                await destination.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken);
                receivedBytes += read;
                progress?.Report(new UpdateDownloadProgress(
                    receivedBytes,
                    totalBytes));
            }

            await destination.FlushAsync(cancellationToken);

            if (totalBytes is > 0
                && receivedBytes != totalBytes.Value)
            {
                throw new InvalidDataException(
                    "安装包下载不完整，请重新下载。");
            }

            if (!await HasExpectedHashAsync(
                    partialPath,
                    expectedHash,
                    cancellationToken))
            {
                throw new InvalidDataException(
                    "安装包 SHA-256 校验失败，文件已被丢弃。");
            }

            File.Move(partialPath, installerPath, true);
            Logger.Info(
                "Updater",
                $"Installer downloaded and verified: {installerPath}");
            return installerPath;
        }
        catch
        {
            File.Delete(partialPath);
            throw;
        }
    }

    private static UpdateReleaseInfo ValidateManifest(
        UpdateManifestDocument manifest)
    {
        if (manifest.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                $"不支持更新清单版本 {manifest.SchemaVersion}。");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version)
            || string.IsNullOrWhiteSpace(manifest.TagName)
            || manifest.Installer == null)
        {
            throw new InvalidDataException(
                "更新清单缺少版本或安装包信息。");
        }

        Version releaseVersion = ParseReleaseVersion(
            manifest.Version);
        string semanticVersion =
            releaseVersion.ToString(3);
        string expectedTag = $"v{semanticVersion}";
        if (!string.Equals(
                manifest.TagName,
                expectedTag,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "更新清单的版本号与 Release 标签不一致。");
        }

        string expectedInstallerName =
            $"{InstallerPrefix}{semanticVersion}{InstallerSuffix}";
        if (!manifest.Installer.Name.Equals(
                expectedInstallerName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "更新清单的版本号与安装包文件名不一致。");
        }

        if (!Uri.TryCreate(
                manifest.ReleasePageUrl,
                UriKind.Absolute,
                out Uri? releasePageUri)
            || !Uri.TryCreate(
                manifest.Installer.DownloadUrl,
                UriKind.Absolute,
                out Uri? installerUri))
        {
            throw new InvalidDataException(
                "更新清单包含无效的网址。");
        }
        ValidateGitHubHttpsUri(releasePageUri, "发布页");
        ValidateGitHubHttpsUri(installerUri, "安装包");

        string digest =
            $"sha256:{manifest.Installer.Sha256}";
        _ = ParseSha256Digest(digest);

        DateTimeOffset? publishedAt = null;
        if (DateTimeOffset.TryParse(
                manifest.PublishedAt,
                out DateTimeOffset parsedPublishedAt))
        {
            publishedAt = parsedPublishedAt;
        }

        return new UpdateReleaseInfo(
            releaseVersion,
            manifest.TagName,
            string.IsNullOrWhiteSpace(manifest.Name)
                ? manifest.TagName
                : manifest.Name,
            manifest.ReleaseNotes ?? string.Empty,
            releasePageUri,
            publishedAt,
            new UpdateAssetInfo(
                manifest.Installer.Name,
                installerUri,
                manifest.Installer.Size,
                digest));
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression =
                DecompressionMethods.GZip
                | DecompressionMethods.Deflate,
            AllowAutoRedirect = true,
            ConnectTimeout = TimeSpan.FromSeconds(15)
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(15)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "WinecloudsStudio-Updater/0.1");
        return client;
    }

    private static async Task<bool> HasExpectedHashAsync(
        string filePath,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            FileOptions.Asynchronous
                | FileOptions.SequentialScan);
        byte[] hash = await SHA256.HashDataAsync(
            stream,
            cancellationToken);
        string actualHash = Convert.ToHexString(hash);
        return actualHash.Equals(
            expectedHash,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ParseSha256Digest(string digest)
    {
        const string prefix = "sha256:";
        if (!digest.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "更新清单使用了不支持的摘要格式。");
        }

        string hash = digest[prefix.Length..].Trim();
        if (hash.Length != 64
            || hash.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException(
                "更新清单中的 SHA-256 摘要无效。");
        }

        return hash;
    }

    private static Version ParseReleaseVersion(
        string versionText)
    {
        string normalized = versionText.Trim();
        if (normalized.StartsWith(
                "v",
                StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        int suffixIndex = normalized.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0)
        {
            normalized = normalized[..suffixIndex];
        }

        if (!Version.TryParse(
                normalized,
                out Version? version))
        {
            throw new InvalidDataException(
                $"无法识别更新版本号“{versionText}”。");
        }

        return NormalizeVersion(version);
    }

    private static Version NormalizeVersion(Version version) => new(
        Math.Max(0, version.Major),
        Math.Max(0, version.Minor),
        Math.Max(0, version.Build),
        Math.Max(0, version.Revision));

    private static void ValidateGitHubHttpsUri(
        Uri uri,
        string purpose)
    {
        if (!uri.Scheme.Equals(
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase)
            || !uri.Host.Equals(
                "github.com",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"{purpose}地址不是受支持的 GitHub HTTPS 地址。");
        }
    }

    private sealed class UpdateManifestDocument
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; init; }

        [JsonPropertyName("version")]
        public string Version { get; init; } = string.Empty;

        [JsonPropertyName("tagName")]
        public string TagName { get; init; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("releaseNotes")]
        public string? ReleaseNotes { get; init; }

        [JsonPropertyName("releasePageUrl")]
        public string ReleasePageUrl { get; init; } = string.Empty;

        [JsonPropertyName("publishedAt")]
        public string? PublishedAt { get; init; }

        [JsonPropertyName("installer")]
        public UpdateInstallerManifest? Installer { get; init; }
    }

    private sealed class UpdateInstallerManifest
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("downloadUrl")]
        public string DownloadUrl { get; init; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; init; }

        [JsonPropertyName("sha256")]
        public string Sha256 { get; init; } = string.Empty;
    }
}
