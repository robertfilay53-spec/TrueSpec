using TrueSpec.Core.Comparison;
using TrueSpec.Core.Models;

namespace TrueSpec.Tests;

/// <summary>
/// Machine DNA (v2.0 U7, Blueprint 4): canonical ordering, truncation,
/// placeholder handling, comparison verdicts and the empty-parts case.
/// All pure Core: runs on every leg.
/// </summary>
public class MachineDnaTests
{
    private static (MachineDnaPartKind Kind, string Label, string? Serial) Board(string? serial) =>
        (MachineDnaPartKind.Board, "Board", serial);

    private static (MachineDnaPartKind Kind, string Label, string? Serial) Bios(string? serial) =>
        (MachineDnaPartKind.Bios, "BIOS", serial);

    private static (MachineDnaPartKind Kind, string Label, string? Serial) Disk(string label, string serial) =>
        (MachineDnaPartKind.Disk, label, serial);

    private static (MachineDnaPartKind Kind, string Label, string? Serial) Memory(string label, string serial) =>
        (MachineDnaPartKind.Memory, label, serial);

    private static (MachineDnaPartKind Kind, string Label, string? Serial) Cpu(string serial) =>
        (MachineDnaPartKind.Cpu, "CPU", serial);

    private static MachineDna FullDna() => MachineDna.Build(new[]
    {
        Board("BOARD123456789"),
        Bios("BIOS987654321"),
        Disk("Disk 0", "DISK-AAAA-1111"),
        Disk("Disk 1", "DISK-BBBB-2222"),
        Memory("Slot 0", "MEM-CCCC-3333"),
        Cpu("CPUID-0123456789ABCDEF"),
    });

    [Fact]
    public void Build_SamePartsInDifferentOrder_GiveTheSameHash()
    {
        var forward = MachineDna.Build(new[]
        {
            Board("BOARD123456789"),
            Bios("BIOS987654321"),
            Disk("Disk 0", "DISK-AAAA-1111"),
        });
        var reversed = MachineDna.Build(new[]
        {
            Disk("Disk 0", "DISK-AAAA-1111"),
            Bios("BIOS987654321"),
            Board("BOARD123456789"),
        });

        Assert.NotNull(forward.Hash);
        Assert.Equal(forward.Hash, reversed.Hash);
        Assert.Equal(
            forward.Parts.Select(p => (p.Kind, p.Label)).ToList(),
            reversed.Parts.Select(p => (p.Kind, p.Label)).ToList());
    }

    [Fact]
    public void Build_PartsAreStoredSortedByKindThenLabel()
    {
        var dna = FullDna();

        var keys = dna.Parts.Select(p => (p.Kind, p.Label)).ToList();
        var sorted = keys
            .OrderBy(k => k.Kind)
            .ThenBy(k => k.Label, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(sorted, keys);
    }

    [Fact]
    public void Build_HashIsA64CharLowercaseHexSha256()
    {
        var dna = FullDna();

        Assert.NotNull(dna.Hash);
        Assert.Equal(64, dna.Hash!.Length);
        Assert.True(dna.Hash.All(c => char.IsDigit(c) || (c >= 'a' && c <= 'f')));
    }

    [Fact]
    public void Build_DifferentSerial_GivesADifferentHash()
    {
        var one = MachineDna.Build(new[] { Board("BOARD123456789") });
        var other = MachineDna.Build(new[] { Board("BOARD123456780") });

        Assert.NotEqual(one.Hash, other.Hash);
    }

    [Fact]
    public void TruncateSerial_LongSerial_KeepsFirstAndLastFour()
    {
        Assert.Equal("ABCD...IJKL", MachineDna.TruncateSerial("ABCDEFGHIJKL"));
        Assert.Equal("ABCD...7890", MachineDna.TruncateSerial("ABCD1234567890"));
    }

    [Fact]
    public void TruncateSerial_ShortSerial_IsShownWhole()
    {
        // Nothing to hide by truncating an 8-char serial, and the overlap
        // would mislead: "ABCDEFGH" truncated would read "ABCD...EFGH".
        Assert.Equal("ABCDEFGH", MachineDna.TruncateSerial("ABCDEFGH"));
        Assert.Equal("ABC123", MachineDna.TruncateSerial("ABC123"));
    }

    [Fact]
    public void Build_StoredPartsCarryOnlyTheTruncatedSerial()
    {
        var dna = MachineDna.Build(new[] { Board("BOARD123456789") });

        var part = Assert.Single(dna.Parts);
        Assert.Equal("BOAR...6789", part.SerialTruncated);
        Assert.DoesNotContain("BOARD123456789", part.SerialTruncated);
        Assert.True(part.IsIdentified);
    }

    [Fact]
    public void FromSerial_PlaceholderSerial_IsNotReported()
    {
        foreach (var placeholder in new[]
                 {
                     "To be filled by O.E.M.",
                     "TO BE FILLED BY O.E.M.",
                     "To be filled by O.E.M",
                     "Default string",
                     "None",
                     "Unknown",
                     "N/A",
                     "00000000",
                 })
        {
            var part = MachineDnaPart.FromSerial(MachineDnaPartKind.Bios, "BIOS", placeholder);

            Assert.False(part.IsIdentified);
            Assert.Equal(SpecLabels.NotReportedByHardware, part.SerialTruncated);
        }
    }

    [Fact]
    public void FromSerial_MissingOrBlankSerial_IsNotReported()
    {
        foreach (var serial in new string?[] { null, "", "   " })
        {
            var part = MachineDnaPart.FromSerial(MachineDnaPartKind.Bios, "BIOS", serial);

            Assert.False(part.IsIdentified);
            Assert.Equal(SpecLabels.NotReportedByHardware, part.SerialTruncated);
        }
    }

    [Fact]
    public void Build_AllPartsUnidentified_HasNoHash()
    {
        // Two machines that both report nothing must not share a DNA:
        // no identified part means no hash, "DNA not available".
        var dna = MachineDna.Build(new[]
        {
            Bios("To be filled by O.E.M."),
            Board(null),
            Cpu(""),
        });

        Assert.Null(dna.Hash);
        Assert.False(dna.IsAvailable);
        Assert.Equal("DNA not available", dna.HashDisplay);
        Assert.Equal(3, dna.Parts.Count);
        Assert.All(dna.Parts, p => Assert.False(p.IsIdentified));
    }

    [Fact]
    public void Build_EmptyParts_HasNoHash()
    {
        var dna = MachineDna.Build(Array.Empty<(MachineDnaPartKind, string, string?)>());

        Assert.Null(dna.Hash);
        Assert.False(dna.IsAvailable);
        Assert.Equal("DNA not available", dna.HashDisplay);
        Assert.Empty(dna.Parts);
    }

    [Fact]
    public void Build_OneIdentifiedPartAmongUnidentified_HasAHash()
    {
        var dna = MachineDna.Build(new[]
        {
            Bios("To be filled by O.E.M."),
            Board("BOARD123456789"),
        });

        Assert.NotNull(dna.Hash);
        Assert.True(dna.IsAvailable);
        Assert.Equal(dna.Hash, dna.HashDisplay);
    }

    [Fact]
    public void Compare_IdenticalDnas_AreIdentical()
    {
        var result = MachineDnaComparer.Compare(FullDna(), FullDna());

        Assert.Equal(MachineDnaVerdict.Identical, result.Verdict);
        Assert.Empty(result.Added);
        Assert.Empty(result.Removed);
        Assert.Empty(result.Changed);
    }

    [Fact]
    public void Compare_ChangedSerial_IsChangedWithThePartListed()
    {
        var old = FullDna();
        var swapped = MachineDna.Build(new[]
        {
            Board("BOARD123456789"),
            Bios("BIOS987654321"),
            Disk("Disk 0", "DISK-AAAA-1111"),
            Disk("Disk 1", "DISK-XXXX-9999"),
            Memory("Slot 0", "MEM-CCCC-3333"),
            Cpu("CPUID-0123456789ABCDEF"),
        });

        var result = MachineDnaComparer.Compare(old, swapped);

        Assert.Equal(MachineDnaVerdict.Changed, result.Verdict);
        var change = Assert.Single(result.Changed);
        Assert.Equal(MachineDnaPartKind.Disk, change.Before.Kind);
        Assert.Equal("Disk 1", change.Before.Label);
        Assert.Equal("DISK...2222", change.Before.SerialTruncated);
        Assert.Equal("DISK...9999", change.After.SerialTruncated);
        Assert.Empty(result.Added);
        Assert.Empty(result.Removed);
    }

    [Fact]
    public void Compare_RemovedPart_IsChangedWithThePartListed()
    {
        var old = FullDna();
        var oneDiskFewer = MachineDna.Build(new[]
        {
            Board("BOARD123456789"),
            Bios("BIOS987654321"),
            Disk("Disk 0", "DISK-AAAA-1111"),
            Memory("Slot 0", "MEM-CCCC-3333"),
            Cpu("CPUID-0123456789ABCDEF"),
        });

        var result = MachineDnaComparer.Compare(old, oneDiskFewer);

        Assert.Equal(MachineDnaVerdict.Changed, result.Verdict);
        var removed = Assert.Single(result.Removed);
        Assert.Equal(MachineDnaPartKind.Disk, removed.Kind);
        Assert.Equal("Disk 1", removed.Label);
        Assert.Empty(result.Added);
        Assert.Empty(result.Changed);
    }

    [Fact]
    public void Compare_AddedPart_IsChangedWithThePartListed()
    {
        var old = MachineDna.Build(new[]
        {
            Board("BOARD123456789"),
            Bios("BIOS987654321"),
            Disk("Disk 0", "DISK-AAAA-1111"),
        });
        var added = MachineDna.Build(new[]
        {
            Board("BOARD123456789"),
            Bios("BIOS987654321"),
            Disk("Disk 0", "DISK-AAAA-1111"),
            Disk("Disk 1", "DISK-BBBB-2222"),
        });

        var result = MachineDnaComparer.Compare(old, added);

        Assert.Equal(MachineDnaVerdict.Changed, result.Verdict);
        var part = Assert.Single(result.Added);
        Assert.Equal("Disk 1", part.Label);
        Assert.Empty(result.Removed);
        Assert.Empty(result.Changed);
    }

    [Fact]
    public void Compare_UnidentifiedPartInEitherScan_IsUnverifiable()
    {
        // The new scan lost the BIOS serial: the machines may be the same
        // or not, and the comparison must say so instead of guessing.
        var old = FullDna();
        var biosLost = MachineDna.Build(new[]
        {
            Board("BOARD123456789"),
            Bios("To be filled by O.E.M."),
            Disk("Disk 0", "DISK-AAAA-1111"),
            Disk("Disk 1", "DISK-BBBB-2222"),
            Memory("Slot 0", "MEM-CCCC-3333"),
            Cpu("CPUID-0123456789ABCDEF"),
        });

        var result = MachineDnaComparer.Compare(old, biosLost);

        Assert.Equal(MachineDnaVerdict.Unverifiable, result.Verdict);
    }

    [Fact]
    public void Compare_UnidentifiedPartInTheOldScan_IsUnverifiable()
    {
        var biosLost = MachineDna.Build(new[]
        {
            Board("BOARD123456789"),
            Bios("To be filled by O.E.M."),
            Disk("Disk 0", "DISK-AAAA-1111"),
        });
        var old = MachineDna.Build(new[]
        {
            Board("BOARD123456789"),
            Bios("BIOS987654321"),
            Disk("Disk 0", "DISK-AAAA-1111"),
        });

        Assert.Equal(MachineDnaVerdict.Unverifiable, MachineDnaComparer.Compare(biosLost, old).Verdict);
    }

    [Fact]
    public void Compare_EitherSideWithoutDna_IsUnverifiable()
    {
        Assert.Equal(
            MachineDnaVerdict.Unverifiable,
            MachineDnaComparer.Compare(null, FullDna()).Verdict);
        Assert.Equal(
            MachineDnaVerdict.Unverifiable,
            MachineDnaComparer.Compare(FullDna(), null).Verdict);
        Assert.Equal(
            MachineDnaVerdict.Unverifiable,
            MachineDnaComparer.Compare(MachineDna.Empty, FullDna()).Verdict);
        Assert.Equal(
            MachineDnaVerdict.Unverifiable,
            MachineDnaComparer.Compare(MachineDna.Empty, MachineDna.Empty).Verdict);
    }

    [Fact]
    public void Compare_TwoMachinesWithOnlyPlaceholders_AreNotIdentical()
    {
        // Both report the same placeholder BIOS serial: without the
        // placeholder rule they would hash alike and compare identical.
        var one = MachineDna.Build(new[] { Bios("To be filled by O.E.M.") });
        var other = MachineDna.Build(new[] { Bios("To be filled by O.E.M.") });

        Assert.Null(one.Hash);
        Assert.Null(other.Hash);
        Assert.Equal(MachineDnaVerdict.Unverifiable, MachineDnaComparer.Compare(one, other).Verdict);
    }

    [Fact]
    public void SystemSnapshot_WithoutDna_SaysNotRecorded()
    {
        var snapshot = TestSnapshots.Healthy();

        Assert.Null(snapshot.MachineDnaHash);
        Assert.Null(snapshot.MachineDnaParts);
        Assert.Equal("DNA not recorded", snapshot.MachineDnaStatus);
    }

    [Fact]
    public void SystemSnapshot_WithDna_ReportsItsHash()
    {
        var dna = FullDna();
        var snapshot = TestSnapshots.Healthy() with
        {
            MachineDnaHash = dna.Hash,
            MachineDnaParts = dna.Parts,
        };

        Assert.Equal(dna.Hash, snapshot.MachineDnaStatus);
    }

    [Fact]
    public void SystemSnapshot_WithDnaButNoUsableSerial_SaysNotAvailable()
    {
        var dna = MachineDna.Build(new[] { Bios("To be filled by O.E.M.") });
        var snapshot = TestSnapshots.Healthy() with
        {
            MachineDnaHash = dna.Hash,
            MachineDnaParts = dna.Parts,
        };

        Assert.Equal("DNA not available", snapshot.MachineDnaStatus);
    }
}
