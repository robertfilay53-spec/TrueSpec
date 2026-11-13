using System.Reflection;
using TrueSpec.Core.Health;
using TrueSpec.Core.Models;

namespace TrueSpec.Tests;

/// <summary>
/// The honesty choke point (hardening 6a): the "not reported" text lives
/// in exactly one public constant, <see cref="SpecLabels.NotReportedByHardware"/>.
/// The literal used to appear 40 times across 20 files with three separate
/// private copies, so a wording change was 40 edits and a comparison could
/// silently miss a copy. This test greps the tree and fails on any other
/// occurrence. The literal is built by concatenation here so this file
/// itself does not contain it.
/// </summary>
public class HonestyChokePointTests
{
    // Built by concatenation on purpose: this file must not contain the
    // literal it greps for.
    private const string Literal = "Not reported by " + "hardware";

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TrueSpec.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not find TrueSpec.sln by walking up from " + AppContext.BaseDirectory);
    }

    [Fact]
    public void NotReportedByHardware_LiteralAppearsOnlyInItsDefinition()
    {
        Assert.Equal(Literal, SpecLabels.NotReportedByHardware);

        var root = FindRepoRoot();
        var offenders = new List<string>();
        foreach (var folder in new[] { "src", "tests" })
        {
            // Code and UI markup: a literal in an .axaml view would show
            // the same text through a second copy, with the same drift risk.
            // Docs (.md) are prose about the product rule, not code, and
            // are deliberately out of scope here.
            foreach (var pattern in new[] { "*.cs", "*.axaml" })
            {
                foreach (var file in Directory.EnumerateFiles(
                    Path.Combine(root, folder), pattern, SearchOption.AllDirectories))
                {
                    var lines = File.ReadAllLines(file);
                    for (var i = 0; i < lines.Length; i++)
                    {
                        if (!lines[i].Contains(Literal, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        // The one allowed occurrence: the constant definition.
                        var isDefinition = Path.GetFileName(file) == "SpecLabels.cs" &&
                            lines[i].Contains("public const string NotReportedByHardware", StringComparison.Ordinal);
                        if (!isDefinition)
                        {
                            offenders.Add($"{Path.GetRelativePath(root, file)}:{i + 1}");
                        }
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "The literal must only appear in the SpecLabels definition; use SpecLabels.NotReportedByHardware instead: " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void SpecValue_NotReportedFactory_IsHonestEmpty()
    {
        var spec = SpecValue.NotReported(SpecLabels.PowerOnHours, "SMART");

        Assert.Equal(SpecLabels.NotReportedByHardware, spec.Value);
        Assert.True(spec.IsNotReported);
        Assert.Null(spec.NumericValue);
        Assert.Null(spec.NumericUnit);
    }

    [Fact]
    public void SpecValue_IsNotReported_FalseForARealValue()
    {
        var spec = new SpecValue(SpecLabels.PowerOnHours, "12400 hours", "SMART", NumericValue: 12400d, NumericUnit: "hours");

        Assert.False(spec.IsNotReported);
        Assert.Equal(12400d, spec.NumericValue);
        Assert.Equal("hours", spec.NumericUnit);
    }

    [Fact]
    public void SpecText_Number_PrefersTheTypedValueOverParsing()
    {
        // The display string would parse to 1,000; the typed value is 5.
        // A consumer must get the measurement, not the parse.
        var specs = new[]
        {
            new SpecValue(SpecLabels.PowerOnHours, "1,000 hours", "SMART", NumericValue: 5d, NumericUnit: "hours"),
        };

        Assert.Equal(5d, SpecText.Number(specs, SpecLabels.PowerOnHours));
    }

    [Fact]
    public void SpecText_Number_IgnoresANonFiniteTypedValue()
    {
        // A corrupt or hand-edited snapshot must never feed a rule with
        // an infinite number: fall back to parsing the display string.
        var specs = new[]
        {
            new SpecValue(SpecLabels.PowerOnHours, "10 hours", "SMART", NumericValue: double.PositiveInfinity),
        };

        Assert.Equal(10d, SpecText.Number(specs, SpecLabels.PowerOnHours));
    }

    [Fact]
    public void SpecText_Number_FallsBackToParsingForOldSnapshots()
    {
        // Collectors that do not set the typed value yet, and snapshots
        // saved before the typed channel existed, still work.
        var specs = new[]
        {
            new SpecValue(SpecLabels.PowerOnHours, "12400 hours", "SMART"),
        };

        Assert.Equal(12400d, SpecText.Number(specs, SpecLabels.PowerOnHours));
    }
}
