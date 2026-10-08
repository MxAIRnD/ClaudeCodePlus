using System.Drawing;

namespace Feature.ChipButton;

/// <summary>The kinds of UI Automation element the locator tells apart.</summary>
public enum ChipElementKind
{
    /// <summary>Any other control type.</summary>
    Other,

    /// <summary>A button (the folder pills, "Add another folder").</summary>
    Button,

    /// <summary>A check box (the "worktree" box).</summary>
    CheckBox,

    /// <summary>A plain text element (the "worktree" label).</summary>
    Text,
}

/// <summary>
/// One element of the Claude window as UI Automation reports it: its accessible name, its kind,
/// its bounds in physical screen pixels, and whether UI Automation calls it off screen.
/// </summary>
/// <param name="Name">The accessible name, as reported (never null; empty when none).</param>
/// <param name="Kind">The control type, reduced to the kinds the locator uses.</param>
/// <param name="Bounds">Bounding rectangle in physical screen pixels; empty when not drawn.</param>
/// <param name="IsOffscreen">UI Automation's <c>IsOffscreen</c> (scrolled out or hidden).</param>
public readonly record struct ChipElement(string Name, ChipElementKind Kind, Rectangle Bounds, bool IsOffscreen);
