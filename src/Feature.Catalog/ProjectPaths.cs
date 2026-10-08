using System;
using System.IO;

namespace Feature.Catalog;

/// <summary>Path rules shared by the sources and the merge: trimming, merge keys, names, noise.</summary>
internal static class ProjectPaths
{
    private const string ScratchWorkspaceMarker = @"\Claude\scratch-workspaces\";
    private const string WorktreeMarker = @"\.claude\worktrees\";

    /// <summary>Surrounding whitespace and one trailing separator removed; a drive root (<c>C:\</c>) keeps its separator.</summary>
    internal static string Trim(string path) => Path.TrimEndingDirectorySeparator(path.Trim());

    /// <summary>The merge key of a trimmed path: letter case ignored.</summary>
    internal static string Key(string trimmedPath) => trimmedPath.ToUpperInvariant();

    /// <summary>The last segment; the path itself when there is none (a drive root).</summary>
    internal static string NameOf(string trimmedPath)
    {
        string name = Path.GetFileName(trimmedPath);
        return name.Length == 0 ? trimmedPath : name;
    }

    internal static bool IsScratchWorkspace(string path) =>
        path.Contains(ScratchWorkspaceMarker, StringComparison.OrdinalIgnoreCase);

    internal static bool IsWorktree(string path) =>
        path.Contains(WorktreeMarker, StringComparison.OrdinalIgnoreCase);
}
