using TrueSpec.Core.Health;
using TrueSpec.Core.Models;

namespace TrueSpec.Tests;

public class SpecTextTests
{
    [Theory]
    [InlineData("16 GB", 16UL << 30)]
    [InlineData("16.0 GB", 16UL << 30)]
    [InlineData("16GB", 16UL << 30)]
    [InlineData("16 GiB", 16UL << 30)]
    [InlineData("16384 MB", 16UL << 30)]
    [InlineData("16", 16UL << 30)]
    [InlineData("1 TB", 1UL << 40)]
    [InlineData("512 MB", 512UL << 20)]
    public void TryParseMemorySizeBytes_ParsesBinarySizes(string text, ulong expected)
    {
        Assert.True(SpecText.TryParseMemorySizeBytes(text, out var bytes));
        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(SpecLabels.NotReportedByHardware)]
    [InlineData("16 XB")]
    [InlineData("GB")]
    [InlineData("-16 GB")]
    public void TryParseMemorySizeBytes_RejectsUnparseableText(string? text)
    {
        Assert.False(SpecText.TryParseMemorySizeBytes(text, out _));
    }
}
