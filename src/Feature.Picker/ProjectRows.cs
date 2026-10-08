using System;
using Feature.Catalog;

namespace Feature.Picker;

/// <summary>The list's row texts, as plain functions apart from the window.</summary>
public static class ProjectRows
{
    /// <summary>
    /// A short age label for a last-activity time seen from <paramref name="now"/>:
    /// <c>just now</c> under one minute (or in the future), <c>N min</c> under one hour,
    /// <c>N h</c> under two days, <c>N d</c> under thirty days, otherwise the date in local
    /// time as <c>yyyy-MM-dd</c>. Rounded down.
    /// </summary>
    public static string Age(DateTimeOffset when, DateTimeOffset now)
    {
        TimeSpan age = now - when;
        if (age < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (age < TimeSpan.FromHours(1))
        {
            return $"{(int)age.TotalMinutes} min";
        }

        if (age < TimeSpan.FromDays(2))
        {
            return $"{(int)age.TotalHours} h";
        }

        if (age < TimeSpan.FromDays(30))
        {
            return $"{(int)age.TotalDays} d";
        }

        return when.ToLocalTime().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The source column: <c>D</c> for the desktop session store, <c>T</c> for the terminal
    /// projects folder, <c>D+T</c> for both, empty for none.
    /// </summary>
    public static string SourcesLabel(ProjectSources sources)
    {
        bool desktop = sources.HasFlag(ProjectSources.DesktopSessions);
        bool terminal = sources.HasFlag(ProjectSources.TerminalProjects);
        return (desktop, terminal) switch
        {
            (true, true) => "D+T",
            (true, false) => "D",
            (false, true) => "T",
            _ => string.Empty,
        };
    }
}
