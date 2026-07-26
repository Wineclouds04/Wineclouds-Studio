using System.Globalization;
using System.Management;
using System.Windows.Forms;
using WinecloudsStudio.Modules.SystemInformation.Models;
using WinecloudsStudio.Shared.Logging;

namespace WinecloudsStudio.Modules.SystemInformation.Services;

internal sealed class HardwareInformationService
{
    private const string Unknown = "未检测到";

    public Task<HardwareInformationSnapshot> LoadAsync(
        CancellationToken cancellationToken) =>
        Task.Run(
            () => Load(cancellationToken),
            cancellationToken);

    private static HardwareInformationSnapshot Load(
        CancellationToken cancellationToken)
    {
        var warnings = new List<string>();

        (string model, ulong systemMemory) = ReadSafely(
            "型号信息",
            ReadComputerSystem,
            (Unknown, 0UL),
            warnings);
        cancellationToken.ThrowIfCancellationRequested();

        string operatingSystem = ReadSafely(
            "系统信息",
            ReadOperatingSystem,
            Unknown,
            warnings);
        cancellationToken.ThrowIfCancellationRequested();

        string processor = ReadSafely(
            "处理器",
            ReadProcessor,
            Unknown,
            warnings);
        cancellationToken.ThrowIfCancellationRequested();

        string motherboard = ReadSafely(
            "主板",
            ReadMotherboard,
            Unknown,
            warnings);
        cancellationToken.ThrowIfCancellationRequested();

        (ulong moduleMemory, IReadOnlyList<string> modules) =
            ReadSafely(
                "内存",
                ReadMemory,
                (0UL, Array.Empty<string>()),
                warnings);
        cancellationToken.ThrowIfCancellationRequested();

        string memory = FormatMemory(
            moduleMemory > 0
                ? moduleMemory
                : systemMemory,
            modules);

        IReadOnlyList<string> graphics = ReadSafely(
            "显卡",
            ReadGraphicsAdapters,
            Array.Empty<string>(),
            warnings);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<string> displays = ReadSafely(
            "显示器",
            ReadDisplays,
            Array.Empty<string>(),
            warnings);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<string> storage = ReadSafely(
            "磁盘",
            ReadStorageDevices,
            Array.Empty<string>(),
            warnings);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<string> audio = ReadSafely(
            "声卡",
            ReadAudioDevices,
            Array.Empty<string>(),
            warnings);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<string> network = ReadSafely(
            "网卡",
            ReadNetworkAdapters,
            Array.Empty<string>(),
            warnings);

        return new HardwareInformationSnapshot(
            model,
            operatingSystem,
            processor,
            motherboard,
            memory,
            WithFallback(graphics),
            WithFallback(displays),
            WithFallback(storage),
            WithFallback(audio),
            WithFallback(network),
            DateTimeOffset.Now,
            warnings);
    }

    private static T ReadSafely<T>(
        string section,
        Func<T> reader,
        T fallback,
        ICollection<string> warnings)
    {
        try
        {
            return reader();
        }
        catch (Exception exception)
        {
            warnings.Add($"{section}读取失败");
            Logger.Warn(
                "HardwareInformation",
                $"{section} query failed: {exception.Message}");
            return fallback;
        }
    }

    private static (string Model, ulong TotalMemory)
        ReadComputerSystem()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT Manufacturer, Model, TotalPhysicalMemory "
            + "FROM Win32_ComputerSystem");
        using ManagementObjectCollection results =
            searcher.Get();

        foreach (ManagementObject item in results)
        {
            using (item)
            {
                string manufacturer =
                    GetText(item, "Manufacturer");
                string model = GetText(item, "Model");
                string summary = JoinDistinct(
                    manufacturer,
                    model);
                return (
                    string.IsNullOrWhiteSpace(summary)
                        ? Unknown
                        : summary,
                    GetUnsignedInteger(
                        item,
                        "TotalPhysicalMemory"));
            }
        }

        return (Unknown, 0);
    }

    private static string ReadOperatingSystem()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT Caption, Version, BuildNumber, "
            + "OSArchitecture FROM Win32_OperatingSystem");
        using ManagementObjectCollection results =
            searcher.Get();

        foreach (ManagementObject item in results)
        {
            using (item)
            {
                string caption =
                    GetText(item, "Caption")
                        .Replace(
                            "Microsoft ",
                            string.Empty,
                            StringComparison.OrdinalIgnoreCase);
                string architecture =
                    GetText(item, "OSArchitecture");
                string build = GetText(
                    item,
                    "BuildNumber");
                string version = GetText(item, "Version");

                var parts = new List<string>();
                AddIfPresent(parts, caption);
                AddIfPresent(parts, architecture);
                AddIfPresent(
                    parts,
                    string.IsNullOrWhiteSpace(build)
                        ? version
                        : $"Build {build}");
                return parts.Count == 0
                    ? Unknown
                    : string.Join(" · ", parts);
            }
        }

        return Unknown;
    }

    private static string ReadProcessor()
    {
        IReadOnlyList<string> processors = QueryStrings(
            "SELECT Name, NumberOfCores, "
            + "NumberOfLogicalProcessors "
            + "FROM Win32_Processor",
            item =>
            {
                string name = GetText(item, "Name");
                ulong cores = GetUnsignedInteger(
                    item,
                    "NumberOfCores");
                ulong threads = GetUnsignedInteger(
                    item,
                    "NumberOfLogicalProcessors");

                if (string.IsNullOrWhiteSpace(name))
                {
                    return null;
                }

                return cores > 0 && threads > 0
                    ? $"{name} · {cores} 核 / {threads} 线程"
                    : name;
            });

        return processors.Count == 0
            ? Unknown
            : string.Join(
                Environment.NewLine,
                processors);
    }

    private static string ReadMotherboard()
    {
        IReadOnlyList<string> boards = QueryStrings(
            "SELECT Manufacturer, Product, Version "
            + "FROM Win32_BaseBoard",
            item =>
            {
                string summary = JoinDistinct(
                    GetText(item, "Manufacturer"),
                    GetText(item, "Product"),
                    GetText(item, "Version"));
                return string.IsNullOrWhiteSpace(summary)
                    ? null
                    : summary;
            });

        return boards.Count == 0
            ? Unknown
            : string.Join(
                Environment.NewLine,
                boards);
    }

    private static (
        ulong Total,
        IReadOnlyList<string> Modules)
        ReadMemory()
    {
        ulong total = 0;
        IReadOnlyList<string> modules = QueryStrings(
            "SELECT Manufacturer, PartNumber, Capacity, Speed "
            + "FROM Win32_PhysicalMemory",
            item =>
            {
                ulong capacity = GetUnsignedInteger(
                    item,
                    "Capacity");
                total += capacity;

                var parts = new List<string>();
                if (capacity > 0)
                {
                    parts.Add(FormatBytes(capacity));
                }

                ulong speed = GetUnsignedInteger(
                    item,
                    "Speed");
                if (speed > 0)
                {
                    parts.Add($"{speed} MT/s");
                }

                AddHardwareValue(
                    parts,
                    GetText(item, "Manufacturer"));
                AddHardwareValue(
                    parts,
                    GetText(item, "PartNumber"));
                return parts.Count == 0
                    ? null
                    : string.Join(" · ", parts);
            });

        return (total, modules);
    }

    private static IReadOnlyList<string>
        ReadGraphicsAdapters() =>
        QueryStrings(
            "SELECT Name, DriverVersion "
            + "FROM Win32_VideoController",
            item =>
            {
                string name = GetText(item, "Name");
                string driver = GetText(
                    item,
                    "DriverVersion");
                if (string.IsNullOrWhiteSpace(name))
                {
                    return null;
                }

                return string.IsNullOrWhiteSpace(driver)
                    ? name
                    : $"{name} · 驱动 {driver}";
            });

    private static IReadOnlyList<string> ReadDisplays()
    {
        var displays = new List<string>();
        foreach (Screen screen in Screen.AllScreens)
        {
            string deviceName = screen.DeviceName
                .Replace(
                    @"\\.\",
                    string.Empty,
                    StringComparison.Ordinal);
            string primary = screen.Primary
                ? " · 主显示器"
                : string.Empty;
            displays.Add(
                $"{deviceName} · "
                + $"{screen.Bounds.Width} × "
                + $"{screen.Bounds.Height}{primary}");
        }

        return Distinct(displays);
    }

    private static IReadOnlyList<string>
        ReadStorageDevices() =>
        QueryStrings(
            "SELECT Model, Size, InterfaceType "
            + "FROM Win32_DiskDrive",
            item =>
            {
                string model = GetText(item, "Model");
                ulong size = GetUnsignedInteger(
                    item,
                    "Size");
                string interfaceType = GetText(
                    item,
                    "InterfaceType");

                var parts = new List<string>();
                AddIfPresent(parts, model);
                if (size > 0)
                {
                    parts.Add(FormatBytes(size));
                }

                AddIfPresent(parts, interfaceType);
                return parts.Count == 0
                    ? null
                    : string.Join(" · ", parts);
            });

    private static IReadOnlyList<string>
        ReadAudioDevices() =>
        QueryStrings(
            "SELECT Name FROM Win32_SoundDevice "
            + "WHERE Status = 'OK'",
            item =>
            {
                string name = GetText(item, "Name");
                return string.IsNullOrWhiteSpace(name)
                    ? null
                    : name;
            });

    private static IReadOnlyList<string>
        ReadNetworkAdapters() =>
        QueryStrings(
            "SELECT Name, NetConnectionID, Speed, "
            + "PNPDeviceID "
            + "FROM Win32_NetworkAdapter "
            + "WHERE PhysicalAdapter = TRUE "
            + "AND NetEnabled = TRUE",
            item =>
            {
                string name = GetText(item, "Name");
                string connectionName = GetText(
                    item,
                    "NetConnectionID");
                string pnpDeviceId = GetText(
                    item,
                    "PNPDeviceID");
                ulong speed = GetUnsignedInteger(
                    item,
                    "Speed");

                if (pnpDeviceId.StartsWith(
                        @"ROOT\",
                        StringComparison.OrdinalIgnoreCase)
                    || name.Contains(
                        "Tunnel",
                        StringComparison.OrdinalIgnoreCase)
                    || connectionName.Contains(
                        "Tunnel",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var parts = new List<string>();
                AddHardwareValue(parts, name);
                if (!connectionName.Equals(
                        name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    AddHardwareValue(
                        parts,
                        connectionName);
                }

                if (speed > 0)
                {
                    parts.Add(FormatNetworkSpeed(speed));
                }

                return parts.Count == 0
                    ? null
                    : string.Join(" · ", parts);
            });

    private static IReadOnlyList<string> QueryStrings(
        string query,
        Func<ManagementBaseObject, string?> selector)
    {
        var values = new List<string>();
        using var searcher =
            new ManagementObjectSearcher(query);
        using ManagementObjectCollection results =
            searcher.Get();

        foreach (ManagementObject item in results)
        {
            using (item)
            {
                string? value = selector(item);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(CollapseWhitespace(value));
                }
            }
        }

        return Distinct(values);
    }

    private static IReadOnlyList<string> Distinct(
        IEnumerable<string> values) =>
        values
            .Where(value =>
                !string.IsNullOrWhiteSpace(value))
            .Distinct(
                StringComparer.CurrentCultureIgnoreCase)
            .Take(12)
            .ToArray();

    private static IReadOnlyList<string> WithFallback(
        IReadOnlyList<string> values) =>
        values.Count == 0
            ? [Unknown]
            : values;

    private static string FormatMemory(
        ulong totalBytes,
        IReadOnlyList<string> modules)
    {
        if (totalBytes == 0 && modules.Count == 0)
        {
            return Unknown;
        }

        var lines = new List<string>();
        if (totalBytes > 0)
        {
            lines.Add($"{FormatBytes(totalBytes)} 总计");
        }

        lines.AddRange(modules);
        return string.Join(
            Environment.NewLine,
            lines);
    }

    private static string GetText(
        ManagementBaseObject item,
        string propertyName)
    {
        object? value =
            item.Properties[propertyName]?.Value;
        return CollapseWhitespace(
            Convert.ToString(
                value,
                CultureInfo.InvariantCulture)
            ?? string.Empty);
    }

    private static ulong GetUnsignedInteger(
        ManagementBaseObject item,
        string propertyName)
    {
        object? value =
            item.Properties[propertyName]?.Value;
        if (value == null)
        {
            return 0;
        }

        try
        {
            return Convert.ToUInt64(
                value,
                CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static void AddIfPresent(
        ICollection<string> values,
        string value)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && !values.Contains(
                value,
                StringComparer.CurrentCultureIgnoreCase))
        {
            values.Add(value);
        }
    }

    private static void AddHardwareValue(
        ICollection<string> values,
        string value)
    {
        if (IsUsefulHardwareValue(value))
        {
            AddIfPresent(values, value);
        }
    }

    private static bool IsUsefulHardwareValue(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.StartsWith(
                "0x",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "default string" => false,
            "to be filled by o.e.m." => false,
            "system product name" => false,
            "system manufacturer" => false,
            "unknown" => false,
            "none" => false,
            _ => true
        };
    }

    private static string JoinDistinct(
        params string[] values) =>
        string.Join(
            " ",
            values
                .Where(IsUsefulHardwareValue)
                .Distinct(
                    StringComparer.CurrentCultureIgnoreCase));

    private static string CollapseWhitespace(
        string value) =>
        string.Join(
            " ",
            value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));

    private static string FormatBytes(ulong bytes)
    {
        const double gibibyte =
            1024d * 1024d * 1024d;
        double gibibytes = bytes / gibibyte;
        return gibibytes >= 1024
            ? $"{gibibytes / 1024:0.##} TB"
            : $"{gibibytes:0.##} GB";
    }

    private static string FormatNetworkSpeed(
        ulong bitsPerSecond)
    {
        const double gigabit = 1_000_000_000d;
        const double megabit = 1_000_000d;
        return bitsPerSecond >= gigabit
            ? $"{bitsPerSecond / gigabit:0.##} Gbps"
            : $"{bitsPerSecond / megabit:0.##} Mbps";
    }
}
