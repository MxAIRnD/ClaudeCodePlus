using System;
using System.Globalization;
using System.IO;

namespace Feature.ChipButton;

/// <summary>
/// The diagnostics file: where the button reports what it could not find while a Claude window
/// with a composer was in front (<see cref="ChipRowLocator.Report"/>). <see cref="Note"/> is
/// called with the current report, or null for nothing missing, at every poll; a report is
/// written once it has been the same for <see cref="SettleCalls"/> consecutive calls (a few
/// seconds, so a page in transition is not reported), and not again until a different report
/// or a null came between. Each line is the local time and the report, appended; the file and
/// its folder are created with the first line. Null path: nothing is written. A file past
/// <see cref="MaxBytes"/> is left as it is, and a write that fails is dropped: diagnostics never
/// cost the button.
/// </summary>
public sealed class DiagnosticsLog
{
    /// <summary>How many consecutive identical reports it takes for one to be written: at the 400 ms poll, about five seconds.</summary>
    public const int SettleCalls = 12;

    /// <summary>The size past which nothing more is appended.</summary>
    public const long MaxBytes = 1_000_000;

    private readonly string? _path;
    private string? _pending;
    private int _seen;
    private bool _written;

    /// <summary>Creates a log appending to <paramref name="path"/>; null for one that writes nothing.</summary>
    public DiagnosticsLog(string? path)
    {
        _path = path;
    }

    /// <summary>Takes the current report (null for nothing missing); writes it as the class says.</summary>
    public void Note(string? report)
    {
        if (!string.Equals(report, _pending, StringComparison.Ordinal))
        {
            _pending = report;
            _seen = 0;
            _written = false;
        }

        if (report is null || _path is null || _written)
        {
            return;
        }

        _seen++;
        if (_seen < SettleCalls)
        {
            return;
        }

        _written = true;
        Append(_path, report);
    }

    private static void Append(string path, string report)
    {
        try
        {
            string? folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var file = new FileInfo(path);
            if (file.Exists && file.Length > MaxBytes)
            {
                return;
            }

            File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " " + report + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // dropped: diagnostics never cost the button
        }
    }
}
