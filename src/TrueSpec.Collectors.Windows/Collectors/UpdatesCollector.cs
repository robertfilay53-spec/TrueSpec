namespace TrueSpec.Collectors.Windows.Collectors;

using System.Globalization;
using Microsoft.Win32;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;

/// <summary>
/// Windows Update status from read-only sources only: the last successful
/// install and check times from the Windows Update registry results, the
/// WSUS policy key, and the installed-hotfix list from
/// Win32_QuickFixEngineering. The Windows Update Agent COM API
/// (IUpdateSession) is deliberately not used. Nothing is installed,
/// downloaded or changed, and no elevation is needed: the results keys and
/// the policy key are world-readable and the hotfix class is readable by a
/// standard user. A missing registry value or an empty hotfix list is
/// reported as "Not reported by hardware", never filled in with a guess.
/// </summary>
public sealed class UpdatesCollector : ICollector
{
    public string Id => "updates";

    public string Name => "Windows Update";

    public string Category => "Security";

    public TimeSpan RefreshInterval => TimeSpan.FromMinutes(30);

    public bool RequiresAdministrator => false;

    private const string ResultsBasePath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\Results";

    private const string PolicyPath =
        @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";

    public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var lastInstall = WindowsUpdateStatusParser.ParseLastSuccessTime(
            ReadRegistryString($"{ResultsBasePath}\\Install", "LastSuccessTime"));
        var lastCheck = WindowsUpdateStatusParser.ParseLastSuccessTime(
            ReadRegistryString($"{ResultsBasePath}\\Detect", "LastSuccessTime"));
        var wsusServer = ReadRegistryString(PolicyPath, "WUServer");
        var isManaged = WindowsUpdateStatusParser.IsManaged(wsusServer);
        var server = string.IsNullOrWhiteSpace(wsusServer) ? null : wsusServer.Trim();

        var hotfixRows = Wmi.Query(
            "root\\cimv2",
            "SELECT HotFixID, InstalledOn, Description FROM Win32_QuickFixEngineering",
            out var hotfixError);
        // A truncation note with rows is partial data, not a failed query:
        // Wmi records the cut in `error` even when rows were returned.
        // Only an error with no rows means the hotfix list is unknown.
        var hotfixQueryFailed = hotfixError is not null && hotfixRows.Count == 0;

        var hotfixes = hotfixRows
            .Select(row => (Id: row.String("HotFixID"), Row: row))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Id))
            .Select(pair => new InstalledHotfix(
                pair.Id!.Trim(),
                WindowsUpdateStatusParser.ParseHotfixDate(pair.Row.String("InstalledOn")),
                pair.Row.String("Description")))
            .ToList();
        var shownHotfixes = WindowsUpdateStatusParser.OrderHotfixes(hotfixes);
        var now = DateTimeOffset.Now;
        var daysSinceInstall = WindowsUpdateStatusParser.DaysSince(lastInstall, now);

        if (lastInstall is null && lastCheck is null && hotfixes.Count == 0 && !isManaged)
        {
            return Task.FromResult(ComponentSnapshot.NotAvailable(
                Id,
                Name,
                Category,
                Id,
                "Windows Update results and the installed-hotfix list were not reported."));
        }

        var headline = WindowsUpdateStatusParser.Headline(
            hotfixQueryFailed ? null : hotfixes.Count,
            lastInstall);

        var specs = new List<SpecValue>
        {
            CollectorSupport.Spec(
                SpecLabels.UpdateManagement,
                isManaged
                    ? $"Managed - updates controlled by your organization (WSUS: {server})"
                    : "Not managed - Windows Update",
                "Registry WindowsUpdate policy (WUServer)"),
            CollectorSupport.Spec(
                SpecLabels.LastSuccessfulUpdateInstall,
                lastInstall?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                "Registry WindowsUpdate Auto Update Results Install LastSuccessTime"),
            CollectorSupport.Spec(
                SpecLabels.DaysSinceLastUpdateInstall,
                daysSinceInstall is null
                    ? null
                    : string.Create(CultureInfo.InvariantCulture, $"{daysSinceInstall.Value:0.0}"),
                "Registry WindowsUpdate Auto Update Results Install LastSuccessTime"),
            CollectorSupport.Spec(
                SpecLabels.LastSuccessfulUpdateCheck,
                lastCheck?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                "Registry WindowsUpdate Auto Update Results Detect LastSuccessTime"),
            CollectorSupport.Spec(
                SpecLabels.InstalledUpdatesHotfixes,
                // A count of 0 from a successful empty query is a real
                // measurement and is shown as 0; only a failed query is
                // reported as unknown.
                hotfixQueryFailed ? null : hotfixes.Count.ToString(CultureInfo.InvariantCulture),
                "CIM Win32_QuickFixEngineering"),
        };

        // Two hotfixes can share an ID in theory; duplicate spec labels
        // collapse in the comparison view, so number repeats.
        var usedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hotfix in shownHotfixes)
        {
            var label = $"Update - {hotfix.Id}";
            var numbered = label;
            var n = 2;
            while (!usedLabels.Add(numbered))
            {
                numbered = $"{label} #{n++}";
            }

            specs.Add(CollectorSupport.Spec(numbered, HotfixValue(hotfix), "CIM Win32_QuickFixEngineering"));
        }

        return Task.FromResult(ComponentSnapshot.NotMeasurable(
            Id,
            Name,
            Category,
            Id,
            headline,
            specs,
            "The last install date comes from the Windows Update registry results, and the list comes from " +
            "the installed hotfixes Windows records. On a managed (WSUS) machine, updates arrive on the " +
            "organization's schedule, so the date shows the last install the organization allowed rather than " +
            "proof that every patch is installed. TrueSpec only reads this information: it never installs " +
            "updates or changes update settings."));
    }

    private static string HotfixValue(InstalledHotfix hotfix)
    {
        var date = hotfix.InstalledOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var description = string.IsNullOrWhiteSpace(hotfix.Description) ? null : hotfix.Description.Trim();

        if (date is not null && description is not null)
        {
            return $"{date} / {description}";
        }

        if (description is not null)
        {
            return description;
        }

        return date ?? "Installed date not reported";
    }

    /// <summary>
    /// One read-only registry string value. A denied, missing or malformed
    /// key means unreadable, never an exception out of the collector.
    /// </summary>
    private static string? ReadRegistryString(string subKeyPath, string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(subKeyPath, writable: false);
            return key?.GetValue(valueName) as string;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException or ArgumentException)
        {
            return null;
        }
    }
}
