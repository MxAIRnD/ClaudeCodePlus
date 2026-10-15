using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Feature.Catalog;
using Feature.ChipButton;
using Feature.ContextBar;
using Feature.DeepLink;
using Feature.Picker;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Infrastructure;

/// <summary>The composition root: options from the environment, the modules, the window.</summary>
public static class LauncherComposition
{
    /// <summary>The command-line switch that runs the resident chip-row button.</summary>
    public const string ButtonSwitch = "--button";

    private const string ButtonMutexName = @"Local\ClaudeCodePlus.ChipButton";

    /// <summary>
    /// Composes the launcher and runs the picker window until it closes. Options: the desktop
    /// session store under <c>%APPDATA%</c>, the projects folder and the context-bar figures
    /// folder under <c>%USERPROFILE%\.claude</c> (or <c>%CLAUDE_CONFIG_DIR%</c>), the diagnostics
    /// file <c>%LOCALAPPDATA%\ClaudeCodePlus\diagnostics.log</c>, every noise switch at its
    /// default (hidden). Call once, from a thread marked <c>[STAThread]</c>, after WinForms
    /// initialization.
    /// </summary>
    public static void Run()
    {
        using ServiceProvider provider = Compose();
        using PickerForm picker = provider.GetRequiredService<PickerForm>();
        Application.Run(picker);
    }

    /// <summary>
    /// Runs by command line: with <see cref="ButtonSwitch"/> (any case) the resident chip-row
    /// button — one per sign-in session; a second start returns at once — whose press opens the
    /// picker (or brings an open one to the front) and whose "Exit" ends it, together with the
    /// context line in the prompt footer of a running session (<see cref="ContextBarForm"/>,
    /// ended with the button); it lists the catalog once at start, in the background, so that
    /// the first press is quick too; otherwise exactly <see cref="Run()"/>. Same threading rules
    /// as <see cref="Run()"/>. Throws <see cref="ArgumentNullException"/> for null.
    /// </summary>
    public static void Run(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (Array.Exists(args, a => string.Equals(a, ButtonSwitch, StringComparison.OrdinalIgnoreCase)))
        {
            RunButton();
        }
        else
        {
            Run();
        }
    }

    private static void RunButton()
    {
        using var single = new Mutex(initiallyOwned: true, ButtonMutexName, out bool isFirst);
        if (!isFirst)
        {
            return;
        }

        using ServiceProvider provider = Compose();
        using ChipButtonForm button = provider.GetRequiredService<ChipButtonForm>();
        PickerForm? picker = null;
        button.Pressed += (_, _) =>
        {
            if (picker is null || picker.IsDisposed)
            {
                picker = provider.GetRequiredService<PickerForm>();
                picker.Show();
            }

            picker.Activate();
        };
        button.ExitRequested += (_, _) => Application.ExitThread();
        button.StartWatching();

        using ContextBarForm bar = provider.GetRequiredService<ContextBarForm>();
        bar.StartWatching();

        // Fill the catalog's memo now, in the background: the first press then lists from memory.
        _ = provider.GetRequiredService<IProjectCatalog>().ListAsync();
        Application.Run(new ApplicationContext());
        picker?.Dispose();
    }

    private static ServiceProvider Compose()
    {
        var options = new CatalogOptions
        {
            DesktopSessionsRoot = ClaudeDataPaths.DesktopSessionsRoot(),
            TerminalProjectsRoot = ClaudeDataPaths.TerminalProjectsRoot(),
        };

        // One diagnostics file for the button and the line, each writing once per change.
        string diagnosticsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeCodePlus", "diagnostics.log");
        var buttonOptions = new ChipButtonOptions { DiagnosticsFile = diagnosticsFile };
        var barOptions = new ContextBarOptions
        {
            FiguresRoot = ClaudeDataPaths.ContextBarRoot(),
            DiagnosticsFile = diagnosticsFile,
        };

        return new ServiceCollection()
            .AddCatalog(options)
            .AddDeepLink()
            .AddPicker()
            .AddChipButton(buttonOptions)
            .AddContextBar(barOptions)
            .BuildServiceProvider();
    }
}
