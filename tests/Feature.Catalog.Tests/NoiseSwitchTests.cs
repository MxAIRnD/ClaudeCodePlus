using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Feature.Catalog;
using Xunit;

namespace Feature.Catalog.Tests;

/// <summary>The three noise switches of CatalogOptions: each hidden by default, shown when on.</summary>
public sealed class NoiseSwitchTests
{
    private const long T = 1791446823792;

    [Fact]
    public void Every_noise_switch_defaults_to_hidden()
    {
        var options = new CatalogOptions();

        Assert.False(options.IncludeScratchWorkspaces);
        Assert.False(options.IncludeWorktrees);
        Assert.False(options.IncludeMissingFolders);
    }

    [Fact]
    public async Task Scratch_workspaces_are_hidden_by_default()
    {
        var fx = CatalogFixture.Create();
        string keep = fx.ExistingFolder("RealProject");
        string scratch = fx.ExistingFolder("Roaming", "Claude", "scratch-workspaces", "made-up-ws", "scratch-0001");
        fx.WriteRecord(keep, T - 1_000);
        fx.WriteRecord(scratch, T);

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(fx.Options());

        Assert.Equal(new[] { keep }, list.Select(e => e.Path));
    }

    [Fact]
    public async Task Scratch_workspaces_are_listed_when_switched_on()
    {
        var fx = CatalogFixture.Create();
        string keep = fx.ExistingFolder("RealProject");
        string scratch = fx.ExistingFolder("Roaming", "Claude", "scratch-workspaces", "made-up-ws", "scratch-0001");
        fx.WriteRecord(keep, T - 1_000);
        fx.WriteRecord(scratch, T);
        CatalogOptions options = new() { DesktopSessionsRoot = fx.SessionsRoot, IncludeScratchWorkspaces = true };

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(options);

        Assert.Equal(new[] { scratch, keep }, list.Select(e => e.Path));
    }

    [Fact]
    public async Task Session_worktrees_are_hidden_by_default()
    {
        var fx = CatalogFixture.Create();
        string keep = fx.ExistingFolder("RealProject");
        string worktree = fx.ExistingFolder("RealProject", ".claude", "worktrees", "made-up-tree");
        fx.WriteRecord(keep, T - 1_000);
        fx.WriteRecord(worktree, T);

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(fx.Options());

        Assert.Equal(new[] { keep }, list.Select(e => e.Path));
    }

    [Fact]
    public async Task Session_worktrees_are_listed_when_switched_on()
    {
        var fx = CatalogFixture.Create();
        string keep = fx.ExistingFolder("RealProject");
        string worktree = fx.ExistingFolder("RealProject", ".claude", "worktrees", "made-up-tree");
        fx.WriteRecord(keep, T - 1_000);
        fx.WriteRecord(worktree, T);
        CatalogOptions options = new() { DesktopSessionsRoot = fx.SessionsRoot, IncludeWorktrees = true };

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(options);

        Assert.Equal(new[] { worktree, keep }, list.Select(e => e.Path));
    }

    [Fact]
    public async Task Missing_folders_are_hidden_by_default()
    {
        var fx = CatalogFixture.Create();
        string keep = fx.ExistingFolder("StillHere");
        string missing = fx.MissingFolder("LongGone");
        fx.WriteRecord(keep, T - 1_000);
        fx.WriteRecord(missing, T);

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(fx.Options());

        Assert.Equal(new[] { keep }, list.Select(e => e.Path));
    }

    [Fact]
    public async Task Missing_folders_are_listed_when_switched_on_and_flagged_as_not_existing()
    {
        var fx = CatalogFixture.Create();
        string keep = fx.ExistingFolder("StillHere");
        string missing = fx.MissingFolder("LongGone");
        fx.WriteRecord(keep, T - 1_000);
        fx.WriteRecord(missing, T);
        CatalogOptions options = new() { DesktopSessionsRoot = fx.SessionsRoot, IncludeMissingFolders = true };

        IReadOnlyList<ProjectEntry> list = await CatalogFixture.ListAsync(options);

        Assert.Equal(new[] { missing, keep }, list.Select(e => e.Path));
        Assert.False(list[0].FolderExists);
        Assert.True(list[1].FolderExists);
    }
}
