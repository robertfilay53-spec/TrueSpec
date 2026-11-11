using TrueSpec.Core.Collectors;

namespace TrueSpec.Tests;

public class StartupProgramParserTests
{
    [Theory]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void ParseEnabledState_DecodesTheStartupApprovedDword(int state, bool expected)
    {
        var bytes = new byte[12];
        BitConverter.GetBytes(state).CopyTo(bytes, 0);

        Assert.Equal(expected, StartupProgramParser.ParseEnabledState(bytes));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public void ParseEnabledState_UnknownStateIsNullNeverAGuess(int state)
    {
        var bytes = new byte[12];
        BitConverter.GetBytes(state).CopyTo(bytes, 0);

        Assert.Null(StartupProgramParser.ParseEnabledState(bytes));
    }

    [Fact]
    public void ParseEnabledState_NullOrShortValueIsNull()
    {
        Assert.Null(StartupProgramParser.ParseEnabledState(null));
        Assert.Null(StartupProgramParser.ParseEnabledState(Array.Empty<byte>()));
        Assert.Null(StartupProgramParser.ParseEnabledState(new byte[] { 2, 0 }));
    }

    [Fact]
    public void CleanCommand_TrimsAndRejectsBlanks()
    {
        Assert.Equal(@"C:\Tools\app.exe", StartupProgramParser.CleanCommand("  C:\\Tools\\app.exe  "));
        Assert.Null(StartupProgramParser.CleanCommand(null));
        Assert.Null(StartupProgramParser.CleanCommand("   "));
    }

    [Fact]
    public void Build_RejectsBlankNamesAndBlankCommands()
    {
        Assert.Null(StartupProgramParser.Build(null, @"C:\app.exe", "Current user - Run", null));
        Assert.Null(StartupProgramParser.Build("  ", @"C:\app.exe", "Current user - Run", null));
        Assert.Null(StartupProgramParser.Build("App", null, "Current user - Run", null));
        Assert.Null(StartupProgramParser.Build("App", "   ", "Current user - Run", null));
    }

    [Fact]
    public void Build_BuildsAnEntryWithItsEnabledState()
    {
        var enabledBytes = new byte[12];
        BitConverter.GetBytes(2).CopyTo(enabledBytes, 0);
        var disabledBytes = new byte[12];
        BitConverter.GetBytes(3).CopyTo(disabledBytes, 0);

        var enabled = StartupProgramParser.Build("OneDrive", @"C:\OneDrive.exe", "Current user - Run", enabledBytes);
        var disabled = StartupProgramParser.Build("OldApp", @"C:\old.exe", "All users - Run", disabledBytes);
        var unknown = StartupProgramParser.Build("Mystery", @"C:\mystery.exe", "All users - Run", null);

        Assert.NotNull(enabled);
        Assert.Equal("OneDrive", enabled!.Name);
        Assert.Equal(@"C:\OneDrive.exe", enabled.Command);
        Assert.Equal("Current user - Run", enabled.Location);
        Assert.True(enabled.IsEnabled);
        Assert.Equal("Enabled", enabled.EnabledText);

        Assert.NotNull(disabled);
        Assert.False(disabled!.IsEnabled);
        Assert.Equal("Disabled", disabled.EnabledText);

        Assert.NotNull(unknown);
        Assert.Null(unknown!.IsEnabled);
        Assert.Equal("Not reported by Windows", unknown.EnabledText);
    }
}
