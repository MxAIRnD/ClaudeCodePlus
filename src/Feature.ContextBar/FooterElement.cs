using System.Drawing;

namespace Feature.ContextBar;

/// <summary>The kinds of UI Automation element the footer locator tells apart.</summary>
public enum FooterElementKind
{
    /// <summary>Any other control type.</summary>
    Other,

    /// <summary>A button (the prompt footer's chips).</summary>
    Button,

    /// <summary>A document (one of the app's pages: its value is the page's URL, its name the page's title).</summary>
    Document,
}

/// <summary>
/// One element of the Claude window as UI Automation reports it: its accessible name, its kind,
/// its bounds in physical screen pixels, whether UI Automation calls it off screen, and its value
/// (a document's page URL; empty for an element without one).
/// </summary>
/// <param name="Name">The accessible name, as reported (never null; empty when none).</param>
/// <param name="Kind">The control type, reduced to the kinds the locator uses.</param>
/// <param name="Bounds">Bounding rectangle in physical screen pixels; empty when not drawn.</param>
/// <param name="IsOffscreen">UI Automation's <c>IsOffscreen</c> (scrolled out or hidden).</param>
/// <param name="Value">The <c>ValuePattern</c> value: a document's page URL; empty when the element has none.</param>
public readonly record struct FooterElement(string Name, FooterElementKind Kind, Rectangle Bounds, bool IsOffscreen, string Value = "");
