using System;
using System.IO;

namespace Core.Infrastructure;

/// <summary>Where Claude keeps its local data on this machine, resolved from the environment.</summary>
internal static class ClaudeDataPaths
{
    private const string ConfigDirVariable = "CLAUDE_CONFIG_DIR";

    /// <summary>The desktop app's session store: <c>%APPDATA%\Claude\claude-code-sessions</c>.</summary>
    internal static string DesktopSessionsRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude", "claude-code-sessions");

    /// <summary>
    /// The Claude Code projects folder: <c>%CLAUDE_CONFIG_DIR%\projects</c> when the variable is
    /// set and non-empty, otherwise <c>%USERPROFILE%\.claude\projects</c>.
    /// </summary>
    internal static string TerminalProjectsRoot()
    {
        string? configDir = Environment.GetEnvironmentVariable(ConfigDirVariable);
        string root = string.IsNullOrWhiteSpace(configDir)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude")
            : configDir.Trim();
        return Path.Combine(root, "projects");
    }
}
