using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Feature.DeepLink;

/// <summary>The link builder and shell launcher behind <see cref="ISessionOpener"/>.</summary>
internal sealed class SessionOpener : ISessionOpener
{
    private const string NewSessionRoute = "claude://code/new";

    public string BuildNewSessionLink(string folderPath, string? prompt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        if (!Path.IsPathFullyQualified(folderPath))
        {
            throw new ArgumentException(
                $"The folder must be a fully qualified path (drive or UNC root): '{folderPath}'.",
                nameof(folderPath));
        }

        if (prompt is not null && prompt.Length > ISessionOpener.MaxPromptLength)
        {
            throw new ArgumentException(
                $"The prompt is {prompt.Length} characters; the app accepts at most {ISessionOpener.MaxPromptLength}.",
                nameof(prompt));
        }

        string link = NewSessionRoute + "?folder=" + Uri.EscapeDataString(folderPath);
        if (!string.IsNullOrEmpty(prompt))
        {
            link += "&q=" + Uri.EscapeDataString(prompt);
        }

        return link;
    }

    public void OpenNewSession(string folderPath, string? prompt = null)
    {
        string link = BuildNewSessionLink(folderPath, prompt);
        try
        {
            // The shell routes the scheme to the registered claude:// handler and returns at once;
            // there is no process handle worth keeping for a protocol launch.
            using Process? handler = Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException($"Windows could not start the link {link}: {ex.Message}", ex);
        }
    }
}
