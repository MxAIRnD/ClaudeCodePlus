using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Feature.Catalog;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Tests.Flows;

/// <summary>
/// Flow "Catalog refresh": every call lists the sources afresh and sees what was added or
/// changed since the previous call — even through the one singleton instance the composed
/// container hands out, whose memo of unchanged records must not get in the way.
/// </summary>
public sealed class CatalogRefreshFlowTests
{
    [Fact]
    public async Task Second_list_on_the_same_catalog_sees_a_record_for_a_new_folder_added_between()
    {
        var fixture = FlowFixture.Create();
        string old = fixture.Folder("Old");
        fixture.Record(old, FlowFixture.BaseMs - 60_000);
        using ServiceProvider provider = fixture.Provider();
        IProjectCatalog catalog = provider.GetRequiredService<IProjectCatalog>();

        IReadOnlyList<ProjectEntry> before = await catalog.ListAsync();
        string fresh = fixture.Folder("Fresh");
        fixture.Record(fresh, FlowFixture.BaseMs, account: "acct-3", org: "org-new");
        IReadOnlyList<ProjectEntry> after = await catalog.ListAsync();

        Assert.Equal(new[] { old }, before.Select(e => e.Path));
        Assert.Equal(new[] { fresh, old }, after.Select(e => e.Path));
    }

    [Fact]
    public async Task Second_list_reranks_when_a_newer_record_for_an_older_folder_is_added_between()
    {
        var fixture = FlowFixture.Create();
        string a = fixture.Folder("A");
        string b = fixture.Folder("B");
        fixture.Record(a, FlowFixture.BaseMs - 120_000);
        fixture.Record(b, FlowFixture.BaseMs - 60_000);
        using ServiceProvider provider = fixture.Provider();

        IReadOnlyList<ProjectEntry> before = await provider.GetRequiredService<IProjectCatalog>().ListAsync();
        fixture.Record(a, FlowFixture.BaseMs);
        IReadOnlyList<ProjectEntry> after = await provider.GetRequiredService<IProjectCatalog>().ListAsync();

        Assert.Equal(new[] { b, a }, before.Select(e => e.Path));
        Assert.Equal(new[] { a, b }, after.Select(e => e.Path));
        Assert.Equal(1, before.Single(e => e.Path == a).SessionCount);
        Assert.Equal(2, after.Single(e => e.Path == a).SessionCount);
    }

    [Fact]
    public async Task Second_list_sees_a_store_root_that_did_not_exist_at_the_first_call()
    {
        var fixture = FlowFixture.Create();
        string lateRoot = Path.Combine(fixture.Root, "late-store");
        var options = new CatalogOptions { DesktopSessionsRoot = lateRoot };
        using ServiceProvider provider = FlowFixture.Compose(options).BuildServiceProvider();
        IProjectCatalog catalog = provider.GetRequiredService<IProjectCatalog>();

        IReadOnlyList<ProjectEntry> before = await catalog.ListAsync();
        string folder = fixture.Folder("Later");
        string dir = Path.Combine(lateRoot, "acct", "org");
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, "local_00000000-0000-0000-0000-00000000abcd.json"),
            JsonSerializer.Serialize(new
            {
                sessionId = "local_a1",
                originCwd = folder,
                lastActivityAt = FlowFixture.BaseMs,
                createdAt = FlowFixture.BaseMs - 6_000_000,
                title = "made up",
            }));
        IReadOnlyList<ProjectEntry> after = await catalog.ListAsync();

        Assert.Empty(before);
        Assert.Equal(folder, Assert.Single(after).Path);
    }

    [Fact]
    public async Task Second_list_sees_a_folder_created_after_the_first_call_once_it_exists()
    {
        var fixture = FlowFixture.Create();
        string pending = fixture.MissingFolder("Pending");
        fixture.Record(pending, FlowFixture.BaseMs);
        using ServiceProvider provider = fixture.Provider();
        IProjectCatalog catalog = provider.GetRequiredService<IProjectCatalog>();

        IReadOnlyList<ProjectEntry> before = await catalog.ListAsync();
        Directory.CreateDirectory(pending);
        IReadOnlyList<ProjectEntry> after = await catalog.ListAsync();

        Assert.Empty(before);
        ProjectEntry entry = Assert.Single(after);
        Assert.Equal(pending, entry.Path);
        Assert.True(entry.FolderExists);
    }
}
