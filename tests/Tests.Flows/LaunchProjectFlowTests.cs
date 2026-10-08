using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Feature.Catalog;
using Feature.DeepLink;
using Feature.Picker;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Tests.Flows;

/// <summary>
/// Flow "Launch a project" through the composed surfaces:
/// synthetic store → <see cref="IProjectCatalog.ListAsync"/> → the first row →
/// <see cref="ProjectRows"/> texts → <see cref="ISessionOpener.BuildNewSessionLink"/>.
/// The open step is verified through the pure link builder only: OpenNewSession is never
/// called (a valid path would start a real Claude session), and no window is shown.
/// </summary>
public sealed class LaunchProjectFlowTests
{
    private const string LinkPrefix = "claude://code/new?folder=";

    private static void AssertFixtureIsNotNoise(FlowFixture fixture)
    {
        // The catalog hides scratch workspaces and worktrees by path; a fixture under such a
        // TEMP would hide every folder and fail for the wrong reason.
        Assert.DoesNotContain(@"\Claude\scratch-workspaces\", fixture.Root + @"\", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"\.claude\worktrees\", fixture.Root + @"\", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<IReadOnlyList<ProjectEntry>> ListAsync(ServiceProvider provider) =>
        await provider.GetRequiredService<IProjectCatalog>().ListAsync();

    [Fact]
    public async Task First_entry_is_the_most_recently_used_folder_and_the_rest_follow_newest_first()
    {
        var fixture = FlowFixture.Create();
        AssertFixtureIsNotNoise(fixture);
        string alpha = fixture.Folder("Alpha");
        string beta = fixture.Folder("Beta");
        string gamma = fixture.Folder("Gamma");
        fixture.Record(alpha, FlowFixture.BaseMs - 3_600_000);
        fixture.Record(gamma, FlowFixture.BaseMs);
        fixture.Record(beta, FlowFixture.BaseMs - 60_000, account: "acct-2", org: "org-9");
        using ServiceProvider provider = fixture.Provider();

        IReadOnlyList<ProjectEntry> entries = await ListAsync(provider);

        Assert.Equal(new[] { gamma, beta, alpha }, entries.Select(e => e.Path));
        ProjectEntry first = entries[0];
        Assert.Equal("Gamma", first.Name);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(FlowFixture.BaseMs), first.LastActivity);
        Assert.Equal(TimeSpan.Zero, first.LastActivity.Offset);
        Assert.Equal(ProjectSources.DesktopSessions, first.Sources);
        Assert.Equal(1, first.SessionCount);
        Assert.True(first.FolderExists);
    }

    [Fact]
    public async Task First_entry_builds_exactly_the_folder_link_of_its_escaped_path()
    {
        var fixture = FlowFixture.Create();
        AssertFixtureIsNotNoise(fixture);
        string older = fixture.Folder("Older");
        string newest = fixture.Folder("My Project #1 (draft)");
        fixture.Record(older, FlowFixture.BaseMs - 86_400_000);
        fixture.Record(newest, FlowFixture.BaseMs);
        using ServiceProvider provider = fixture.Provider();
        ISessionOpener opener = provider.GetRequiredService<ISessionOpener>();

        ProjectEntry first = (await ListAsync(provider))[0];
        string link = opener.BuildNewSessionLink(first.Path);

        Assert.Equal(newest, first.Path);
        Assert.Equal(LinkPrefix + Uri.EscapeDataString(newest), link);
        Assert.DoesNotContain(" ", link);
        Assert.DoesNotContain("#", link);
        Assert.DoesNotContain(@"\", link);
        Assert.Equal(newest, Uri.UnescapeDataString(link[LinkPrefix.Length..]));
    }

    [Fact]
    public async Task First_entry_with_a_prompt_appends_the_escaped_prompt_as_q()
    {
        var fixture = FlowFixture.Create();
        AssertFixtureIsNotNoise(fixture);
        string folder = fixture.Folder("Delta");
        fixture.Record(folder, FlowFixture.BaseMs);
        using ServiceProvider provider = fixture.Provider();
        ISessionOpener opener = provider.GetRequiredService<ISessionOpener>();
        const string prompt = "made up: fix A & B? 100% = done #now";

        ProjectEntry first = (await ListAsync(provider))[0];
        string link = opener.BuildNewSessionLink(first.Path, prompt);

        Assert.Equal(LinkPrefix + Uri.EscapeDataString(folder) + "&q=" + Uri.EscapeDataString(prompt), link);
        int q = link.IndexOf("&q=", StringComparison.Ordinal);
        Assert.Equal(link.LastIndexOf('&'), q);
        Assert.Equal(prompt, Uri.UnescapeDataString(link[(q + 3)..]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task First_entry_without_a_prompt_has_no_q_parameter(string? prompt)
    {
        var fixture = FlowFixture.Create();
        AssertFixtureIsNotNoise(fixture);
        string folder = fixture.Folder("Epsilon");
        fixture.Record(folder, FlowFixture.BaseMs);
        using ServiceProvider provider = fixture.Provider();
        ISessionOpener opener = provider.GetRequiredService<ISessionOpener>();

        ProjectEntry first = (await ListAsync(provider))[0];

        Assert.Equal(LinkPrefix + Uri.EscapeDataString(folder), opener.BuildNewSessionLink(first.Path, prompt));
    }

    [Fact]
    public async Task First_entry_row_texts_are_its_age_and_D_for_a_desktop_only_folder()
    {
        var fixture = FlowFixture.Create();
        AssertFixtureIsNotNoise(fixture);
        string folder = fixture.Folder("Zeta");
        fixture.Record(folder, FlowFixture.BaseMs);
        using ServiceProvider provider = fixture.Provider();

        ProjectEntry first = (await ListAsync(provider))[0];

        Assert.Equal("D", ProjectRows.SourcesLabel(first.Sources));
        Assert.Equal("just now", ProjectRows.Age(first.LastActivity, first.LastActivity.AddSeconds(59)));
        Assert.Equal("12 min", ProjectRows.Age(first.LastActivity, first.LastActivity.AddMinutes(12.9)));
        Assert.Equal("3 h", ProjectRows.Age(first.LastActivity, first.LastActivity.AddHours(3.25)));
        Assert.Equal("5 d", ProjectRows.Age(first.LastActivity, first.LastActivity.AddDays(5.5)));
        Assert.Equal(
            first.LastActivity.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ProjectRows.Age(first.LastActivity, first.LastActivity.AddDays(40)));
    }

    [Fact]
    public async Task Noise_newer_than_every_real_folder_never_becomes_the_first_entry()
    {
        var fixture = FlowFixture.Create();
        AssertFixtureIsNotNoise(fixture);
        string real = fixture.Folder("Real");
        string scratch = fixture.Folder(Path.Combine("Claude", "scratch-workspaces", "made-up-ws"));
        string worktree = fixture.Folder(Path.Combine("Repo", ".claude", "worktrees", "made-up-wt"));
        string missing = fixture.MissingFolder("Gone");
        fixture.Record(real, FlowFixture.BaseMs - 600_000);
        fixture.Record(scratch, FlowFixture.BaseMs);
        fixture.Record(worktree, FlowFixture.BaseMs + 1_000);
        fixture.Record(missing, FlowFixture.BaseMs + 2_000);
        using ServiceProvider provider = fixture.Provider();
        ISessionOpener opener = provider.GetRequiredService<ISessionOpener>();

        IReadOnlyList<ProjectEntry> entries = await ListAsync(provider);

        ProjectEntry only = Assert.Single(entries);
        Assert.Equal(real, only.Path);
        Assert.Equal(LinkPrefix + Uri.EscapeDataString(real), opener.BuildNewSessionLink(only.Path));
    }

    [Fact]
    public async Task Several_records_of_one_folder_are_one_row_with_the_newest_activity_and_their_count()
    {
        var fixture = FlowFixture.Create();
        AssertFixtureIsNotNoise(fixture);
        string busy = fixture.Folder("Busy");
        string other = fixture.Folder("Other");
        fixture.Record(busy, FlowFixture.BaseMs - 7_200_000);
        fixture.Record(busy, FlowFixture.BaseMs);
        fixture.Record(busy, FlowFixture.BaseMs - 60_000, account: "acct-2");
        fixture.Record(other, FlowFixture.BaseMs - 1_000);
        using ServiceProvider provider = fixture.Provider();

        IReadOnlyList<ProjectEntry> entries = await ListAsync(provider);

        Assert.Equal(2, entries.Count);
        ProjectEntry first = entries[0];
        Assert.Equal(busy, first.Path);
        Assert.Equal(3, first.SessionCount);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(FlowFixture.BaseMs), first.LastActivity);
        Assert.Equal("D", ProjectRows.SourcesLabel(first.Sources));
    }

    [Fact]
    public async Task Missing_folder_when_included_still_ranks_and_builds_a_link_marked_not_existing()
    {
        var fixture = FlowFixture.Create();
        AssertFixtureIsNotNoise(fixture);
        string real = fixture.Folder("Here");
        string missing = fixture.MissingFolder("NotHere");
        fixture.Record(real, FlowFixture.BaseMs - 1_000);
        fixture.Record(missing, FlowFixture.BaseMs);
        var options = new CatalogOptions { DesktopSessionsRoot = fixture.StoreRoot, IncludeMissingFolders = true };
        using ServiceProvider provider = FlowFixture.Compose(options).BuildServiceProvider();
        ISessionOpener opener = provider.GetRequiredService<ISessionOpener>();

        IReadOnlyList<ProjectEntry> entries = await ListAsync(provider);

        Assert.Equal(new[] { missing, real }, entries.Select(e => e.Path));
        Assert.False(entries[0].FolderExists);
        Assert.True(entries[1].FolderExists);
        // DeepLink does no existence check: the link is built all the same.
        Assert.Equal(LinkPrefix + Uri.EscapeDataString(missing), opener.BuildNewSessionLink(entries[0].Path));
    }

    [Fact]
    public async Task Empty_store_gives_no_rows()
    {
        var fixture = FlowFixture.Create();
        using ServiceProvider provider = fixture.Provider();

        Assert.Empty(await ListAsync(provider));
    }
}
