using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Feature.Catalog;
using Xunit;

namespace Feature.Catalog.Tests;

/// <summary>
/// IProjectCatalog.ListAsync: what the desktop source reads, what it skips, how it fails; the
/// terminal root switch; the streaming read and the memo of unchanged records.
/// </summary>
public sealed class SourceInputTests
{
    private const long T = 1791446823792;

    [Fact]
    public async Task Records_are_found_at_any_depth_under_the_root()
    {
        var fx = CatalogFixture.Create();
        string a = fx.ExistingFolder("FromAccountA");
        string b = fx.ExistingFolder("FromAccountB");
        fx.WriteRecord(a, T, account: "acct-aaaa", org: "org-1111");
        fx.WriteRecord(b, T - 1_000, account: "acct-bbbb", org: Path.Combine("org-2222", "nested-deeper"));

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(fx.Options());

        Assert.Equal(new[] { a, b }, list.Select(e => e.Path));
    }

    [Fact]
    public async Task Only_local_star_json_files_are_read()
    {
        var fx = CatalogFixture.Create();
        string listed = fx.ExistingFolder("Listed");
        string ignored = fx.ExistingFolder("Ignored");
        fx.WriteRecord(listed, T - 1_000);
        fx.WriteJson(new { sessionId = "other_1", originCwd = ignored, lastActivityAt = T }, fileName: "other_made-up.json");

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(fx.Options());

        Assert.Equal(new[] { listed }, list.Select(e => e.Path));
    }

    [Fact]
    public async Task A_malformed_file_is_skipped_and_the_rest_still_listed()
    {
        var fx = CatalogFixture.Create();
        string good = fx.ExistingFolder("Good");
        fx.WriteRecord(good, T);
        fx.WriteRaw("{\"sessionId\":\"local_bad\",\"originCwd\":\"C:\\\\made\\\\up\" this is not json");

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(fx.Options());

        Assert.Equal(new[] { good }, list.Select(e => e.Path));
    }

    [Theory]
    [InlineData("[1,2,3]")]
    [InlineData("\"just a string\"")]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("")]
    public async Task A_file_whose_json_is_not_an_object_is_skipped(string text)
    {
        var fx = CatalogFixture.Create();
        string good = fx.ExistingFolder("Good");
        fx.WriteRecord(good, T);
        fx.WriteRaw(text);

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(fx.Options());

        Assert.Equal(new[] { good }, list.Select(e => e.Path));
    }

    [Fact]
    public async Task A_record_without_origin_cwd_is_skipped_even_when_it_has_a_cwd()
    {
        var fx = CatalogFixture.Create();
        string good = fx.ExistingFolder("Good");
        string cwdOnly = fx.ExistingFolder("CwdOnly");
        fx.WriteRecord(good, T - 1_000);
        fx.WriteJson(new { sessionId = "local_nocwd", cwd = cwdOnly, lastActivityAt = T, title = "made up" });

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(fx.Options());

        Assert.Equal(new[] { good }, list.Select(e => e.Path));
    }

    [Fact]
    public async Task A_record_with_an_empty_origin_cwd_is_skipped()
    {
        var fx = CatalogFixture.Create();
        string good = fx.ExistingFolder("Good");
        fx.WriteRecord(good, T - 1_000);
        fx.WriteRecord("", T);
        CatalogOptions options = new() { DesktopSessionsRoot = fx.SessionsRoot, IncludeMissingFolders = true };

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(options);

        Assert.Equal(new[] { good }, list.Select(e => e.Path));
    }

    [Fact]
    public async Task A_null_desktop_root_gives_an_empty_list()
    {
        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(new CatalogOptions { IncludeMissingFolders = true });

        Assert.Empty(list);
    }

    [Fact]
    public async Task A_desktop_root_that_does_not_exist_gives_an_empty_list()
    {
        var fx = CatalogFixture.Create();
        CatalogOptions options = new() { DesktopSessionsRoot = fx.MissingFolder("no-such-store"), IncludeMissingFolders = true };

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(options);

        Assert.Empty(list);
    }

    [Fact]
    public async Task A_terminal_projects_root_is_read_alongside_the_desktop_store()
    {
        var fx = CatalogFixture.Create();
        string desktop = fx.ExistingFolder("DesktopOnly");
        string terminal = fx.ExistingFolder("TerminalOnly");
        fx.WriteRecord(desktop, T - 1_000);
        fx.WriteTranscript("C--made-up-TerminalOnly", terminal, CatalogFixture.Utc(T));

        IReadOnlyList<ProjectEntry> without = await CatalogFixture.ListAsync(fx.Options());
        IReadOnlyList<ProjectEntry> with = await CatalogFixture.ListAsync(fx.BothSources());

        Assert.Equal(new[] { desktop }, without.Select(e => e.Path));
        Assert.Equal(new[] { terminal, desktop }, with.Select(e => e.Path));
        Assert.Equal(ProjectSources.TerminalProjects, with[0].Sources);
        Assert.Equal(ProjectSources.DesktopSessions, with[1].Sources);
    }

    [Fact]
    public async Task A_cancelled_token_throws_operation_canceled()
    {
        var fx = CatalogFixture.Create();
        fx.WriteRecord(fx.ExistingFolder("Any"), T);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        IProjectCatalog catalog = CatalogFixture.Catalog(fx.Options());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => catalog.ListAsync(cts.Token));
    }

    [Fact]
    public async Task Listing_writes_nothing_to_the_session_store()
    {
        var fx = CatalogFixture.Create();
        string file = fx.WriteRecord(fx.ExistingFolder("ReadOnly"), T);
        DateTime before = File.GetLastWriteTimeUtc(file);
        string[] filesBefore = Directory.GetFiles(fx.SessionsRoot, "*", SearchOption.AllDirectories);

        await CatalogFixture.ListAsync(fx.Options());

        Assert.Equal(before, File.GetLastWriteTimeUtc(file));
        Assert.Equal(filesBefore, Directory.GetFiles(fx.SessionsRoot, "*", SearchOption.AllDirectories));
    }

    // --- the streaming read: a record counts once its folder and time are known ---

    [Fact]
    public async Task A_record_cut_off_after_its_folder_and_time_is_still_listed()
    {
        var fx = CatalogFixture.Create();
        string cut = fx.ExistingFolder("Cut");
        fx.WriteRaw("{\"sessionId\":\"local_cut\",\"originCwd\":" + JsonSerializer.Serialize(cut)
            + ",\"lastActivityAt\":" + T + ",\"title\":\"made up");

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(fx.Options());

        ProjectEntry entry = Assert.Single(list);
        Assert.Equal(cut, entry.Path);
        Assert.Equal(CatalogFixture.Ms(T), entry.LastActivity);
    }

    // --- the memo: a record is read again only when its size or write time changed ---

    [Fact]
    public async Task A_record_rewritten_in_place_is_read_again_on_the_next_list()
    {
        var fx = CatalogFixture.Create();
        string folder = fx.ExistingFolder("Rewritten");
        IProjectCatalog catalog = CatalogFixture.Catalog(fx.Options());
        fx.WriteJson(new { sessionId = "local_same", originCwd = folder, lastActivityAt = T - 1_000, title = "v1" }, fileName: "local_same.json");

        IReadOnlyList<ProjectEntry> before = await catalog.ListAsync();
        // A longer title changes the size as well, so the test does not rest on the clock's resolution.
        fx.WriteJson(new { sessionId = "local_same", originCwd = folder, lastActivityAt = T, title = "v2, longer" }, fileName: "local_same.json");
        IReadOnlyList<ProjectEntry> after = await catalog.ListAsync();

        Assert.Equal(CatalogFixture.Ms(T - 1_000), Assert.Single(before).LastActivity);
        Assert.Equal(CatalogFixture.Ms(T), Assert.Single(after).LastActivity);
    }

    [Fact]
    public async Task An_unchanged_record_is_listed_from_memory_even_while_another_process_holds_it()
    {
        var fx = CatalogFixture.Create();
        string folder = fx.ExistingFolder("Held");
        string file = fx.WriteRecord(folder, T);
        IProjectCatalog catalog = CatalogFixture.Catalog(fx.Options());

        IReadOnlyList<ProjectEntry> before = await catalog.ListAsync();
        IReadOnlyList<ProjectEntry> during;
        using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            during = await catalog.ListAsync();
        }

        Assert.Equal(folder, Assert.Single(before).Path);
        Assert.Equal(folder, Assert.Single(during).Path);
    }

    [Fact]
    public async Task A_record_that_could_not_be_opened_is_read_once_it_can_be()
    {
        var fx = CatalogFixture.Create();
        string folder = fx.ExistingFolder("Locked");
        string file = fx.WriteRecord(folder, T);
        IProjectCatalog catalog = CatalogFixture.Catalog(fx.Options());

        IReadOnlyList<ProjectEntry> during;
        using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            during = await catalog.ListAsync();
        }

        IReadOnlyList<ProjectEntry> after = await catalog.ListAsync();

        Assert.Empty(during);
        Assert.Equal(folder, Assert.Single(after).Path);
    }

    [Fact]
    public async Task A_record_renamed_away_from_the_pattern_is_dropped_on_the_next_list()
    {
        var fx = CatalogFixture.Create();
        string folder = fx.ExistingFolder("Renamed");
        string file = fx.WriteRecord(folder, T);
        IProjectCatalog catalog = CatalogFixture.Catalog(fx.Options());

        IReadOnlyList<ProjectEntry> before = await catalog.ListAsync();
        File.Move(file, Path.Combine(Path.GetDirectoryName(file)!, "other_renamed.json"));
        IReadOnlyList<ProjectEntry> after = await catalog.ListAsync();

        Assert.Equal(folder, Assert.Single(before).Path);
        Assert.Empty(after);
    }
}
