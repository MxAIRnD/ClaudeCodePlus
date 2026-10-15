using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Feature.ContextBar;

/// <summary>
/// The context line in the Claude app's prompt footer: a borderless, always-on-top, click-through
/// layered window (per-pixel alpha: only the line and the figures show) placed between the
/// permission-mode chip and the model chip of a running session (see <see cref="FooterLocator"/>).
/// Once <see cref="StartWatching"/> is called it polls the foreground window every 400 ms: shown
/// with the figures of the session the app shows (its id read from the page's URL by
/// <see cref="FooterLocator.ShownSessionId"/>, its figures from that session's own file under
/// <see cref="ContextBarOptions.FiguresRoot"/>, see <see cref="ReadFigures"/>) while a Claude
/// window with such a row is in front and that session has published figures that are not
/// hidden; hidden otherwise, never with another session's figures. While a Claude window is in
/// front and the line cannot be drawn, what is missing goes to the diagnostics file
/// (<see cref="ContextBarOptions.DiagnosticsFile"/>), once per change. It never takes the focus
/// or a click, and it does not change the Claude app.
/// </summary>
public sealed class ContextBarForm : Form
{
    private const int WsExToolWindow = 0x00000080;
    private const int WsExTopmost = 0x00000008;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;

    private readonly Timer _timer;
    private readonly DiagnosticsLog _log;
    private readonly string? _figuresRoot;
    private bool _probing;
    private Rectangle _slot;
    private ContextFigures? _figures;

    /// <summary>Creates the window, hidden and not yet watching. Throws <see cref="ArgumentNullException"/> for null.</summary>
    public ContextBarForm(ContextBarOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _figuresRoot = options.FiguresRoot;
        _log = new DiagnosticsLog(options.DiagnosticsFile);

        Text = "Claude Code Plus — context line";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;

        _timer = new Timer { Interval = 400 };
        _timer.Tick += async (_, _) => await TickAsync();
    }

    /// <inheritdoc/>
    protected override bool ShowWithoutActivation => true;

    /// <inheritdoc/>
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WsExToolWindow | WsExTopmost | WsExNoActivate | WsExLayered | WsExTransparent;
            return cp;
        }
    }

    /// <summary>
    /// The figures the session <paramref name="sessionId"/> (as <see cref="FooterLocator.ShownSessionId"/>
    /// answers it) has published under <paramref name="figuresRoot"/>: the file
    /// <c>&lt;figuresRoot&gt;\&lt;sessionId&gt;.json</c>, read by <see cref="FiguresFile.TryRead"/>.
    /// Null when either is null (no session shown, or nowhere to read), or when the file is
    /// missing, unreadable or not a figures file (the mod has not published for that session).
    /// Never another session's figures.
    /// </summary>
    public static ContextFigures? ReadFigures(string? figuresRoot, string? sessionId) =>
        figuresRoot is null || sessionId is null ? null : FiguresFile.TryRead(Path.Combine(figuresRoot, sessionId + ".json"));

    /// <summary>Starts polling the foreground window; call once, on the UI thread.</summary>
    public void StartWatching() => _timer.Start();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task TickAsync()
    {
        if (_probing || IsDisposed)
        {
            return;
        }

        _probing = true;
        try
        {
            IReadOnlyList<FooterElement> elements = await Task.Run(FooterProbe.Read);
            if (IsDisposed)
            {
                return;
            }

            Rectangle? slot = FooterLocator.FindSlot(elements);
            string? sessionId = FooterLocator.ShownSessionId(elements);
            ContextFigures? figures = slot is null ? null : ReadFigures(_figuresRoot, sessionId);
            _log.Note(FooterLocator.Report(elements, slot, sessionId, figures is not null));
            if (slot is null || figures is null || figures.IsHidden)
            {
                if (Visible)
                {
                    Hide();
                }

                return;
            }

            if (slot.Value != _slot || figures != _figures)
            {
                _slot = slot.Value;
                _figures = figures;
                PaintSlot(slot.Value, figures);
            }

            if (!Visible)
            {
                Show();
            }
        }
        finally
        {
            _probing = false;
        }
    }

    private void PaintSlot(Rectangle slot, ContextFigures figures)
    {
        if (Bounds != slot)
        {
            Bounds = slot;
        }

        if (!IsHandleCreated)
        {
            CreateHandle();
        }

        using var frame = new Bitmap(slot.Width, slot.Height, PixelFormat.Format32bppArgb);
        BarPainter.Render(frame, figures);
        LayeredWindow.Update(Handle, frame, slot.Location);
    }
}
