using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Xunit;

namespace Feature.ContextBar.Tests;

/// <summary>
/// Black-box behavior of <see cref="FocusedSessionFinder"/> over a synthetic session store under
/// %TEMP%\ClaudeCodePlus.Tests\Feature.ContextBar.Tests\&lt;test&gt;-&lt;8 hex&gt;, never deleted
/// (agreed behavior on this machine).
/// </summary>
public sealed class FocusedSessionFinderTests
{
    private static string Store([CallerMemberName] string testName = "")
    {
        string root = Path.Combine(Path.GetTempPath(), "ClaudeCodePlus.Tests", "Feature.ContextBar.Tests", testName + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        return root;
    }

    private static string Record(string store, string? cliSessionId, long? lastFocusedAt, bool? isArchived = null, string folder = "acct\\org", string? fileName = null, string title = "made up")
    {
        var record = new Dictionary<string, object?> { ["sessionId"] = "local_" + Guid.NewGuid().ToString("N")[..6], ["title"] = title };
        if (cliSessionId is not null) record["cliSessionId"] = cliSessionId;
        if (lastFocusedAt is not null) record["lastFocusedAt"] = lastFocusedAt.Value;
        if (isArchived is not null) record["isArchived"] = isArchived.Value;
        record["lastActivityAt"] = 1792054399179;
        string dir = Path.Combine(store, folder);
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, fileName ?? "local_" + Guid.NewGuid().ToString("D") + ".json");
        File.WriteAllText(file, JsonSerializer.Serialize(record));
        return file;
    }

    [Fact]
    public void The_newest_focused_unarchived_record_with_an_engine_id_wins()
    {
        string store = Store();
        Record(store, "a", 100);
        Record(store, "b", 200, folder: "acct\\org2");
        Record(store, "c", 300, isArchived: true);
        Record(store, null, 400);
        Record(store, "e", null);

        Assert.Equal("b", new FocusedSessionFinder(store).FindNow());
    }

    [Fact]
    public void The_shown_title_picks_its_session_the_newest_focused_among_namesakes_else_the_newest_of_all()
    {
        string store = Store();
        Record(store, "a", 100, title: "Deploy latest player version");
        Record(store, "b", 900, title: "Something else");
        Record(store, "c", 200, title: "Deploy latest player version");
        Record(store, "d", 950, title: "Deploy latest player version", isArchived: true);
        var finder = new FocusedSessionFinder(store);

        Assert.Equal("c", finder.FindNow("Deploy latest player version"));
        Assert.Equal("b", finder.FindNow("No such session"));
        Assert.Equal("b", finder.FindNow(null));
        Assert.Equal("b", finder.FindNow("deploy latest player version")); // titles match exactly
    }

    [Fact]
    public void A_record_rewritten_with_a_newer_focus_is_read_again()
    {
        string store = Store();
        string a = Record(store, "a", 100, fileName: "local_a.json");
        Record(store, "b", 200);
        var finder = new FocusedSessionFinder(store);
        Assert.Equal("b", finder.FindNow());

        File.WriteAllText(a, """{"cliSessionId":"a","lastFocusedAt":500,"isArchived":false,"extra":"a longer record now"}""");
        File.SetLastWriteTimeUtc(a, DateTime.UtcNow.AddSeconds(5));

        Assert.Equal("a", finder.FindNow());
    }

    [Fact]
    public void Find_answers_from_the_last_scan_for_a_second_and_FindNow_scans_at_once()
    {
        string store = Store();
        Record(store, "a", 100);
        var finder = new FocusedSessionFinder(store);
        Assert.Equal("a", finder.Find());

        Record(store, "b", 200);

        Assert.Equal("a", finder.Find());
        Assert.Equal("b", finder.FindNow());
        Assert.Equal("b", finder.Find());
    }

    [Fact]
    public void A_record_that_is_not_json_is_skipped_and_the_others_still_count()
    {
        string store = Store();
        Record(store, "a", 100);
        File.WriteAllText(Path.Combine(store, "local_broken.json"), "{ not json");
        File.WriteAllText(Path.Combine(store, "local_array.json"), "[1, 2]");
        File.WriteAllText(Path.Combine(store, "other.json"), """{"cliSessionId":"z","lastFocusedAt":999}""");

        Assert.Equal("a", new FocusedSessionFinder(store).FindNow());
    }

    [Fact]
    public void A_missing_or_null_root_knows_no_session()
    {
        Assert.Null(new FocusedSessionFinder(null).FindNow());
        Assert.Null(new FocusedSessionFinder(Path.Combine(Store(), "gone")).FindNow());
        Assert.Null(new FocusedSessionFinder(Store()).FindNow());
    }
}
