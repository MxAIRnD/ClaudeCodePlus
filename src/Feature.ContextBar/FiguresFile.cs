using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Feature.ContextBar;

/// <summary>Reads the figures file the context-bar mod writes, and formats token counts as the line shows them.</summary>
public static class FiguresFile
{
    /// <summary>
    /// Parses a figures file's text: a JSON object with <c>sessionId</c> (a non-empty string) and
    /// <c>window</c> (a positive integer), and optionally <c>tokens</c> and <c>threshold</c>
    /// (integers, or null), <c>at</c> (an integer) and <c>isHidden</c> (a boolean). A missing or
    /// differently typed optional field reads as absent (<c>at</c> as 0, <c>isHidden</c> as
    /// false). Null for anything else, malformed JSON included. Throws
    /// <see cref="ArgumentNullException"/> for null.
    /// </summary>
    public static ContextFigures? Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!root.TryGetProperty("sessionId", out JsonElement id) || id.ValueKind != JsonValueKind.String
                || string.IsNullOrEmpty(id.GetString()))
            {
                return null;
            }

            long? window = Integer(root, "window");
            if (window is not long w || w <= 0)
            {
                return null;
            }

            bool isHidden = root.TryGetProperty("isHidden", out JsonElement hidden) && hidden.ValueKind == JsonValueKind.True;
            return new ContextFigures(id.GetString()!, Integer(root, "tokens"), w, Integer(root, "threshold"), Integer(root, "at") ?? 0, isHidden);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads and parses the file at <paramref name="path"/>: null when it is missing, cannot be
    /// read, or is not a figures file. Throws <see cref="ArgumentNullException"/> for null.
    /// </summary>
    public static ContextFigures? TryRead(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// A token count as the line shows it: under a thousand as is (840), under ten thousand with
    /// one decimal (8.4K, 8K), under a million rounded (84K, 200K), else in millions with up to
    /// two decimals (1M, 1.25M). Halves round away from zero; a negative count reads as 0.
    /// </summary>
    public static string FormatTokens(long n)
    {
        if (n < 0)
        {
            n = 0;
        }

        if (n < 1000)
        {
            return n.ToString(CultureInfo.InvariantCulture);
        }

        if (n < 10_000)
        {
            string s = Math.Round(n / 1000.0, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture);
            return (s.EndsWith(".0", StringComparison.Ordinal) ? s[..^2] : s) + "K";
        }

        if (n < 1_000_000)
        {
            return Math.Round(n / 1000.0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "K";
        }

        return Math.Round(n / 1_000_000.0, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture) + "M";
    }

    private static long? Integer(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long n)
            ? n
            : null;
}
