using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace Feature.ContextBar;

/// <summary>
/// Reads the locator's candidates from the foreground window when it belongs to the Claude
/// desktop app (process name <c>claude</c>); otherwise reads nothing. Candidates are every
/// button UI Automation calls on screen (the footer's chips among them) and every document (the
/// app's page, whose name is its title). Their properties come back in one cached request
/// instead of a call per property. Read-only UI Automation: nothing is invoked or changed in
/// the app. Any UI Automation failure reads as nothing.
/// </summary>
internal static class FooterProbe
{
    private const string ClaudeProcessName = "claude";

    private static readonly Condition Candidates = new OrCondition(
        new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
            new PropertyCondition(AutomationElement.IsOffscreenProperty, false)),
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));

    /// <summary>The candidates in the foreground Claude window; empty when Claude is not in front.</summary>
    public static IReadOnlyList<FooterElement> Read()
    {
        IntPtr window = GetForegroundWindow();
        if (window == IntPtr.Zero || !IsClaude(window))
        {
            return Array.Empty<FooterElement>();
        }

        try
        {
            AutomationElement root = AutomationElement.FromHandle(window);
            AutomationElementCollection found;
            using (Properties().Activate())
            {
                found = root.FindAll(TreeScope.Descendants, Candidates);
            }

            var elements = new List<FooterElement>(found.Count);
            foreach (AutomationElement e in found)
            {
                AutomationElement.AutomationElementInformation info = e.Cached;
                System.Windows.Rect r = info.BoundingRectangle;
                Rectangle bounds = r.IsEmpty
                    ? Rectangle.Empty
                    : new Rectangle((int)Math.Round(r.X), (int)Math.Round(r.Y), (int)Math.Round(r.Width), (int)Math.Round(r.Height));
                elements.Add(new FooterElement(info.Name ?? string.Empty, KindOf(info.ControlType), bounds, info.IsOffscreen));
            }

            return elements;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException or ArgumentException)
        {
            return Array.Empty<FooterElement>();
        }
    }

    // The four properties a FooterElement needs, fetched with the search; no live references kept.
    private static CacheRequest Properties()
    {
        var request = new CacheRequest { AutomationElementMode = AutomationElementMode.None };
        request.Add(AutomationElement.NameProperty);
        request.Add(AutomationElement.ControlTypeProperty);
        request.Add(AutomationElement.BoundingRectangleProperty);
        request.Add(AutomationElement.IsOffscreenProperty);
        return request;
    }

    private static bool IsClaude(IntPtr window)
    {
        _ = GetWindowThreadProcessId(window, out uint pid);
        if (pid == 0)
        {
            return false;
        }

        try
        {
            using Process process = Process.GetProcessById((int)pid);
            return string.Equals(process.ProcessName, ClaudeProcessName, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false; // the process ended between the two calls
        }
    }

    private static FooterElementKind KindOf(ControlType type) =>
        type == ControlType.Button ? FooterElementKind.Button
        : type == ControlType.Document ? FooterElementKind.Document
        : FooterElementKind.Other;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
