using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Feature.Catalog;
using Xunit;

namespace Feature.Catalog.Tests;

/// <summary>IProjectCatalog.ListAsync: order, one entry per folder, entry fields.</summary>
public sealed class RankingAndMergeTests
{
    private const long T = 1791446823792;

    [Fact]
    public async Task Folders_are_listed_most_recently_used_first()
    {
        var fx = CatalogFixture.Create();
        string older = fx.ExistingFolder("Older");
        string newest = fx.ExistingFolder("Newest");
        string middle = fx.ExistingFolder("Middle");
        fx.WriteRecord(older, T - 2_000);
        fx.WriteRecord(newest, T);
        fx.WriteRecord(middle, T - 1_000);

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(fx.Options());

        Assert.Equal(new[] { newest, middle, older }, list.Select(e => e.Path));
    }

    [Fact]
    public async Task Equal_activity_is_ordered_by_path_ascending_ignoring_case()
    {
        var fx = CatalogFixture.Create();
        string beta = fx.ExistingFolder("beta");
        string alphaUpper = fx.ExistingFolder("Alpha");
        string charlie = fx.ExistingFolder("Charlie");
        fx.WriteRecord(charlie, T);
        fx.WriteRecord(beta, T);
        fx.WriteRecord(alphaUpper, T);

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(fx.Options());

        // Ordinal-ignoring-case: "Alpha" < "beta" < "Charlie" (plain ordinal would put "beta" last).
        Assert.Equal(new[] { alphaUpper, beta, charlie }, list.Select(e => e.Path));
    }

    [Fact]
    public async Task Records_differing_only_in_case_merge_into_one_entry_with_the_newest_spelling()
    {
        var fx = CatalogFixture.Create();
        string folder = fx.ExistingFolder("MixedCase");
        string lower = folder.Substring(0, folder.Length - "MixedCase".Length) + "mixedcase";
        string upper = folder.Substring(0, folder.Length - "MixedCase".Length) + "MIXEDCASE";
        fx.WriteRecord(lower, T - 5_000);
        fx.WriteRecord(upper, T);
        fx.WriteRecord(folder, T - 1_000);

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(fx.Options());

        ProjectEntry entry = Assert.Single(list);
        Assert.Equal(upper, entry.Path);
        Assert.Equal(3, entry.SessionCount);
        Assert.Equal(CatalogFixture.Ms(T), entry.LastActivity);
    }

    [Fact]
    public async Task A_trailing_separator_is_dropped_and_merges_with_the_bare_path()
    {
        var fx = CatalogFixture.Create();
        string folder = fx.ExistingFolder("Trailing");
        fx.WriteRecord(folder + "\\", T);
        fx.WriteRecord(folder, T - 1_000);

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(fx.Options());

        ProjectEntry entry = Assert.Single(list);
        Assert.Equal(folder, entry.Path);
        Assert.Equal(2, entry.SessionCount);
    }

    [Fact]
    public async Task An_entry_carries_newest_activity_record_count_desktop_source_and_existence()
    {
        var fx = CatalogFixture.Create();
        string folder = fx.ExistingFolder("Counted");
        fx.WriteRecord(folder, T - 3_000, account: "acct-a", org: "org-a");
        fx.WriteRecord(folder, T, account: "acct-b", org: "org-b");
        fx.WriteRecord(folder, T - 7_000, account: "acct-a", org: "org-c");

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(fx.Options());

        ProjectEntry entry = Assert.Single(list);
        Assert.Equal(folder, entry.Path);
        Assert.Equal("Counted", entry.Name);
        Assert.Equal(CatalogFixture.Ms(T), entry.LastActivity);
        Assert.Equal(TimeSpan.Zero, entry.LastActivity.Offset);
        Assert.Equal(3, entry.SessionCount);
        Assert.Equal(ProjectSources.DesktopSessions, entry.Sources);
        Assert.True(entry.FolderExists);
    }

    [Fact]
    public async Task Name_is_the_last_path_segment()
    {
        var fx = CatalogFixture.Create();
        string folder = fx.ExistingFolder("outer", "inner.part", "Leaf Name");
        fx.WriteRecord(folder, T);

        ProjectEntry entry = Assert.Single(await CatalogFixture.ListAsync(fx.Options()));

        Assert.Equal("Leaf Name", entry.Name);
    }

    [Fact]
    public async Task Name_of_a_drive_root_is_the_path_itself()
    {
        var fx = CatalogFixture.Create();
        // A made-up drive root; IncludeMissingFolders keeps the test independent of whether it exists.
        fx.WriteRecord(@"Q:\", T);
        CatalogOptions options = new() { DesktopSessionsRoot = fx.SessionsRoot, IncludeMissingFolders = true };

        ProjectEntry entry = Assert.Single(await CatalogFixture.ListAsync(options));

        Assert.Equal(entry.Path, entry.Name);
        Assert.StartsWith("Q:", entry.Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Last_activity_falls_back_to_created_at_when_last_activity_at_is_missing()
    {
        var fx = CatalogFixture.Create();
        string folder = fx.ExistingFolder("CreatedOnly");
        fx.WriteRecord(folder, lastActivityAt: null, createdAt: T - 60_000);

        ProjectEntry entry = Assert.Single(await CatalogFixture.ListAsync(fx.Options()));

        Assert.Equal(CatalogFixture.Ms(T - 60_000), entry.LastActivity);
    }

    [Fact]
    public async Task Last_activity_at_wins_over_created_at_when_both_are_present()
    {
        var fx = CatalogFixture.Create();
        string folder = fx.ExistingFolder("Both");
        fx.WriteRecord(folder, lastActivityAt: T, createdAt: T - 60_000);

        ProjectEntry entry = Assert.Single(await CatalogFixture.ListAsync(fx.Options()));

        Assert.Equal(CatalogFixture.Ms(T), entry.LastActivity);
    }

    [Fact]
    public async Task Last_activity_falls_back_to_the_file_write_time_when_both_timestamps_are_missing()
    {
        var fx = CatalogFixture.Create();
        string folder = fx.ExistingFolder("NoTimestamps");
        string file = fx.WriteRecord(folder, lastActivityAt: null, createdAt: null);
        DateTime writeTime = new(2025, 3, 14, 15, 9, 26, 535, DateTimeKind.Utc);
        System.IO.File.SetLastWriteTimeUtc(file, writeTime);

        ProjectEntry entry = Assert.Single(await CatalogFixture.ListAsync(fx.Options()));

        Assert.Equal(new DateTimeOffset(writeTime), entry.LastActivity);
    }
}
