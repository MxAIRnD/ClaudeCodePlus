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
/// with the figures of the session the app shows (<see cref="FocusedSessionFinder"/>; the
/// figures from <see cref="ContextBarOptions.FiguresRoot"/>) while a Claude window with such a
/// row is in front and that session has published figures that are not hidden; hidden
/// otherwise. It never takes the focus or a click, and it does not change the Claude app.
/// </summary>
public sealed class ContextBarForm : Form
{
    private const int WsExToolWindow = 0x00000080;
    private const int WsExTopmost = 0x00000008;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;

    private readonly Timer _timer;
    private readonly FocusedSessionFinder _finder;
    private readonly string? _figuresRoot;
    private bool _probing;
    private Rectangle _slot;
    private ContextFigures? _figures;

    /// <summary>Creates the window, hidden and not yet watching. Throws <see cref="ArgumentNullException"/> for null.</summary>
    public ContextBarForm(ContextBarOptions options, FocusedSessionFinder finder)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(finder);
        _finder = finder;
        _figuresRoot = options.FiguresRoot;

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
            ContextFigures? figures = slot is null ? null : ReadFigures(FooterLocator.ShownSessionTitle(elements));
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

    private ContextFigures? ReadFigures(string? shownTitle)
    {
        string? id = _finder.Find(shownTitle);
        return id is null || _figuresRoot is null ? null : FiguresFile.TryRead(Path.Combine(_figuresRoot, id + ".json"));
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
