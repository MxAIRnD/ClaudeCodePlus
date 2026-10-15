namespace Feature.ContextBar;

/// <summary>Where the context line reads. Filled by the composition root.</summary>
public sealed class ContextBarOptions
{
    /// <summary>
    /// Root of the desktop app's session store, normally
    /// <c>%APPDATA%\Claude\claude-code-sessions</c>; searched recursively for
    /// <c>local_*.json</c> records, whose <c>cliSessionId</c>, <c>lastFocusedAt</c> and
    /// <c>isArchived</c> say which session the app shows. Null, or a folder that does not exist:
    /// no session is ever known and the line stays hidden.
    /// </summary>
    public string? SessionsRoot { get; init; }

    /// <summary>
    /// The folder the context-bar mod publishes into, normally <c>~\.claude\context-bar</c>
    /// (<c>%CLAUDE_CONFIG_DIR%\context-bar</c> when that variable is set): one
    /// <c>&lt;session id&gt;.json</c> per session. Null: the line stays hidden.
    /// </summary>
    public string? FiguresRoot { get; init; }
}
