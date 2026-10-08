using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Feature.Catalog;
using Xunit;

namespace Feature.Catalog.Tests;

/// <summary>
/// IProjectCatalog.ListAsync over the Claude Code projects folder (CatalogOptions.TerminalProjectsRoot):
/// &lt;root&gt;\&lt;encoded&gt;\&lt;guid&gt;.jsonl transcripts, path from the first cwd, one record per transcript.
/// </summary>
public sealed class TerminalSourceTests
{
    private const long T = 1791446823792;

    [Fact]
    public async Task The_path_comes_from_the_transcript_cwd_not_from_the_encoded_folder_name()
    {
        var fx = CatalogFixture.Create();
        string real = fx.ExistingFolder("RealPlace");
        // The folder name decodes to C:\made\up\Elsewhere; listing missing folders would show it if it were used.
        fx.WriteTranscript("C--made-up-Elsewhere", real, CatalogFixture.Utc(T));
        CatalogOptions options = new() { TerminalProjectsRoot = fx.ProjectsRoot, IncludeMissingFolders = true };

        ProjectEntry entry = Assert.Single(await CatalogFixture.ListAsync(options));

        Assert.Equal(real, entry.Path);
        Assert.Equal("RealPlace", entry.Name);
        Assert.True(entry.FolderExists);
    }

    [Fact]
    public async Task A_terminal_only_folder_carries_the_terminal_source_transcript_count_and_newest_write_time()
    {
        var fx = CatalogFixture.Create();
        string folder = fx.ExistingFolder("Counted");
        fx.WriteTranscript("C--made-up-Counted", folder, CatalogFixture.Utc(T - 3_000));
        fx.WriteTranscript("C--made-up-Counted", folder, CatalogFixture.Utc(T));
        fx.WriteTranscript("C--made-up-Counted", folder, CatalogFixture.Utc(T - 7_000));

        ProjectEntry entry = Assert.Single(await CatalogFixture.ListAsync(fx.BothSources()));

        Assert.Equal(folder, entry.Path);
        Assert.Equal(3, entry.SessionCount);
        Assert.Equal(CatalogFixture.Ms(T), entry.LastActivity);
        Assert.Equal(TimeSpan.Zero, entry.LastActivity.Offset);
        Assert.Equal(ProjectSources.TerminalProjects, entry.Sources);
    }

    [Fact]
    public async Task Terminal_folders_are_ranked_by_their_newest_transcript()
    {
        var fx = CatalogFixture.Create();
        string older = fx.ExistingFolder("Older");
        string newer = fx.ExistingFolder("Newer");
        fx.WriteTranscript("C--made-up-Older", older, CatalogFixture.Utc(T - 2_000));
        fx.WriteTranscript("C--made-up-Newer", newer, CatalogFixture.Utc(T - 9_000));
        fx.WriteTranscript("C--made-up-Newer", newer, CatalogFixture.Utc(T));

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(fx.BothSources());

        Assert.Equal(new[] { newer, older }, list.Select(e => e.Path));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_folder_named_by_both_sources_is_one_entry_with_both_flags_the_newer_time_and_the_summed_count(bool terminalIsNewer)
    {
        var fx = CatalogFixture.Create();
        string folder = fx.ExistingFolder("Both");
        long desktopMs = terminalIsNewer ? T - 60_000 : T;
        long terminalMs = terminalIsNewer ? T : T - 60_000;
        fx.WriteRecord(folder, desktopMs);
        fx.WriteRecord(folder, desktopMs - 1_000, account: "acct-b", org: "org-b");
        fx.WriteTranscript("C--made-up-Both", folder, CatalogFixture.Utc(terminalMs));
        fx.WriteTranscript("C--made-up-Both", folder, CatalogFixture.Utc(terminalMs - 5_000));
        fx.WriteTranscript("C--made-up-Both", folder, CatalogFixture.Utc(terminalMs - 9_000));

        ProjectEntry entry = Assert.Single(await CatalogFixture.ListAsync(fx.BothSources()));

        Assert.Equal(folder, entry.Path);
        Assert.Equal(ProjectSources.DesktopSessions | ProjectSources.TerminalProjects, entry.Sources);
        Assert.Equal(5, entry.SessionCount);
        Assert.Equal(CatalogFixture.Ms(T), entry.LastActivity);
    }

    [Fact]
    public async Task Only_top_level_jsonl_files_of_a_project_folder_are_transcripts()
    {
        var fx = CatalogFixture.Create();
        string listed = fx.ExistingFolder("Listed");
        string nested = fx.ExistingFolder("NestedOnly");
        string other = fx.ExistingFolder("OtherExtension");
        string session = Guid.NewGuid().ToString("D");
        fx.WriteTranscript("C--made-up-Listed", listed, CatalogFixture.Utc(T - 5_000), fileName: session + ".jsonl");
        // Subfolders hold tool results and sub-transcripts, not sessions: none of these count.
        fx.WriteTranscriptLines("C--made-up-Listed", new[] { CatalogFixture.TranscriptLine(nested) }, CatalogFixture.Utc(T),
            fileName: Path.Combine(session, "tool-results", "made-up.txt"));
        fx.WriteTranscriptLines("C--made-up-Listed", new[] { CatalogFixture.TranscriptLine(nested) }, CatalogFixture.Utc(T),
            fileName: Path.Combine(session, "subagents", "agent-made-up.jsonl"));
        fx.WriteTranscriptLines("C--made-up-Nested", new[] { CatalogFixture.TranscriptLine(nested) }, CatalogFixture.Utc(T),
            fileName: Path.Combine(Guid.NewGuid().ToString("D"), "made-up.jsonl"));
        // Top level but not *.jsonl.
        fx.WriteTranscriptLines("C--made-up-Listed", new[] { CatalogFixture.TranscriptLine(other) }, CatalogFixture.Utc(T),
            fileName: "made-up.json");
        fx.WriteTranscriptLines("C--made-up-Other", new[] { CatalogFixture.TranscriptLine(other) }, CatalogFixture.Utc(T),
            fileName: "made-up.txt");

        ProjectEntry entry = Assert.Single(await CatalogFixture.ListAsync(fx.BothSources()));

        Assert.Equal(listed, entry.Path);
        Assert.Equal(1, entry.SessionCount);
        Assert.Equal(CatalogFixture.Ms(T - 5_000), entry.LastActivity);
    }

    [Fact]
    public async Task A_project_folder_without_a_transcript_is_skipped()
    {
        var fx = CatalogFixture.Create();
        string listed = fx.ExistingFolder("Listed");
        fx.WriteTranscript("C--made-up-Listed", listed, CatalogFixture.Utc(T));
        Directory.CreateDirectory(Path.Combine(fx.ProjectsRoot, "C--made-up-Empty"));
        CatalogOptions options = new() { TerminalProjectsRoot = fx.ProjectsRoot, IncludeMissingFolders = true };

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(options);

        Assert.Equal(new[] { listed }, list.Select(e => e.Path));
    }

    [Theory]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public async Task Only_the_first_50_lines_of_a_transcript_are_searched_for_a_cwd(int cwdLine, bool listed)
    {
        var fx = CatalogFixture.Create();
        string folder = fx.ExistingFolder("LateCwd");
        IEnumerable<string> lines = Enumerable.Range(1, cwdLine - 1).Select(_ => CatalogFixture.TranscriptLine(null, "summary"))
            .Append(CatalogFixture.TranscriptLine(folder));
        fx.WriteTranscriptLines("C--made-up-LateCwd", lines, CatalogFixture.Utc(T));

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(fx.BothSources());

        Assert.Equal(listed ? new[] { folder } : Array.Empty<string>(), list.Select(e => e.Path));
    }

    [Fact]
    public async Task A_transcript_without_a_cwd_still_counts_for_its_project_folder()
    {
        var fx = CatalogFixture.Create();
        string folder = fx.ExistingFolder("Partly");
        fx.WriteTranscript("C--made-up-Partly", folder, CatalogFixture.Utc(T - 5_000));
        // Newer and names no folder: the path is per project folder, the record is per transcript.
        fx.WriteTranscriptLines("C--made-up-Partly",
            new[] { CatalogFixture.TranscriptLine(null, "summary"), CatalogFixture.TranscriptLine(null, "user") }, CatalogFixture.Utc(T));

        ProjectEntry entry = Assert.Single(await CatalogFixture.ListAsync(fx.BothSources()));

        Assert.Equal(folder, entry.Path);
        Assert.Equal(2, entry.SessionCount);
        Assert.Equal(CatalogFixture.Ms(T), entry.LastActivity);
    }

    [Fact]
    public async Task Transcripts_of_one_project_folder_share_the_path_of_the_smallest_transcript()
    {
        var fx = CatalogFixture.Create();
        string fromSmall = fx.ExistingFolder("FromSmall");
        string fromLarge = fx.ExistingFolder("FromLarge");
        fx.WriteTranscriptLines("C--made-up-Shared",
            Enumerable.Range(0, 20).Select(_ => CatalogFixture.TranscriptLine(fromLarge)), CatalogFixture.Utc(T));
        fx.WriteTranscriptLines("C--made-up-Shared", new[] { CatalogFixture.TranscriptLine(fromSmall) }, CatalogFixture.Utc(T - 5_000));

        ProjectEntry entry = Assert.Single(await CatalogFixture.ListAsync(fx.BothSources()));

        Assert.Equal(fromSmall, entry.Path);
        Assert.Equal(2, entry.SessionCount);
        Assert.Equal(CatalogFixture.Ms(T), entry.LastActivity);
    }

    [Fact]
    public async Task A_project_folder_where_no_transcript_has_a_cwd_is_skipped()
    {
        var fx = CatalogFixture.Create();
        string listed = fx.ExistingFolder("Listed");
        fx.WriteTranscript("C--made-up-Listed", listed, CatalogFixture.Utc(T - 5_000));
        fx.WriteTranscriptLines("C--made-up-NoCwd", new[] { CatalogFixture.TranscriptLine(null, "summary") }, CatalogFixture.Utc(T));
        fx.WriteTranscriptLines("C--made-up-NoCwd", new[] { CatalogFixture.TranscriptLine(null, "user") }, CatalogFixture.Utc(T));
        CatalogOptions options = new() { TerminalProjectsRoot = fx.ProjectsRoot, IncludeMissingFolders = true };

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(options);

        Assert.Equal(new[] { listed }, list.Select(e => e.Path));
    }

    [Fact]
    public async Task A_session_that_changed_folder_keeps_its_first_cwd()
    {
        var fx = CatalogFixture.Create();
        string first = fx.ExistingFolder("StartedHere");
        string later = fx.ExistingFolder("MovedThere");
        fx.WriteTranscriptLines("C--made-up-StartedHere", new[]
        {
            CatalogFixture.TranscriptLine(null, "summary"),
            CatalogFixture.TranscriptLine(first, "user"),
            CatalogFixture.TranscriptLine(later, "assistant"),
            CatalogFixture.TranscriptLine(later, "user"),
        }, CatalogFixture.Utc(T));

        ProjectEntry entry = Assert.Single(await CatalogFixture.ListAsync(fx.BothSources()));

        Assert.Equal(first, entry.Path);
        Assert.Equal(1, entry.SessionCount);
    }

    [Fact]
    public async Task A_scratch_workspace_cwd_is_hidden_by_default()
    {
        var fx = CatalogFixture.Create();
        string keep = fx.ExistingFolder("RealProject");
        string scratch = fx.ExistingFolder("Roaming", "Claude", "scratch-workspaces", "made-up-ws", "scratch-0001");
        fx.WriteTranscript("C--made-up-RealProject", keep, CatalogFixture.Utc(T - 1_000));
        fx.WriteTranscript("C--made-up-scratch-0001", scratch, CatalogFixture.Utc(T));

        IReadOnlyList<ProjectEntry> hidden = await CatalogFixture.ListAsync(fx.BothSources());
        IReadOnlyList<ProjectEntry> shown = await CatalogFixture.ListAsync(
            new CatalogOptions { TerminalProjectsRoot = fx.ProjectsRoot, IncludeScratchWorkspaces = true });

        Assert.Equal(new[] { keep }, hidden.Select(e => e.Path));
        Assert.Equal(new[] { scratch, keep }, shown.Select(e => e.Path));
    }

    [Fact]
    public async Task A_worktree_cwd_and_a_missing_cwd_are_hidden_by_default()
    {
        var fx = CatalogFixture.Create();
        string keep = fx.ExistingFolder("RealProject");
        string worktree = fx.ExistingFolder("RealProject", ".claude", "worktrees", "made-up-tree");
        string missing = fx.MissingFolder("LongGone");
        fx.WriteTranscript("C--made-up-RealProject", keep, CatalogFixture.Utc(T - 1_000));
        fx.WriteTranscript("C--made-up-RealProject--claude-worktrees-made-up-tree", worktree, CatalogFixture.Utc(T));
        fx.WriteTranscript("C--made-up-LongGone", missing, CatalogFixture.Utc(T));

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(fx.BothSources());

        Assert.Equal(new[] { keep }, list.Select(e => e.Path));
    }

    [Fact]
    public async Task A_terminal_root_that_does_not_exist_contributes_nothing_and_the_desktop_store_still_lists()
    {
        var fx = CatalogFixture.Create();
        string desktop = fx.ExistingFolder("DesktopOnly");
        fx.WriteRecord(desktop, T);
        CatalogOptions options = new() { DesktopSessionsRoot = fx.SessionsRoot, TerminalProjectsRoot = fx.MissingFolder("no-such-projects") };

        ProjectEntry entry = Assert.Single(await CatalogFixture.ListAsync(options));

        Assert.Equal(desktop, entry.Path);
        Assert.Equal(ProjectSources.DesktopSessions, entry.Sources);
    }

    [Fact]
    public async Task A_null_desktop_root_leaves_the_terminal_source_working()
    {
        var fx = CatalogFixture.Create();
        string terminal = fx.ExistingFolder("TerminalOnly");
        fx.WriteTranscript("C--made-up-TerminalOnly", terminal, CatalogFixture.Utc(T));

        ProjectEntry entry = Assert.Single(await CatalogFixture.ListAsync(new CatalogOptions { TerminalProjectsRoot = fx.ProjectsRoot }));

        Assert.Equal(terminal, entry.Path);
        Assert.Equal(ProjectSources.TerminalProjects, entry.Sources);
    }

    [Fact]
    public async Task Every_call_scans_afresh_so_a_transcript_added_between_calls_shows_up()
    {
        var fx = CatalogFixture.Create();
        string first = fx.ExistingFolder("First");
        string second = fx.ExistingFolder("Second");
        fx.WriteTranscript("C--made-up-First", first, CatalogFixture.Utc(T - 5_000));
        IProjectCatalog catalog = CatalogFixture.Catalog(fx.BothSources());

        IReadOnlyList<ProjectEntry> before = await catalog.ListAsync();
        fx.WriteTranscript("C--made-up-First", first, CatalogFixture.Utc(T - 4_000));
        fx.WriteTranscript("C--made-up-Second", second, CatalogFixture.Utc(T));
        IReadOnlyList<ProjectEntry> after = await catalog.ListAsync();

        Assert.Equal(1, Assert.Single(before).SessionCount);
        Assert.Equal(new[] { second, first }, after.Select(e => e.Path));
        Assert.Equal(2, after[1].SessionCount);
        Assert.Equal(CatalogFixture.Ms(T - 4_000), after[1].LastActivity);
    }

    [Fact]
    public async Task Listing_writes_nothing_to_the_projects_folder()
    {
        var fx = CatalogFixture.Create();
        string file = fx.WriteTranscript("C--made-up-ReadOnly", fx.ExistingFolder("ReadOnly"), CatalogFixture.Utc(T));
        string[] filesBefore = Directory.GetFiles(fx.ProjectsRoot, "*", SearchOption.AllDirectories);

        await CatalogFixture.ListAsync(fx.BothSources());

        Assert.Equal(CatalogFixture.Utc(T), File.GetLastWriteTimeUtc(file));
        Assert.Equal(filesBefore, Directory.GetFiles(fx.ProjectsRoot, "*", SearchOption.AllDirectories));
    }
}
