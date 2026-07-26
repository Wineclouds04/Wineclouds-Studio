namespace WinecloudsStudio.Modules.SystemInformation.Models;

internal sealed record HardwareInformationSnapshot(
    string ModelSummary,
    string OperatingSystemSummary,
    string Processor,
    string Motherboard,
    string Memory,
    IReadOnlyList<string> GraphicsAdapters,
    IReadOnlyList<string> Displays,
    IReadOnlyList<string> StorageDevices,
    IReadOnlyList<string> AudioDevices,
    IReadOnlyList<string> NetworkAdapters,
    DateTimeOffset RefreshedAt,
    IReadOnlyList<string> Warnings);
