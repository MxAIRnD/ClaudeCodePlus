using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Feature.Catalog;

/// <summary>
/// The scan behind <see cref="IProjectCatalog"/>: sightings from every enabled source, merged
/// per folder, noise removed per <see cref="CatalogOptions"/>, ordered most recent first. The
/// desktop store's memo (see <see cref="DesktopSessionStore"/>) lives with this instance.
/// </summary>
internal sealed class ProjectCatalog : IProjectCatalog
{
    private readonly CatalogOptions _options;
    private readonly DesktopSessionStore? _desktop;

    public ProjectCatalog(CatalogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _desktop = string.IsNullOrWhiteSpace(options.DesktopSessionsRoot)
            ? null
            : new DesktopSessionStore(options.DesktopSessionsRoot);
    }

    public Task<IReadOnlyList<ProjectEntry>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(cancellationToken), cancellationToken);

    private IReadOnlyList<ProjectEntry> Scan(CancellationToken cancellationToken)
    {
        var sightings = new List<Sighting>();
        if (_desktop is not null)
        {
            sightings.AddRange(_desktop.Read(cancellationToken));
        }

        if (!string.IsNullOrWhiteSpace(_options.TerminalProjectsRoot))
        {
            sightings.AddRange(TerminalProjectsFolder.Read(_options.TerminalProjectsRoot, cancellationToken));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Merge(sightings, _options);
    }

    private static IReadOnlyList<ProjectEntry> Merge(List<Sighting> sightings, CatalogOptions options)
    {
        var folders = new Dictionary<string, Folder>(StringComparer.Ordinal);
        foreach (Sighting sighting in sightings)
        {
            string path = ProjectPaths.Trim(sighting.Path);
            if (path.Length == 0)
            {
                continue;
            }

            string key = ProjectPaths.Key(path);
            if (!folders.TryGetValue(key, out Folder? folder))
            {
                folder = new Folder(path, sighting.When);
                folders.Add(key, folder);
            }

            folder.Add(path, sighting);
        }

        var entries = new List<ProjectEntry>(folders.Count);
        foreach (Folder folder in folders.Values)
        {
            if (!options.IncludeScratchWorkspaces && ProjectPaths.IsScratchWorkspace(folder.Path))
            {
                continue;
            }

            if (!options.IncludeWorktrees && ProjectPaths.IsWorktree(folder.Path))
            {
                continue;
            }

            bool exists = Directory.Exists(folder.Path);
            if (!options.IncludeMissingFolders && !exists)
            {
                continue;
            }

            entries.Add(new ProjectEntry(
                folder.Path,
                ProjectPaths.NameOf(folder.Path),
                folder.LastActivity,
                folder.Sources,
                folder.Count,
                exists));
        }

        entries.Sort(static (a, b) =>
        {
            int byActivity = b.LastActivity.CompareTo(a.LastActivity);
            return byActivity != 0
                ? byActivity
                : string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
        });
        return entries;
    }

    /// <summary>The running merge of one folder: newest record's spelling and time, all sources, the count.</summary>
    private sealed class Folder
    {
        public Folder(string path, DateTimeOffset firstSeen)
        {
            Path = path;
            LastActivity = firstSeen;
        }

        public string Path { get; private set; }

        public DateTimeOffset LastActivity { get; private set; }

        public ProjectSources Sources { get; private set; }

        public int Count { get; private set; }

        public void Add(string path, Sighting sighting)
        {
            Count++;
            Sources |= sighting.Source;
            if (sighting.When >= LastActivity)
            {
                LastActivity = sighting.When;
                Path = path;
            }
        }
    }
}
