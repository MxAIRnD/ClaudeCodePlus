using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Feature.ChipButton;

/// <summary>
/// A small borderless, always-on-top button labelled "All projects" that sits right after the
/// chip row of the Claude app's new-session composer (see <see cref="ChipRowLocator"/>). Once
/// <see cref="StartWatching"/> is called it polls the foreground window every 400 ms: shown at
/// the row while a Claude window with the row is in front, hidden otherwise, and hidden when
/// it would cover one of the app's own buttons. It never takes the focus. A left click raises
/// <see cref="Pressed"/>; its context menu offers the same and "Exit", which raises
/// <see cref="ExitRequested"/>. It does not change the Claude app.
/// </summary>
public sealed class ChipButtonForm : Form
{
    private const string Label = "All projects";
    private const int WsExToolWindow = 0x00000080;
    private const int WsExTopmost = 0x00000008;
    private const int WsExNoActivate = 0x08000000;

    // Close to the app's own chips (white pill, thin warm-grey edge) in its light theme.
    private static readonly Color Fill = Color.FromArgb(255, 255, 255);
    private static readonly Color FillHover = Color.FromArgb(243, 241, 236);
    private static readonly Color Border = Color.FromArgb(222, 219, 212);
    private static readonly Color Ink = Color.FromArgb(40, 39, 37);

    private readonly Timer _timer;
    private Font? _font;
    private bool _probing;
    private bool _hover;

    /// <summary>Creates the button, hidden and not yet watching.</summary>
    public ChipButtonForm()
    {
        Text = "Claude Code Plus — button";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Fill;
        Cursor = Cursors.Hand;
        DoubleBuffered = true;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open the project list", null, (_, _) => Pressed?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Exit (remove the button until next sign-in)", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));
        ContextMenuStrip = menu;

        _timer = new Timer { Interval = 400 };
        _timer.Tick += async (_, _) => await TickAsync();
    }

    /// <summary>Raised on a left click and on the context menu's first item.</summary>
    public event EventHandler? Pressed;

    /// <summary>Raised on the context menu's "Exit"; the owner ends the message loop.</summary>
    public event EventHandler? ExitRequested;

    /// <inheritdoc/>
    protected override bool ShowWithoutActivation => true;

    /// <inheritdoc/>
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WsExToolWindow | WsExTopmost | WsExNoActivate;
            return cp;
        }
    }

    /// <summary>Starts polling the foreground window; call once, on the UI thread.</summary>
    public void StartWatching() => _timer.Start();

    /// <inheritdoc/>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location))
        {
            Pressed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc/>
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        Invalidate();
    }

    /// <inheritdoc/>
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        Invalidate();
    }

    /// <inheritdoc/>
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle r = ClientRectangle;
        r.Width -= 1;
        r.Height -= 1;
        using (GraphicsPath path = RoundedRectangle(r, Radius(ClientSize.Height)))
        using (var fill = new SolidBrush(_hover ? FillHover : Fill))
        using (var pen = new Pen(Border))
        {
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }

        if (_font is not null)
        {
            TextRenderer.DrawText(g, Label, _font, ClientRectangle, Ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _font?.Dispose();
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
            IReadOnlyList<ChipElement> elements = await Task.Run(ClaudeWindowProbe.Read);
            if (IsDisposed)
            {
                return;
            }

            Rectangle? anchor = ChipRowLocator.FindAnchor(elements);
            Rectangle bounds = anchor is null ? Rectangle.Empty : BoundsAfter(anchor.Value);
            if (anchor is null || ChipRowLocator.Collides(bounds, anchor.Value, elements))
            {
                if (Visible)
                {
                    Hide();
                }

                return;
            }

            Place(bounds);
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

    private Rectangle BoundsAfter(Rectangle anchor)
    {
        // The font follows the chip height, so the button matches the row at any display scale.
        float pixels = Math.Max(9f, anchor.Height * 0.46f);
        if (_font is null || Math.Abs(_font.Size - pixels) > 0.1f)
        {
            _font?.Dispose();
            _font = new Font("Segoe UI", pixels, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        int textWidth = TextRenderer.MeasureText(Label, _font).Width;
        return ChipRowLocator.ButtonBounds(anchor, textWidth);
    }

    private void Place(Rectangle bounds)
    {
        if (Bounds != bounds)
        {
            Bounds = bounds;
            using GraphicsPath shape = RoundedRectangle(new Rectangle(0, 0, bounds.Width, bounds.Height), Radius(bounds.Height));
            Region?.Dispose();
            Region = new Region(shape);
            Invalidate();
        }
    }

    private static int Radius(int height) => Math.Max(4, height / 5);

    private static GraphicsPath RoundedRectangle(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
