using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Feature.Catalog;
using Feature.DeepLink;
using Xunit;

namespace Feature.Picker.Tests;

/// <summary>
/// Regression for the resident button's crash (2026-10-08): a double-click on a row of a
/// window shown modelessly (<c>Show</c> inside <c>Application.Run(new ApplicationContext())</c>,
/// as the button shows it) opened the folder, then threw <see cref="ObjectDisposedException"/>
/// out of the list's own click handling. Each test runs one real window on its own STA thread,
/// off-screen, over one made-up row; input is posted to the window's handles, so it is
/// dispatched by the message loop like a real click or key. A recording opener stands in for
/// the shell: no link is started.
/// </summary>
public sealed class PickerFormCloseTests
{
    private const string Folder = @"C:\Work\Alpha";

    private const int WM_KEYDOWN = 0x0100;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int MK_LBUTTON = 0x0001;
    private const int VK_RETURN = 0x0D;

    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(15);

    [Fact]
    public void Double_click_on_a_row_of_a_modeless_window_opens_it_and_closes_without_an_exception()
    {
        Outcome outcome = Run(asMainForm: false, DoubleClickFirstRow);

        Assert.Empty(outcome.Errors);
        Assert.Equal(new[] { Folder }, outcome.Opened);
        Assert.True(outcome.Disposed, "the window should end closed (disposed)");
    }

    [Fact]
    public void Enter_in_the_search_box_of_a_modeless_window_opens_the_row_and_closes_without_an_exception()
    {
        Outcome outcome = Run(asMainForm: false, EnterInSearchBox);

        Assert.Empty(outcome.Errors);
        Assert.Equal(new[] { Folder }, outcome.Opened);
        Assert.True(outcome.Disposed, "the window should end closed (disposed)");
    }

    [Fact]
    public void Double_click_on_a_row_of_the_main_window_opens_it_and_closes_without_an_exception()
    {
        Outcome outcome = Run(asMainForm: true, DoubleClickFirstRow);

        Assert.Empty(outcome.Errors);
        Assert.Equal(new[] { Folder }, outcome.Opened);
        Assert.True(outcome.Disposed, "the window should end closed (disposed)");
    }

    private static void DoubleClickFirstRow(PickerForm form)
    {
        ListView list = form.Controls.OfType<ListView>().Single();
        Rectangle row = list.Items[0].Bounds;
        PostMessage(list.Handle, WM_LBUTTONDBLCLK, MK_LBUTTON, LParam(row.X + 8, row.Y + (row.Height / 2)));
    }

    private static void EnterInSearchBox(PickerForm form)
    {
        TextBox search = form.Controls.OfType<TextBox>().Single();
        PostMessage(search.Handle, WM_KEYDOWN, VK_RETURN, 0);
    }

    private static nint LParam(int x, int y) => (y << 16) | (x & 0xFFFF);

    // One window on an STA thread with a running message loop: modeless inside an empty
    // ApplicationContext (the resident button), or as the loop's main form (single-shot). A
    // timer posts the input once the row is listed and ends the loop once the window is gone.
    private static Outcome Run(bool asMainForm, Action<PickerForm> input)
    {
        var opener = new RecordingOpener();
        var errors = new List<Exception>();
        bool disposed = false;
        Exception? harness = null;

        var thread = new Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles();
                Application.ThreadException += (_, e) => errors.Add(e.Exception);
                var form = new PickerForm(new OneRowCatalog(), opener)
                {
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-20000, -20000),
                    ShowInTaskbar = false,
                };
                bool posted = false;
                DateTime end = DateTime.UtcNow + Deadline;
                using var timer = new System.Windows.Forms.Timer { Interval = 20 };
                timer.Tick += (_, _) =>
                {
                    if (form.IsDisposed || DateTime.UtcNow > end)
                    {
                        timer.Stop();
                        Application.ExitThread();
                    }
                    else if (!posted && form.Controls.OfType<ListView>().Single().Items.Count > 0)
                    {
                        posted = true;
                        input(form);
                    }
                };
                timer.Start();
                if (asMainForm)
                {
                    Application.Run(form);
                }
                else
                {
                    form.Show();
                    Application.Run(new ApplicationContext());
                }

                disposed = form.IsDisposed;
                form.Dispose();
            }
            catch (Exception ex)
            {
                harness = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(Deadline + TimeSpan.FromSeconds(15)), "the window thread did not end");
        Assert.Null(harness);
        return new Outcome(errors, opener.Opened, disposed);
    }

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, int msg, nint wParam, nint lParam);

    private sealed record Outcome(List<Exception> Errors, List<string> Opened, bool Disposed);

    private sealed class OneRowCatalog : IProjectCatalog
    {
        public Task<IReadOnlyList<ProjectEntry>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjectEntry>>(new[]
            {
                new ProjectEntry(Folder, "Alpha", DateTimeOffset.UtcNow.AddHours(-1), ProjectSources.DesktopSessions, 1, true),
            });
    }

    private sealed class RecordingOpener : ISessionOpener
    {
        public List<string> Opened { get; } = new();

        public string BuildNewSessionLink(string folderPath, string? prompt = null) => throw new NotSupportedException();

        public void OpenNewSession(string folderPath, string? prompt = null) => Opened.Add(folderPath);
    }
}
