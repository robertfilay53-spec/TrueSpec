namespace TrueSpec.Core.Diagnostics;

/// <summary>
/// Validates calls to external Windows tools before they run. TrueSpec only
/// ever runs a small allowlist of built-in tools with fixed argument shapes;
/// anything else is rejected so a bad value can never turn into a shell
/// command. There is no shell involved at all: UseShellExecute is always off.
/// </summary>
public static class ExternalToolValidator
{
    private static readonly string[] AllowedTools =
    {
        "netsh",
    };

    private static readonly char[] ForbiddenArgumentChars =
    {
        '&', '|', ';', '<', '>', '\n', '\r', '"', '\'', '`', '$', '(', ')', '%', '!',
    };

    /// <summary>
    /// Returns true when the tool name is on the allowlist and the arguments
    /// contain only plain words for a known subcommand shape.
    /// </summary>
    public static bool IsAllowed(string? fileName, string? arguments)
    {
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(arguments))
        {
            return false;
        }

        var tool = fileName.Trim();
        if (!AllowedTools.Contains(tool, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        // No path separators: the tool must resolve from the system PATH, never
        // from a caller-supplied directory.
        if (tool.Contains(Path.DirectorySeparatorChar) || tool.Contains(Path.AltDirectorySeparatorChar))
        {
            return false;
        }

        if (arguments.IndexOfAny(ForbiddenArgumentChars) >= 0)
        {
            return false;
        }

        // netsh only ever runs these two read-only queries in TrueSpec.
        var args = arguments.Trim();
        return string.Equals(args, "wlan show interfaces", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(args, "wlan show drivers", StringComparison.OrdinalIgnoreCase);
    }
}
