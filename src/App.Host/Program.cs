using System;
using Core.Infrastructure;

namespace App.Host;

/// <summary>Entry point of ClaudeCodePlus.exe: WinForms initialization, then the composition root.</summary>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        LauncherComposition.Run(args);
    }
}
