using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace Feature.Catalog;

/// <summary>
/// The desktop app's session store: every <c>local_*.json</c> under the root (recursively) is
/// one session record; the ones that name a folder become sightings. A record is read with a
/// streaming reader that stops as soon as its <c>originCwd</c> and <c>lastActivityAt</c> are
/// known: they sit at the start of the record, before the bulk of it, so a read usually
/// touches one chunk. What a record yielded is kept in memory with the record's size and last
/// write time, and the record is read again only when either changed; a record that could not
/// be opened is not kept and is tried again next time. The memo lives as long as this instance.
/// Read-only; a file that cannot be read or parsed, or has no usable <c>originCwd</c>, is skipped.
/// </summary>
internal sealed class DesktopSessionStore
{
    private const string RecordPattern = "local_*.json";
    private const int ChunkSize = 16 * 1024;
    private const long MaxUnixMilliseconds = 253_402_300_799_999; // 9999-12-31, the DateTimeOffset ceiling

    private static readonly EnumerationOptions Recursive = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        MatchCasing = MatchCasing.CaseInsensitive,
    };

    private readonly string _root;

    // Replaced as a whole by every completed read, so concurrent reads never share a dictionary.
    private volatile Dictionary<string, Memo> _memo = new(StringComparer.OrdinalIgnoreCase);

    public DesktopSessionStore(string root)
    {
        _root = root;
    }

    internal List<Sighting> Read(CancellationToken cancellationToken)
    {
        var sightings = new List<Sighting>();
        if (!Directory.Exists(_root))
        {
            return sightings;
        }

        List<FileInfo> files;
        try
        {
            // The enumeration carries each file's size and write time; no file is opened for them.
            files = new List<FileInfo>(new DirectoryInfo(_root).EnumerateFiles(RecordPattern, Recursive));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return sightings;
        }

        Dictionary<string, Memo> previous = _memo;
        var next = new Dictionary<string, Memo>(files.Count, StringComparer.OrdinalIgnoreCase);
        foreach (FileInfo file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long length = file.Length;
            DateTime written = file.LastWriteTimeUtc;
            if (!previous.TryGetValue(file.FullName, out Memo memo) || memo.Length != length || memo.LastWriteUtc != written)
            {
                if (!TryReadRecord(file.FullName, written, out Sighting? sighting))
                {
                    continue;
                }

                memo = new Memo(length, written, sighting);
            }

            next[file.FullName] = memo;
            if (memo.Sighting is not null)
            {
                sightings.Add(memo.Sighting);
            }
        }

        _memo = next;
        return sightings;
    }

    /// <summary>
    /// False when the file could not be opened or read (worth another try next time). True
    /// otherwise, with a null <paramref name="sighting"/> when the file is not a usable record.
    /// </summary>
    private static bool TryReadRecord(string file, DateTime lastWriteUtc, out Sighting? sighting)
    {
        sighting = null;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, ChunkSize, FileOptions.SequentialScan);
            sighting = ReadRecord(stream, ref buffer, lastWriteUtc);
            return true;
        }
        catch (JsonException)
        {
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Walks the record token by token, chunk by chunk, and returns as soon as the root object's
    /// <c>originCwd</c> and <c>lastActivityAt</c> are known; otherwise reads to the end, where a
    /// missing <c>lastActivityAt</c> falls back to <c>createdAt</c>, then to the file's write time.
    /// Null for a root that is not an object or an <c>originCwd</c> that is not a non-blank
    /// string. Throws <see cref="JsonException"/> for malformed JSON before the keys are known.
    /// </summary>
    private static Sighting? ReadRecord(FileStream stream, ref byte[] buffer, DateTime lastWriteUtc)
    {
        var state = new JsonReaderState();
        int filled = 0;
        bool firstChunk = true;
        Pending pending = Pending.None;
        bool cwdSeen = false;
        string? cwd = null;
        long? lastActivityAt = null;
        long? createdAt = null;

        while (true)
        {
            int read = stream.Read(buffer, filled, buffer.Length - filled);
            bool isFinalBlock = read == 0;
            filled += read;
            int start = 0;
            if (firstChunk)
            {
                firstChunk = false;
                // JsonDocument.Parse(Stream) skips a UTF-8 byte-order mark; Utf8JsonReader does not.
                if (filled >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                {
                    start = 3;
                }
            }

            var reader = new Utf8JsonReader(buffer.AsSpan(start, filled - start), isFinalBlock, state);
            while (reader.Read())
            {
                if (pending != Pending.None)
                {
                    // The value of a root-level property of interest.
                    switch (pending)
                    {
                        case Pending.OriginCwd:
                            cwdSeen = true;
                            if (reader.TokenType != JsonTokenType.String)
                            {
                                return null;
                            }

                            cwd = reader.GetString();
                            if (string.IsNullOrWhiteSpace(cwd))
                            {
                                return null;
                            }

                            break;
                        case Pending.LastActivityAt:
                            lastActivityAt = UnixMilliseconds(ref reader);
                            break;
                        case Pending.CreatedAt:
                            createdAt = UnixMilliseconds(ref reader);
                            break;
                    }

                    pending = Pending.None;
                    if (cwdSeen && lastActivityAt is not null)
                    {
                        return ToSighting(cwd!, lastActivityAt, createdAt, lastWriteUtc);
                    }

                    continue;
                }

                if (reader.CurrentDepth == 0
                    && reader.TokenType != JsonTokenType.StartObject
                    && reader.TokenType != JsonTokenType.EndObject)
                {
                    return null; // the root is not an object
                }

                if (reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 1)
                {
                    if (reader.ValueTextEquals("originCwd"u8))
                    {
                        pending = Pending.OriginCwd;
                    }
                    else if (reader.ValueTextEquals("lastActivityAt"u8))
                    {
                        pending = Pending.LastActivityAt;
                    }
                    else if (reader.ValueTextEquals("createdAt"u8))
                    {
                        pending = Pending.CreatedAt;
                    }
                }
            }

            if (isFinalBlock)
            {
                // The whole record was valid JSON (the reader throws otherwise).
                return cwdSeen ? ToSighting(cwd!, lastActivityAt, createdAt, lastWriteUtc) : null;
            }

            // Keep the unconsumed tail, carry the reader's state, append the next chunk.
            state = reader.CurrentState;
            int consumed = start + (int)reader.BytesConsumed;
            int tail = filled - consumed;
            if (tail > 0)
            {
                Buffer.BlockCopy(buffer, consumed, buffer, 0, tail);
            }

            filled = tail;
            if (filled == buffer.Length)
            {
                // One token longer than the buffer (a very long string): grow.
                byte[] larger = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                Buffer.BlockCopy(buffer, 0, larger, 0, filled);
                ArrayPool<byte>.Shared.Return(buffer);
                buffer = larger;
            }
        }
    }

    private static long? UnixMilliseconds(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.Number
        && reader.TryGetInt64(out long milliseconds)
        && milliseconds > 0
        && milliseconds <= MaxUnixMilliseconds
            ? milliseconds
            : null;

    private static Sighting ToSighting(string cwd, long? lastActivityAt, long? createdAt, DateTime lastWriteUtc)
    {
        DateTimeOffset when = lastActivityAt is long last ? DateTimeOffset.FromUnixTimeMilliseconds(last)
            : createdAt is long created ? DateTimeOffset.FromUnixTimeMilliseconds(created)
            : new DateTimeOffset(lastWriteUtc);
        return new Sighting(cwd, when, ProjectSources.DesktopSessions);
    }

    /// <summary>The root-level property whose value the next token is.</summary>
    private enum Pending
    {
        None,
        OriginCwd,
        LastActivityAt,
        CreatedAt,
    }

    /// <summary>What one record file yielded, with the size and write time it had then.</summary>
    private readonly record struct Memo(long Length, DateTime LastWriteUtc, Sighting? Sighting);
}
