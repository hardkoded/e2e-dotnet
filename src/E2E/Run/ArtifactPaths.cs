// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using E2E.Internal;
using E2E.Reports;

namespace E2E;

/// <summary>The names a run's files go by on disk (upstream's <c>run/artifacts.ts</c>, the part a trace page needs).</summary>
internal static partial class ArtifactPaths
{
    /// <summary>Characters of the test file's name a result's segment keeps.</summary>
    private const int MaxFileSlugChars = 40;

    /// <summary>Characters of the title's first words a result's segment keeps.</summary>
    private const int MaxTitleSlugChars = 32;

    /// <summary>
    /// Hex characters of the result id a result's segment ends in. Results whose file and first title words agree differ only in it,
    /// and 32 bits collide within a short search, so it keeps 64.
    /// </summary>
    private const int ResultDigestChars = 16;

    /// <summary>Latin letters NFKD leaves whole, spelled the way a slug reads them.</summary>
    private static readonly Dictionary<char, string> LatinLetters = new()
    {
        ['æ'] = "ae",
        ['ð'] = "d",
        ['đ'] = "d",
        ['ı'] = "i",
        ['ł'] = "l",
        ['ø'] = "o",
        ['œ'] = "oe",
        ['ß'] = "ss",
        ['þ'] = "th",
    };

    [GeneratedRegex(@"(\.e2e)?\.([cm]?[jt]sx?|cs)\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex SourceExtension();

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex NotAlphanumeric();

    /// <summary>
    /// The name a result goes by on disk: the test file's name, a lowercase ASCII slug of the title's first words, and the head of
    /// the result id, e.g. <c>checkout-applies-the-coupon-1a2b3c4d5e6f7a8b</c>. A first title word equal to the file's name is left out
    /// rather than said twice. The slugs are for reading only; the id keeps two results with the same words apart. Upstream's
    /// <c>resultSegment</c>.
    /// </summary>
    public static string ResultSegment(string id, string file, IReadOnlyList<string> titlePath)
    {
        var name = file[(file.LastIndexOf('/') + 1)..];
        var stem = NotAlphanumeric().Replace(SourceExtension().Replace(name, "").ToLowerInvariant(), "-").Trim('-');
        stem = stem.Length > MaxFileSlugChars ? stem[..MaxFileSlugChars] : stem;

        // Accents come off first, so `café` reads `cafe`, not words split at each accent.
        var plain = new StringBuilder();
        foreach (var letter in string.Join(' ', titlePath).Normalize(NormalizationForm.FormKD).ToLowerInvariant())
        {
            if (CharUnicodeInfo.GetUnicodeCategory(letter) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            plain.Append(LatinLetters.TryGetValue(letter, out var spelled) ? spelled : letter.ToString());
        }

        var words = NotAlphanumeric().Split(plain.ToString()).Where(word => word.Length > 0).ToList();
        if (words.Count > 0 && string.Equals(words[0], stem, StringComparison.Ordinal))
        {
            words.RemoveAt(0);
        }

        var title = words.Count == 0 ? "" : words[0].Length > MaxTitleSlugChars ? words[0][..MaxTitleSlugChars] : words[0];
        foreach (var word in words.Skip(1))
        {
            if (title.Length + 1 + word.Length > MaxTitleSlugChars)
            {
                break;
            }

            title = title + "-" + word;
        }

        return string.Join('-', new[] { stem, title, id.Length > ResultDigestChars ? id[..ResultDigestChars] : id }.Where(part => part.Length > 0));
    }

    /// <summary>The media type of a file by its extension (upstream's <c>mediaTypeFor</c>).</summary>
    public static string MediaTypeFor(string relativePath)
    {
        if (relativePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return "image/png";
        }

        if (relativePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return "application/zip";
        }

        if (relativePath.EndsWith(".webm", StringComparison.OrdinalIgnoreCase))
        {
            return "video/webm";
        }

        if (relativePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            return "video/mp4";
        }

        if (relativePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return "text/plain";
        }

        if (relativePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return "text/csv";
        }

        if (relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return "application/json";
        }

        if (relativePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || relativePath.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
        {
            return "text/html";
        }

        return "application/octet-stream";
    }

    /// <summary>Whether the bytes of a media type are text the secret redactor can rewrite (upstream's <c>isTextLike</c>).</summary>
    public static bool IsTextLike(string mediaType) => mediaType.StartsWith("text/", StringComparison.Ordinal) || mediaType == "application/json";

    /// <summary>
    /// Rewrites a text-like download through the session's ledger, and returns the redaction the record can claim: <c>Complete</c> once
    /// every registered value is gone from it, changed or not, and <c>Incomplete</c> when the file contains NUL bytes, is not UTF-8 text,
    /// or cannot be read or written, in which case it is left as served. A leading byte order mark is kept as part of the text, so a
    /// rewritten file starts the way it was served. Upstream's <c>redactDownload</c>.
    /// </summary>
    public static ArtifactRedaction RedactDownload(string absolute, Redactor ledger)
    {
        try
        {
            var bytes = File.ReadAllBytes(absolute);
            // BOM-less UTF-16 can pass UTF-8 decoding while NULs keep its secrets from matching.
            if (bytes.AsSpan().Contains((byte)0))
            {
                return ArtifactRedaction.Incomplete;
            }

            // GetString keeps a leading byte order mark as U+FEFF, as upstream's ignoreBOM does.
            var text = new UTF8Encoding(false, true).GetString(bytes);
            var redacted = ledger.RedactFragments(text);
            if (!string.Equals(redacted, text, StringComparison.Ordinal))
            {
                AtomicWrite.WriteFileAtomic(absolute, redacted);
            }

            return ArtifactRedaction.Complete;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException or RegexMatchTimeoutException)
        {
            return ArtifactRedaction.Incomplete;
        }
    }
}
