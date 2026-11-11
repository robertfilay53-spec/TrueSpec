namespace TrueSpec.Core.Collectors;

/// <summary>
/// One program Windows launches at sign-in, read from a Run / RunOnce
/// registry key. The command is shown exactly as stored; TrueSpec never
/// guesses what a program does from its name.
/// </summary>
public sealed record StartupProgram(
    string Name,
    string Command,
    string Location,
    bool? IsEnabled)
{
    public string EnabledText => IsEnabled switch
    {
        true => "Enabled",
        false => "Disabled",
        _ => "Not reported by Windows",
    };
}

/// <summary>
/// Pure parsing for startup entries, kept in Core so it is testable without
/// a Windows registry. The registry reading itself lives in the Windows
/// collector; this class only interprets what was read.
/// </summary>
public static class StartupProgramParser
{
    /// <summary>
    /// Decodes the Explorer StartupApproved value for a Run entry. Windows
    /// writes a 12-byte REG_BINARY: the first DWORD is 2 when the entry is
    /// enabled and 3 when the user disabled it in Task Manager. Any other
    /// shape is unknown, not enabled: it returns null instead of guessing.
    /// </summary>
    public static bool? ParseEnabledState(byte[]? approvedBytes)
    {
        if (approvedBytes is null || approvedBytes.Length < 4)
        {
            return null;
        }

        var state = BitConverter.ToInt32(approvedBytes, 0);
        return state switch
        {
            2 => true,
            3 => false,
            _ => null,
        };
    }

    /// <summary>
    /// Cleans a stored Run value: trims whitespace, expands %variables%
    /// using the current environment, and returns null for an empty value
    /// so the collector reports it as "Not reported by hardware" instead
    /// of showing a blank command.
    /// </summary>
    public static string? CleanCommand(string? rawCommand)
    {
        if (string.IsNullOrWhiteSpace(rawCommand))
        {
            return null;
        }

        var expanded = Environment.ExpandEnvironmentVariables(rawCommand.Trim());
        return string.IsNullOrWhiteSpace(expanded) ? null : expanded;
    }

    /// <summary>
    /// Builds one entry from a registry value. A blank name is never a
    /// program: it returns null so the caller skips the value.
    /// </summary>
    public static StartupProgram? Build(
        string? valueName,
        string? rawCommand,
        string location,
        byte[]? approvedBytes)
    {
        if (string.IsNullOrWhiteSpace(valueName))
        {
            return null;
        }

        var command = CleanCommand(rawCommand);
        if (command is null)
        {
            return null;
        }

        return new StartupProgram(
            valueName.Trim(),
            command,
            location,
            ParseEnabledState(approvedBytes));
    }
}
