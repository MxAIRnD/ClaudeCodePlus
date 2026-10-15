using System;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace Feature.ChipButton.Tests;

/// <summary>
/// Black-box behavior of <see cref="DiagnosticsLog"/> over a file under
/// %TEMP%\ClaudeCodePlus.Tests\Feature.ChipButton.Tests\&lt;test&gt;-&lt;8 hex&gt;, never deleted
/// (agreed behavior on this machine).
/// </summary>
public sealed class DiagnosticsLogTests
{
    private const string Report = "app 2.37337.1.0: a composer but no chip-row anchor; documents: none; buttons on the lowest row: 'Env: AI' 1012,1493 75x40";

    private static string File_([CallerMemberName] string testName = "") =>
        Path.Combine(Path.GetTempPath(), "ClaudeCodePlus.Tests", "Feature.ChipButton.Tests", testName + "-" + Guid.NewGuid().ToString("N")[..8], "diagnostics.log");

    private static void Note(DiagnosticsLog log, string? report, int times)
    {
        for (int i = 0; i < times; i++)
        {
            log.Note(report);
        }
    }

    [Fact]
    public void A_report_is_written_once_it_has_settled_and_then_only_once()
    {
        string file = File_();
        var log = new DiagnosticsLog(file);

        Note(log, Report, DiagnosticsLog.SettleCalls - 1);
        Assert.False(File.Exists(file));

        log.Note(Report);
        string[] lines = File.ReadAllLines(file);
        Assert.Single(lines);
        Assert.EndsWith(" " + Report, lines[0]);

        Note(log, Report, 100);
        Assert.Single(File.ReadAllLines(file));
    }

    [Fact]
    public void A_report_that_does_not_settle_is_not_written()
    {
        string file = File_();
        var log = new DiagnosticsLog(file);

        for (int i = 0; i < 10; i++)
        {
            Note(log, Report, DiagnosticsLog.SettleCalls - 1);
            log.Note(null);
        }

        Assert.False(File.Exists(file));
    }

    [Fact]
    public void The_same_report_after_a_normal_state_is_written_again()
    {
        string file = File_();
        var log = new DiagnosticsLog(file);

        Note(log, Report, DiagnosticsLog.SettleCalls);
        log.Note(null);
        Note(log, Report, DiagnosticsLog.SettleCalls);

        Assert.Equal(2, File.ReadAllLines(file).Length);
    }

    [Fact]
    public void A_null_path_writes_nothing()
    {
        Note(new DiagnosticsLog(null), Report, 5 * DiagnosticsLog.SettleCalls);
    }
}
