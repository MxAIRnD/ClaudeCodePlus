using System.Collections.Generic;
using System.Drawing;
using Xunit;

namespace Feature.ContextBar.Tests;

/// <summary>Black-box behavior of <see cref="FooterLocator.FindSlot"/> over in-memory element snapshots.</summary>
public sealed class FooterLocatorTests
{
    // A running session's prompt footer as UI Automation reported it (physical px, app build
    // 2.37337.1, 2026-10-15), names only, no session content. The sidebar's own footer buttons
    // sit on the same row at the far left.
    private static readonly Rectangle ModeChip = new(770, 1493, 219, 40);
    private static readonly Rectangle ModelChip = new(1773, 1493, 104, 40);
    private static readonly Rectangle EffortChip = new(1883, 1493, 64, 40);
    private static readonly Rectangle UsageChip = new(1954, 1493, 40, 40);
    private static readonly Rectangle SendFeedback = new(633, 1492, 40, 40);
    private static readonly Rectangle UserMenu = new(13, 1489, 217, 46);

    private static FooterElement Button(string name, Rectangle bounds, bool isOffscreen = false) =>
        new(name, FooterElementKind.Button, bounds, isOffscreen);

    private static FooterElement Other(string name, Rectangle bounds) => new(name, FooterElementKind.Other, bounds, false);

    private static List<FooterElement> RunningSession(Rectangle? model = null) => new()
    {
        Button("Idle Claude project selection button", new Rectangle(13, 1471, 659, 43)),
        Button("Maks Max", UserMenu),
        Button("Send feedback", SendFeedback),
        Button("Add", new Rectangle(745, 1413, 46, 47)),
        Button("Bypass permissions", ModeChip),
        Button("Model: Fable 5.1", model ?? ModelChip),
        Button("Effort: Max", EffortChip),
        Button("Usage: 57% of session limit, Resets in 2 hr 22 min", UsageChip),
    };

    [Fact]
    public void Running_session_slot_runs_from_the_mode_chip_to_the_model_chip_with_half_a_height_of_gap()
    {
        Rectangle? slot = FooterLocator.FindSlot(RunningSession());

        Assert.Equal(new Rectangle(989 + 20, 1493, 1773 - 20 - (989 + 20), 40), slot);
    }

    [Fact]
    public void The_sidebar_buttons_on_the_same_row_are_not_the_anchor()
    {
        List<FooterElement> elements = RunningSession();
        elements.RemoveAll(e => e.Name == "Bypass permissions");
        // without the mode chip the nearest button before the model chip is the sidebar's
        Rectangle? slot = FooterLocator.FindSlot(elements);

        Assert.NotNull(slot);
        Assert.Equal(SendFeedback.Right + 20, slot!.Value.Left);
    }

    [Fact]
    public void Other_modes_anchor_the_same_way()
    {
        List<FooterElement> elements = RunningSession();
        elements[4] = Button("Plan mode", new Rectangle(770, 1493, 150, 40));

        Assert.Equal(new Rectangle(920 + 20, 1493, 1773 - 20 - 940, 40), FooterLocator.FindSlot(elements));
    }

    [Fact]
    public void New_session_composer_is_left_alone()
    {
        var composer = new List<FooterElement>
        {
            Button("Environment: AI", new Rectangle(1012, 1493, 75, 40)),
            Button("Permissions: Bypass permissions", new Rectangle(1092, 1493, 219, 40)),
            Button("Model: Fable 5.1", new Rectangle(2005, 1493, 103, 40)),
            Button("Effort: Extra", new Rectangle(2114, 1493, 74, 40)),
        };

        Assert.Null(FooterLocator.FindSlot(composer));
    }

    [Fact]
    public void No_model_chip_means_no_slot()
    {
        List<FooterElement> elements = RunningSession();
        elements.RemoveAll(e => e.Name.StartsWith("Model:", System.StringComparison.Ordinal));

        Assert.Null(FooterLocator.FindSlot(elements));
    }

    [Fact]
    public void An_offscreen_or_empty_model_chip_does_not_count()
    {
        List<FooterElement> offscreen = RunningSession();
        offscreen[5] = Button("Model: Fable 5.1", ModelChip, isOffscreen: true);
        List<FooterElement> empty = RunningSession(Rectangle.Empty);

        Assert.Null(FooterLocator.FindSlot(offscreen));
        Assert.Null(FooterLocator.FindSlot(empty));
    }

    [Fact]
    public void A_slot_narrower_than_three_chip_heights_is_null()
    {
        // the model chip right after the mode chip: 989 + 20 .. 1120 - 20 = 91 px < 120
        Rectangle? slot = FooterLocator.FindSlot(RunningSession(new Rectangle(1120, 1493, 104, 40)));

        Assert.Null(slot);
    }

    [Fact]
    public void Only_buttons_on_the_model_chips_row_anchor_and_the_lowest_model_chip_wins()
    {
        List<FooterElement> elements = RunningSession();
        elements.Add(Other("Bypass permissions", new Rectangle(1500, 1493, 100, 40))); // not a button
        elements.Add(Button("Something above", new Rectangle(1500, 1400, 100, 40))); // another row
        elements.Add(Button("Model: Other", new Rectangle(1700, 900, 104, 40))); // higher up: not the footer

        Assert.Equal(new Rectangle(1009, 1493, 744, 40), FooterLocator.FindSlot(elements));
    }

    [Fact]
    public void The_shown_session_title_is_the_page_title_less_its_suffix()
    {
        var elements = new List<FooterElement>(RunningSession())
        {
            new("Claude", FooterElementKind.Other, new Rectangle(0, 0, 2561, 1548), false),
            new(string.Empty, FooterElementKind.Document, new Rectangle(0, 0, 2561, 1548), false),
            new("Card channel line and now-playing icon - Claude Code", FooterElementKind.Document, new Rectangle(0, 0, 2561, 1548), false),
        };

        Assert.Equal("Card channel line and now-playing icon", FooterLocator.ShownSessionTitle(elements));
    }

    [Fact]
    public void No_titled_document_means_no_shown_title()
    {
        var bare = new List<FooterElement>(RunningSession()) { new(string.Empty, FooterElementKind.Document, Rectangle.Empty, false) };
        var other = new List<FooterElement> { new("Settings - Claude", FooterElementKind.Document, Rectangle.Empty, false) };
        var empty = new List<FooterElement> { new("  - Claude Code", FooterElementKind.Document, Rectangle.Empty, false) };
        var notADocument = new List<FooterElement> { new("Title - Claude Code", FooterElementKind.Button, Rectangle.Empty, false) };

        Assert.Null(FooterLocator.ShownSessionTitle(bare));
        Assert.Null(FooterLocator.ShownSessionTitle(other));
        Assert.Null(FooterLocator.ShownSessionTitle(empty));
        Assert.Null(FooterLocator.ShownSessionTitle(notADocument));
    }

    [Fact]
    public void Null_elements_throw()
    {
        Assert.Throws<System.ArgumentNullException>(() => FooterLocator.FindSlot(null!));
        Assert.Throws<System.ArgumentNullException>(() => FooterLocator.ShownSessionTitle(null!));
    }
}
