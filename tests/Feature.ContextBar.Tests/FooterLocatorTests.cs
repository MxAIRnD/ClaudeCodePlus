using System.Collections.Generic;
using System.Drawing;
using Xunit;

namespace Feature.ContextBar.Tests;

/// <summary>
/// Black-box behavior of <see cref="FooterLocator"/> over in-memory element snapshots: the slot,
/// the shown session's id from the page URL, the app version, and the diagnostics reports.
/// </summary>
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
    private static readonly Rectangle Page = new(0, 0, 2561, 1548);

    // The two page documents of the window on the same build: the shell page (no name, a file
    // URL naming the packaged app's version) and the session page (the title, the session URL).
    private const string SessionId = "local_8804b5b6-7c1d-4e2f-9a3b-0c1d2e3f4a5b";
    private const string SessionUrl = "https://claude.ai/epitaxy/" + SessionId;
    private const string ShellUrl = "file:///C:/Program%20Files/WindowsApps/Claude_2.37337.1.0_x64__pzs8sxrjxfjjc/app/resources/app.asar/.vite/renderer/main_window/index.html";

    private static FooterElement Button(string name, Rectangle bounds, bool isOffscreen = false) =>
        new(name, FooterElementKind.Button, bounds, isOffscreen);

    private static FooterElement Other(string name, Rectangle bounds) => new(name, FooterElementKind.Other, bounds, false);

    private static FooterElement Document(string name, string url) => new(name, FooterElementKind.Document, Page, false, url);

    private static FooterElement ShellDocument() => Document(string.Empty, ShellUrl);

    private static FooterElement SessionDocument(string url = SessionUrl) => Document("Card channel line and now-playing icon - Claude Code", url);

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

    private static List<FooterElement> ShownSession()
    {
        List<FooterElement> elements = RunningSession();
        elements.Add(Other("Claude", Page));
        elements.Add(ShellDocument());
        elements.Add(SessionDocument());
        return elements;
    }

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

    // --- the shown session ---

    [Fact]
    public void The_shown_session_id_is_the_local_id_of_the_session_documents_URL()
    {
        Assert.Equal(SessionId, FooterLocator.ShownSessionId(ShownSession()));
        // the documents' order does not matter, nor the page title
        Assert.Equal(SessionId, FooterLocator.ShownSessionId(new[] { Document("Settings - Claude", SessionUrl), ShellDocument() }));
    }

    [Fact]
    public void Only_a_session_page_URL_on_a_document_gives_an_id()
    {
        var shellOnly = new List<FooterElement>(RunningSession()) { ShellDocument() };
        var noDocument = new List<FooterElement>(RunningSession()) { Other("Claude", Page) };
        var newSession = new List<FooterElement> { ShellDocument(), Document("Claude Code", "https://claude.ai/epitaxy/new") };
        var otherRoutes = new List<FooterElement>
        {
            Document("Claude Code", "https://claude.ai/code/" + SessionId),
            Document("Claude Code", "claude://claude.ai/epitaxy/" + SessionId),
            Document("Claude Code", SessionUrl + "/settings"),
            Document("Claude Code", "https://claude.ai/epitaxy/local_8804b5b6"),
            Document("Claude Code", "https://claude.ai/epitaxy/LOCAL_8804B5B6-7C1D-4E2F-9A3B-0C1D2E3F4A5B"),
        };
        var notADocument = new List<FooterElement> { new("Title - Claude Code", FooterElementKind.Button, Page, false, SessionUrl) };

        Assert.Null(FooterLocator.ShownSessionId(shellOnly));
        Assert.Null(FooterLocator.ShownSessionId(noDocument));
        Assert.Null(FooterLocator.ShownSessionId(newSession));
        Assert.Null(FooterLocator.ShownSessionId(otherRoutes));
        Assert.Null(FooterLocator.ShownSessionId(notADocument));
        Assert.Null(FooterLocator.ShownSessionId(new List<FooterElement>()));
    }

    [Fact]
    public void The_app_version_is_read_from_the_shell_documents_file_URL()
    {
        Assert.Equal("2.37337.1.0", FooterLocator.AppVersion(ShownSession()));
        Assert.Null(FooterLocator.AppVersion(new[] { SessionDocument() }));
        Assert.Null(FooterLocator.AppVersion(new[] { Document(string.Empty, "file:///C:/Users/x/AppData/Local/Programs/claude/resources/app.asar/index.html") }));
        Assert.Null(FooterLocator.AppVersion(RunningSession()));
    }

    // --- the diagnostics reports ---

    [Fact]
    public void A_session_shown_without_a_footer_slot_is_reported_with_the_model_chips_row()
    {
        List<FooterElement> elements = ShownSession();
        elements[4] = Button("Permissions: Bypass permissions", ModeChip); // a renamed mode chip: the row reads as the composer

        string? report = FooterLocator.Report(elements, FooterLocator.FindSlot(elements), FooterLocator.ShownSessionId(elements), false);

        Assert.NotNull(report);
        Assert.StartsWith("app 2.37337.1.0: a session is shown but no footer slot was found; buttons on the model chip's row: ", report);
        Assert.Contains("'Permissions: Bypass permissions' 770,1493 219x40 | 'Model: Fable 5.1' 1773,1493 104x40 | 'Effort: Max' 1883,1493 64x40", report);
        Assert.Contains("'Usage: 57% of session limit, Resets in 2…' 1954,1493 40x40", report); // names cut to 40 characters
        Assert.DoesNotContain("Something above", report);
        Assert.DoesNotContain(SessionId, report);
    }

    [Fact]
    public void A_session_shown_without_any_model_chip_is_reported_with_the_lowest_row()
    {
        List<FooterElement> elements = ShownSession();
        elements.RemoveAll(e => e.Name.StartsWith("Model:", System.StringComparison.Ordinal));
        elements.Add(Button("Something above", new Rectangle(1500, 1400, 100, 40)));

        string? report = FooterLocator.Report(elements, null, SessionId, false);

        Assert.NotNull(report);
        Assert.Contains("'Bypass permissions' 770,1493 219x40 | 'Effort: Max' 1883,1493 64x40", report);
        Assert.DoesNotContain("Something above", report);
    }

    [Fact]
    public void A_footer_slot_without_a_session_URL_is_reported_with_the_documents()
    {
        var elements = new List<FooterElement>(RunningSession()) { ShellDocument(), Document("Claude Code", "https://claude.ai/epitaxy/new") };

        string? report = FooterLocator.Report(elements, FooterLocator.FindSlot(elements), null, false);

        Assert.NotNull(report);
        Assert.StartsWith("app 2.37337.1.0: a footer slot but no session page URL; documents: [name 0 chars, url file:///C:/Program%20Files/WindowsApps/Claude_2.37337.1.0_x64__pzs8sxrjxfjjc/app/resources/app.asar/.vite/renderer/main_…] [name 11 chars, url https://claude.ai/epitaxy/new]", report);
    }

    [Fact]
    public void A_shown_session_without_a_figures_file_is_reported_with_its_id_cut_short()
    {
        List<FooterElement> elements = ShownSession();

        string? report = FooterLocator.Report(elements, FooterLocator.FindSlot(elements), SessionId, false);

        Assert.Equal("app 2.37337.1.0: no figures file for the shown session local_8804b5b6… (the context-bar mod is not publishing for it)", report);
    }

    [Fact]
    public void Nothing_is_reported_when_the_line_can_be_drawn_or_Claude_is_not_in_front_or_no_session_and_no_footer_show()
    {
        List<FooterElement> shown = ShownSession();
        Rectangle? slot = FooterLocator.FindSlot(shown);
        var settings = new List<FooterElement> { ShellDocument(), Document("Settings - Claude", "https://claude.ai/settings") };
        List<FooterElement> unknownBuild = ShownSession();
        unknownBuild.RemoveAll(e => e.Name.Length == 0 && e.Kind == FooterElementKind.Document);

        Assert.Null(FooterLocator.Report(new List<FooterElement>(), null, null, false));
        Assert.Null(FooterLocator.Report(shown, slot, SessionId, true));
        Assert.Null(FooterLocator.Report(settings, null, null, false));
        Assert.StartsWith("app unknown: ", FooterLocator.Report(unknownBuild, slot, SessionId, false));
    }

    [Fact]
    public void Null_elements_throw()
    {
        Assert.Throws<System.ArgumentNullException>(() => FooterLocator.FindSlot(null!));
        Assert.Throws<System.ArgumentNullException>(() => FooterLocator.ShownSessionId(null!));
        Assert.Throws<System.ArgumentNullException>(() => FooterLocator.AppVersion(null!));
        Assert.Throws<System.ArgumentNullException>(() => FooterLocator.Report(null!, null, null, false));
    }
}
