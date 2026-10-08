using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Feature.Catalog;
using Feature.DeepLink;

namespace Feature.Picker;

/// <summary>
/// The launcher's window: a search box over the catalog's rows, most recent first. Typing
/// filters the rows (see <see cref="ProjectFilter"/>); Up/Down move the selection from the box
/// or the list; Enter or a double-click opens the selected folder as a new Code session and
/// closes the window; Esc clears a non-empty search, otherwise closes. The list is read after
/// the window is shown; the title carries the row count ("shown of total" while filtered). A
/// folder that no longer exists is shown greyed. An opener error is shown in a message box and
/// the window stays open.
/// </summary>
public sealed class PickerForm : Form
{
    private const string Title = "Claude Code Plus";

    private readonly IProjectCatalog _catalog;
    private readonly ISessionOpener _opener;
    private readonly TextBox _search;
    private readonly ListView _list;
    private readonly CancellationTokenSource _closing = new();
    private IReadOnlyList<ProjectEntry> _all = Array.Empty<ProjectEntry>();

    /// <summary>Creates the window. Nothing is read until the window is shown.</summary>
    public PickerForm(IProjectCatalog catalog, ISessionOpener opener)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(opener);
        _catalog = catalog;
        _opener = opener;

        Text = Title;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(980, 600);
        MinimumSize = new Size(640, 320);
        KeyPreview = true;

        _search = new TextBox
        {
            Dock = DockStyle.Top,
            PlaceholderText = "Type to filter — Up/Down select, Enter opens, Esc clears or closes",
            Enabled = false,
        };
        _search.TextChanged += (_, _) => Refill();

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
            Enabled = false,
        };
        _list.Columns.Add("Project", 230);
        _list.Columns.Add("Last used", 90);
        _list.Columns.Add("Path", 560);
        _list.Columns.Add("Source", 60);
        _list.ItemActivate += (_, _) => OpenSelected();

        Controls.Add(_search);
        Controls.Add(_list);
        _list.BringToFront();
    }

    /// <inheritdoc/>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _ = LoadAsync();
    }

    /// <inheritdoc/>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _closing.Cancel();
        base.OnFormClosing(e);
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Escape:
                if (_search.Text.Length > 0)
                {
                    _search.Clear();
                }
                else
                {
                    Close();
                }

                Handled(e);
                return;
            case Keys.Enter when ActiveControl == _search:
                OpenSelected();
                Handled(e);
                return;
            case Keys.Down when ActiveControl == _search:
                MoveSelection(+1);
                Handled(e);
                return;
            case Keys.Up when ActiveControl == _search:
                MoveSelection(-1);
                Handled(e);
                return;
        }

        base.OnKeyDown(e);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _closing.Dispose();
        }

        base.Dispose(disposing);
    }

    private static void Handled(KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private async Task LoadAsync()
    {
        Text = Title + " — reading…";
        try
        {
            IReadOnlyList<ProjectEntry> entries = await _catalog.ListAsync(_closing.Token);
            if (!_closing.IsCancellationRequested)
            {
                _all = entries;
                _search.Enabled = true;
                _list.Enabled = true;
                Refill();
                _search.Focus();
            }
        }
        catch (OperationCanceledException)
        {
            // The window closed while reading: nothing to show.
        }
    }

    private void Refill()
    {
        IReadOnlyList<ProjectEntry> shown = ProjectFilter.Apply(_all, _search.Text);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (ProjectEntry entry in shown)
        {
            var item = new ListViewItem(new[]
            {
                entry.Name,
                ProjectRows.Age(entry.LastActivity, now),
                entry.Path,
                ProjectRows.SourcesLabel(entry.Sources),
            })
            {
                Tag = entry,
            };
            if (!entry.FolderExists)
            {
                item.ForeColor = SystemColors.GrayText;
            }

            _list.Items.Add(item);
        }

        _list.EndUpdate();
        Text = shown.Count == _all.Count
            ? $"{Title} — {_all.Count} projects"
            : $"{Title} — {shown.Count} of {_all.Count} projects";
        if (_list.Items.Count > 0)
        {
            _list.Items[0].Selected = true;
            _list.Items[0].Focused = true;
        }
    }

    private void MoveSelection(int delta)
    {
        if (_list.Items.Count == 0)
        {
            return;
        }

        int current = _list.SelectedIndices.Count > 0 ? _list.SelectedIndices[0] : -1;
        int next = Math.Clamp(current + delta, 0, _list.Items.Count - 1);
        _list.Items[next].Selected = true;
        _list.Items[next].Focused = true;
        _list.EnsureVisible(next);
    }

    private void OpenSelected()
    {
        if (_list.SelectedItems.Count == 0 || _list.SelectedItems[0].Tag is not ProjectEntry entry)
        {
            return;
        }

        try
        {
            _opener.OpenNewSession(entry.Path);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            MessageBox.Show(this, ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // Close once the current message is done: Close disposes the window and its list, and
        // the list's own double-click handling still runs after this returns.
        BeginInvoke(() =>
        {
            if (!IsDisposed)
            {
                Close();
            }
        });
    }
}
