using System;

namespace Feature.DeepLink;

/// <summary>
/// Opens a new Claude desktop Code session in a folder through the app's
/// <c>claude://code/new</c> deep link.
/// </summary>
public interface ISessionOpener
{
    /// <summary>
    /// The longest prompt the app accepts on the link (characters). Longer prompts would be cut
    /// silently by the app, so this module rejects them instead.
    /// </summary>
    const int MaxPromptLength = 14336;

    /// <summary>
    /// Builds the link text, exactly what <see cref="OpenNewSession"/> hands to the shell:
    /// <c>claude://code/new?folder=&lt;encoded path&gt;</c>, followed by
    /// <c>&amp;q=&lt;encoded prompt&gt;</c> when <paramref name="prompt"/> is non-empty. Encoding
    /// is <see cref="Uri.EscapeDataString(string)"/> (so <c>C:\RnDs\AI</c> becomes
    /// <c>C%3A%5CRnDs%5CAI</c>); the path is encoded as given, with no normalization and no
    /// existence check. Pure: no side effects. Throws <see cref="ArgumentException"/> when
    /// <paramref name="folderPath"/> is null, empty or not fully qualified (fully qualified = rooted
    /// at a drive, <c>C:\…</c>, or a UNC share, <c>\\server\share\…</c>; so a relative path,
    /// <c>C:folder</c> and <c>\folder</c> throw),
    /// or when <paramref name="prompt"/> is longer than <see cref="MaxPromptLength"/>.
    /// </summary>
    string BuildNewSessionLink(string folderPath, string? prompt = null);

    /// <summary>
    /// Builds the link (same rules and exceptions as <see cref="BuildNewSessionLink"/>) and
    /// starts it through the Windows shell, which hands it to the registered <c>claude://</c>
    /// handler: the app opens the Code tab with a new-session draft in that folder and asks
    /// once to confirm the folder; a closed app is started first. Returns as soon as the shell
    /// has accepted the link; does not wait for the app and does not confirm that a session
    /// appeared. Throws <see cref="InvalidOperationException"/> when Windows has no handler for
    /// <c>claude://</c> or refuses to start it.
    /// </summary>
    void OpenNewSession(string folderPath, string? prompt = null);
}
