namespace Feature.Catalog;

/// <summary>
/// Where the catalog reads and what it hides. Filled by the composition root. A null root
/// switches that source off; every noise switch defaults to hidden.
/// </summary>
public sealed class CatalogOptions
{
    /// <summary>
    /// Root of the desktop app's session store, normally
    /// <c>%APPDATA%\Claude\claude-code-sessions</c>; searched recursively for
    /// <c>local_*.json</c>. A record is read only as far as its <c>originCwd</c> and
    /// <c>lastActivityAt</c> (a record cut off after them still counts), and read again only
    /// when its size or last write time changed. Null, or a folder that does not exist: the
    /// source contributes nothing.
    /// </summary>
    public string? DesktopSessionsRoot { get; init; }

    /// <summary>
    /// Root of the Claude Code projects folder, normally <c>~\.claude\projects</c>
    /// (<c>%CLAUDE_CONFIG_DIR%\projects</c> when that variable is set): one subfolder per
    /// project holding one <c>*.jsonl</c> transcript per session at its top level; the folder
    /// path is the first <c>cwd</c> found in the first 50 lines of those transcripts, smallest
    /// first (never the encoded folder name), and is shared by every transcript of that project
    /// folder: each counts as one session dated by its last write, even one without a
    /// <c>cwd</c>. A project folder where no transcript yields a <c>cwd</c> is skipped. Null, or
    /// a folder that does not exist: the source contributes nothing.
    /// </summary>
    public string? TerminalProjectsRoot { get; init; }

    /// <summary>
    /// Keep desktop scratch workspaces (paths containing <c>\Claude\scratch-workspaces\</c>).
    /// Default false: hidden.
    /// </summary>
    public bool IncludeScratchWorkspaces { get; init; }

    /// <summary>
    /// Keep session worktrees (paths containing <c>\.claude\worktrees\</c>). Default false: hidden.
    /// </summary>
    public bool IncludeWorktrees { get; init; }

    /// <summary>Keep folders that no longer exist on disk. Default false: hidden.</summary>
    public bool IncludeMissingFolders { get; init; }
}
