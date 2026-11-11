using System.Collections.Generic;

namespace TrueSpec.App;

/// <summary>
/// Simple 24x24 path icons for the rail, cards and page headers. Kept as plain
/// path markup so the app has no icon font dependency.
/// </summary>
public static class IconLibrary
{
    private static readonly Dictionary<string, string> Icons = new()
    {
        ["overview"] = "M3,3 H10 V10 H3 Z M14,3 H21 V10 H14 Z M3,14 H10 V21 H3 Z M14,14 H21 V21 H14 Z",
        ["system"] = "M3,4 H21 V16 H3 Z M8,20 H16 M12,16 V20",
        ["cpu"] = "M8,8 H16 V16 H8 Z M4,9 H8 M4,15 H8 M16,9 H20 M16,15 H20 M9,4 V8 M15,4 V8 M9,16 V20 M15,16 V20",
        ["ram"] = "M3,8 H21 V16 H3 Z M7,8 V5 M12,8 V5 M17,8 V5 M7,16 V19 M12,16 V19 M17,16 V19",
        ["storage"] = "M4,6 C4,4 20,4 20,6 V18 C20,20 4,20 4,18 Z M4,6 C4,8 20,8 20,6 M4,12 C4,14 20,14 20,12",
        ["battery"] = "M3,8 H18 V16 H3 Z M20,10 V14 M6,10 V14",
        ["gpu"] = "M3,7 H21 V17 H3 Z M7,17 V20 M17,17 V20 M7,11 H10 M12,11 H15",
        ["display"] = "M3,5 H21 V16 H3 Z M8,20 H16 M12,16 V20",
        ["network"] = "M2,9 C8,4 16,4 22,9 M5,12 C9,9 15,9 19,12 M8,15 C10,13 14,13 16,15 M12,18 C12.55,18 13,18.45 13,19 C13,19.55 12.55,20 12,20 C11.45,20 11,19.55 11,19 C11,18.45 11.45,18 12,18",
        ["motherboard"] = "M4,4 H20 V20 H4 Z M8,8 H12 V12 H8 Z M14,8 H16 M14,12 H16 M8,16 H16",
        ["security"] = "M12,3 L20,6 V12 C20,17 16,20 12,21 C8,20 4,17 4,12 V6 Z",
        ["tests"] = "M8,4 H16 V7 H8 Z M6,6 H18 V21 H6 Z M9,12 L11,14 L15,10",
        ["report"] = "M6,3 H14 L18,7 V21 H6 Z M14,3 V7 H18 M9,12 H15 M9,16 H15",
        ["compare"] = "M4,6 H11 V18 H4 Z M13,6 H20 V18 H13 Z M8,10 H7 M8,14 H7 M17,10 H16 M17,14 H16",
        ["settings"] = "M12,9 C13.66,9 15,10.34 15,12 C15,13.66 13.66,15 12,15 C10.34,15 9,13.66 9,12 C9,10.34 10.34,9 12,9 M12,2 V5 M12,19 V22 M2,12 H5 M19,12 H22 M5,5 L7,7 M17,17 L19,19 M19,5 L17,7 M7,17 L5,19",
        ["drivers"] = "M4,6 H20 V18 H4 Z M8,10 H16 M8,14 H13",
        ["updates"] = "M12,3 V14 M7,9 L12,14 L17,9 M4,17 H20 V21 H4 Z",
        ["refresh"] = "M20,12 A8,8 0 1 1 17,6 M20,3 V7 H16",
        ["export"] = "M12,3 V15 M8,7 L12,3 L16,7 M4,14 V20 H20 V14",
        ["shield-check"] = "M12,3 L20,6 V12 C20,17 16,20 12,21 C8,20 4,17 4,12 V6 Z M8,12 L11,15 L16,9",
    };

    public static string Get(string key) =>
        Icons.TryGetValue(key, out var data) ? data : Icons["system"];
}
