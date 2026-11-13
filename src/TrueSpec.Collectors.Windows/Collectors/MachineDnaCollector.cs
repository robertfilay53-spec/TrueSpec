namespace TrueSpec.Collectors.Windows.Collectors;

using System.Globalization;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Comparison;

/// <summary>
/// The machine fingerprint (Blueprint 4, U7): board, BIOS, disk, memory
/// and processor serials from CIM, read-only, as a standard user. The
/// full serials live only in the local variables of one call: they are
/// handed to <see cref="MachineDna.Build"/>, which hashes them and keeps
/// only the truncated form, and they are never logged, never put in a
/// component spec and never written to disk. Disk serials come from
/// Win32_DiskDrive here (per physical disk), the same class the plan
/// names; the Storage collector's MSFT_PhysicalDisk serials are a
/// separate reading and are not reused, so this collector stays
/// independent of the admin-gated storage stack.
/// </summary>
public sealed class MachineDnaCollector : IMachineDnaProvider
{
    public const string BaseBoardQuery = "SELECT SerialNumber FROM Win32_BaseBoard";

    public const string BiosQuery = "SELECT SerialNumber FROM Win32_BIOS";

    public const string DiskQuery = "SELECT DeviceID, SerialNumber FROM Win32_DiskDrive";

    public const string MemoryQuery = "SELECT DeviceLocator, SerialNumber FROM Win32_PhysicalMemory";

    public const string ProcessorQuery = "SELECT ProcessorId FROM Win32_Processor";

    public Task<MachineDna?> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // WMI reads are synchronous; the scan service already runs this
        // call on the thread pool under its timeout race, so no Task.Run
        // here. A query that fails returns no rows (Wmi.Query records the
        // error instead of throwing): that class simply contributes no
        // parts, and the DNA reports honestly on what did arrive.
        var boards = Wmi.Query("root\\cimv2", BaseBoardQuery);
        cancellationToken.ThrowIfCancellationRequested();
        var bios = Wmi.Query("root\\cimv2", BiosQuery);
        cancellationToken.ThrowIfCancellationRequested();
        var disks = Wmi.Query("root\\cimv2", DiskQuery);
        cancellationToken.ThrowIfCancellationRequested();
        var memory = Wmi.Query("root\\cimv2", MemoryQuery);
        cancellationToken.ThrowIfCancellationRequested();
        var processors = Wmi.Query("root\\cimv2", ProcessorQuery);

        var dna = Build(boards, bios, disks, memory, processors);
        return Task.FromResult<MachineDna?>(dna);
    }

    /// <summary>
    /// Pure mapping from WMI rows to a DNA: every row becomes one part,
    /// including rows whose serial is blank or a placeholder (those become
    /// unidentified parts, never dropped, so the snapshot lists every part
    /// the hardware was asked about). Testable without WMI by building
    /// fake <see cref="WmiRow"/> values.
    /// </summary>
    internal static MachineDna Build(
        IReadOnlyList<WmiRow> boards,
        IReadOnlyList<WmiRow> bios,
        IReadOnlyList<WmiRow> disks,
        IReadOnlyList<WmiRow> memory,
        IReadOnlyList<WmiRow> processors)
    {
        var parts = new List<(MachineDnaPartKind Kind, string Label, string? Serial)>();

        foreach (var row in boards)
        {
            parts.Add((MachineDnaPartKind.Board, "Base board", row.String("SerialNumber")));
        }

        foreach (var row in bios)
        {
            parts.Add((MachineDnaPartKind.Bios, "BIOS", row.String("SerialNumber")));
        }

        for (var i = 0; i < disks.Count; i++)
        {
            parts.Add((MachineDnaPartKind.Disk, DiskLabel(disks[i], i), disks[i].String("SerialNumber")));
        }

        for (var i = 0; i < memory.Count; i++)
        {
            parts.Add((MachineDnaPartKind.Memory, MemoryLabel(memory[i], i), memory[i].String("SerialNumber")));
        }

        for (var i = 0; i < processors.Count; i++)
        {
            var label = processors.Count > 1
                ? string.Create(CultureInfo.InvariantCulture, $"Processor {i}")
                : "Processor";
            parts.Add((MachineDnaPartKind.Cpu, label, processors[i].String("ProcessorId")));
        }

        return MachineDna.Build(parts);
    }

    /// <summary>
    /// The disk label is the physical-drive number from the DeviceID
    /// ("\\.\PHYSICALDRIVE0" becomes "Disk 0"), stable across scans; the
    /// row index is the fallback when the DeviceID is missing or has no
    /// trailing number.
    /// </summary>
    internal static string DiskLabel(WmiRow row, int index)
    {
        var deviceId = row.String("DeviceID");
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            var digits = new string(deviceId.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
            if (digits.Length > 0 && int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                return string.Create(CultureInfo.InvariantCulture, $"Disk {number}");
            }
        }

        return string.Create(CultureInfo.InvariantCulture, $"Disk {index}");
    }

    /// <summary>
    /// The memory label is the slot the firmware reports ("DIMM 0"),
    /// stable across scans; the row index is the fallback.
    /// </summary>
    internal static string MemoryLabel(WmiRow row, int index)
    {
        var locator = row.String("DeviceLocator");
        if (!string.IsNullOrWhiteSpace(locator))
        {
            return locator.Trim();
        }

        return string.Create(CultureInfo.InvariantCulture, $"Memory {index}");
    }
}
