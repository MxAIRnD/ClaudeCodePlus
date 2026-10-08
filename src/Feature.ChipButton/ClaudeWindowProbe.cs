using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace Feature.ChipButton;

/// <summary>
/// Reads the chip-row candidates from the foreground window when it belongs to the Claude
/// desktop app (process name <c>claude</c>); otherwise reads nothing. Read-only UI Automation:
/// nothing is invoked or changed in the app. Any UI Automation failure reads as nothing.
/// </summary>
internal static class ClaudeWindowProbe
{
    private const string ClaudeProcessName = "claude";

    private static readonly Condition Candidates = new OrCondition(
        new PropertyCondition(AutomationElement.NameProperty, ChipRowLocator.AddFolderName),
        new PropertyCondition(AutomationElement.NameProperty, ChipRowLocator.WorktreeName));

    /// <summary>The candidates in the foreground Claude window; empty when Claude is not in front.</summary>
    public static IReadOnlyList<ChipElement> Read()
    {
        IntPtr window = GetForegroundWindow();
        if (window == IntPtr.Zero || !IsClaude(window))
        {
            return Array.Empty<ChipElement>();
        }

        try
        {
            AutomationElementCollection found = AutomationElement.FromHandle(window)
                .FindAll(TreeScope.Descendants, Candidates);
            var elements = new List<ChipElement>(found.Count);
            foreach (AutomationElement e in found)
            {
                AutomationElement.AutomationElementInformation info = e.Current;
                System.Windows.Rect r = info.BoundingRectangle;
                Rectangle bounds = r.IsEmpty
                    ? Rectangle.Empty
                    : new Rectangle((int)Math.Round(r.X), (int)Math.Round(r.Y), (int)Math.Round(r.Width), (int)Math.Round(r.Height));
                elements.Add(new ChipElement(info.Name ?? string.Empty, KindOf(info.ControlType), bounds, info.IsOffscreen));
            }

            return elements;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException or ArgumentException)
        {
            return Array.Empty<ChipElement>();
        }
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

    private static ChipElementKind KindOf(ControlType type) =>
        type == ControlType.Button ? ChipElementKind.Button
        : type == ControlType.CheckBox ? ChipElementKind.CheckBox
        : type == ControlType.Text ? ChipElementKind.Text
        : ChipElementKind.Other;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
