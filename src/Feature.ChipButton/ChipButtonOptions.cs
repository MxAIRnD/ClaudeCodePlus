namespace Feature.ChipButton;

/// <summary>Where the button reports. Filled by the composition root.</summary>
public sealed class ChipButtonOptions
{
    /// <summary>
    /// The diagnostics file (<see cref="DiagnosticsLog"/>): a line per change when a Claude
    /// window with a composer is in front and no anchor is found (<see cref="ChipRowLocator.Report"/>).
    /// Null: nothing is written.
    /// </summary>
    public string? DiagnosticsFile { get; init; }
}
