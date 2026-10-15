using System;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace Feature.ContextBar.Tests;

/// <summary>
/// Black-box behavior of <see cref="DiagnosticsLog"/> over a file under
/// %TEMP%\ClaudeCodePlus.Tests\Feature.ContextBar.Tests\&lt;test&gt;-&lt;8 hex&gt;, never deleted
/// (agreed behavior on this machine).
/// </summary>
public sealed class DiagnosticsLogTests
{
    private const string Report = "app 2.37337.1.0: no figures file for the shown session local_8804b5b6… (the context-bar mod is not publishing for it)";

    private static string File_([CallerMemberName] string testName = "") =>
        Path.Combine(Path.GetTempPath(), "ClaudeCodePlus.Tests", "Feature.ContextBar.Tests", testName + "-" + Guid.NewGuid().ToString("N")[..8], "diagnostics.log");

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
        Assert.Equal(20, lines[0].Length - Report.Length); // "yyyy-MM-dd HH:mm:ss " before it

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

        for (int i = 0; i < 10 * DiagnosticsLog.SettleCalls; i++)
        {
            log.Note(Report + " " + i);
        }

        Assert.False(File.Exists(file));
    }

    [Fact]
    public void The_same_report_after_a_normal_state_is_written_again_and_a_different_one_too()
    {
        string file = File_();
        var log = new DiagnosticsLog(file);

        Note(log, Report, DiagnosticsLog.SettleCalls);
        log.Note(null);
        Note(log, Report, DiagnosticsLog.SettleCalls);
        Note(log, "app 2.37337.1.0: a footer slot but no session page URL; documents: none", DiagnosticsLog.SettleCalls);

        string[] lines = File.ReadAllLines(file);
        Assert.Equal(3, lines.Length);
        Assert.EndsWith(Report, lines[1]);
        Assert.EndsWith("documents: none", lines[2]);
    }

    [Fact]
    public void A_null_path_writes_nothing_and_a_null_report_is_never_written()
    {
        var log = new DiagnosticsLog(null);
        Note(log, Report, 5 * DiagnosticsLog.SettleCalls);

        string file = File_();
        Note(new DiagnosticsLog(file), null, 5 * DiagnosticsLog.SettleCalls);
        Assert.False(File.Exists(file));
    }
}
