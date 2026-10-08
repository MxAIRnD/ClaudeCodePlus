using System;

namespace Feature.Catalog;

/// <summary>
/// Which of Claude's local data sources knew a project folder. Flags: a folder seen in both
/// sources carries both bits.
/// </summary>
[Flags]
public enum ProjectSources
{
    /// <summary>Seen in no source. The zero value of the flags; the catalog never returns an entry with it.</summary>
    None = 0,

    /// <summary>
    /// The desktop app's session store (<c>%APPDATA%\Claude\claude-code-sessions</c>): the
    /// <c>originCwd</c> of a session record.
    /// </summary>
    DesktopSessions = 1,

    /// <summary>
    /// The Claude Code projects folder (<c>~\.claude\projects</c>): the <c>cwd</c> of a
    /// transcript record.
    /// </summary>
    TerminalProjects = 2,
}
