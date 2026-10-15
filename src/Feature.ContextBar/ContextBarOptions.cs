namespace Feature.ContextBar;

/// <summary>Where the context line reads and where it reports. Filled by the composition root.</summary>
public sealed class ContextBarOptions
{
    /// <summary>
    /// The folder the context-bar mod publishes into, normally <c>~\.claude\context-bar</c>
    /// (<c>%CLAUDE_CONFIG_DIR%\context-bar</c> when that variable is set): one
    /// <c>&lt;session id&gt;.json</c> per session, named by the desktop app's session id
    /// (<c>local_&lt;uuid&gt;</c>, the one the shown page's URL carries). Null: the line stays
    /// hidden.
    /// </summary>
    public string? FiguresRoot { get; init; }

    /// <summary>
    /// The diagnostics file (<see cref="DiagnosticsLog"/>): a line per change when a Claude
    /// window is in front and the line cannot be drawn (<see cref="FooterLocator.Report"/>).
    /// Null: nothing is written.
    /// </summary>
    public string? DiagnosticsFile { get; init; }
}
