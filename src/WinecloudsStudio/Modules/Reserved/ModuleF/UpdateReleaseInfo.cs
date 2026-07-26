namespace WinecloudsStudio.Modules.Reserved.ModuleF;

internal sealed record UpdateAssetInfo(
    string Name,
    Uri DownloadUri,
    long Size,
    string Digest);

internal sealed record UpdateReleaseInfo(
    Version Version,
    string TagName,
    string Name,
    string ReleaseNotes,
    Uri ReleasePageUri,
    DateTimeOffset? PublishedAt,
    UpdateAssetInfo Installer);

internal readonly record struct UpdateDownloadProgress(
    long BytesReceived,
    long? TotalBytes)
{
    public double? Percentage =>
        TotalBytes is > 0
            ? Math.Clamp(BytesReceived * 100d / TotalBytes.Value, 0d, 100d)
            : null;
}
