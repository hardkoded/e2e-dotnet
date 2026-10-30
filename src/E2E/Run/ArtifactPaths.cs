// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Security.Cryptography;
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
    /// How much of each kind the runner masked. A screenshot masks secure fields at the source; a log passes through the secret redactor.
    /// A recording masks nothing: a secure field renders its own dots, but anything else the screen showed is in the frames. A download is
    /// whatever the app served, bytes the runner did not write and does not rewrite: it is <c>Incomplete</c> unless the sink scanned it (see
    /// <see cref="RedactDownload"/>), so a store exporting only vouched-for artifacts holds it back. Upstream's <c>REDACTION_BY_KIND</c>.
    /// </summary>
    internal static ArtifactRedaction RedactionByKind(ArtifactKind kind) =>
        kind is ArtifactKind.Screenshot or ArtifactKind.Log ? ArtifactRedaction.Complete : ArtifactRedaction.Incomplete;

    /// <summary>
    /// Creates the artifact directory for one attempt and a sink that registers produced files under report-relative paths, measuring and
    /// hashing them asynchronously. With a store, each complete artifact is also handed to it right then, as produced and not at run end,
    /// and the store's reference lands on the record as <c>Ref</c>; a provider-hosted link goes to the store's <c>PutLinkAsync</c> the same
    /// way. Upstream's <c>createAttemptArtifacts</c>.
    /// </summary>
    public static AttemptArtifacts CreateAttemptArtifacts(AttemptArtifactsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new AttemptArtifacts(options);
    }

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

    /// <summary>Size and SHA-256 of one file, streamed; null when it cannot be read (upstream's <c>measure</c>).</summary>
    internal static async Task<(long Size, string Sha256)?> MeasureAsync(string absolute, CancellationToken cancellationToken)
    {
        try
        {
            var stream = new FileStream(absolute, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: true);
            await using (stream.ConfigureAwait(false))
            {
                var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
                return (stream.Length, Convert.ToHexStringLower(hash));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

/// <summary>The identity handed to the store with each artifact (upstream's <c>identity</c> option of <c>createAttemptArtifacts</c>).</summary>
/// <param name="RunId">The run the attempt belongs to.</param>
/// <param name="TestId">The test the attempt belongs to.</param>
/// <param name="AttemptId">
/// The attempt the report files the artifact under; a serial member's artifacts land on the group attempt's record, so its store identity
/// is the group's id while its own ids (<c>&lt;attemptId&gt;:artifact:N</c>) still mint from the member. Defaults to the options' <c>AttemptId</c>.
/// </param>
internal sealed record ArtifactIdentity(string RunId, string TestId, string? AttemptId = null);

/// <summary>The options of <see cref="ArtifactPaths.CreateAttemptArtifacts"/>.</summary>
internal sealed class AttemptArtifactsOptions
{
    public required string ArtifactsRoot { get; init; }

    /// <summary>Report path segments, e.g. <c>[ResultSegment(result), "attempt-1"]</c>.</summary>
    public required IReadOnlyList<string> Segments { get; init; }

    public required string AttemptId { get; init; }

    /// <summary>When provided, artifacts are attributed to the currently running step.</summary>
    public Func<string?>? CurrentStepId { get; init; }

    /// <summary>Host store every artifact is handed to once complete; null keeps files local only.</summary>
    public IArtifactStore? Store { get; init; }

    /// <summary>
    /// The ledger of the session the attempt runs on, read when a download is registered; null (no session open yet) leaves every download
    /// as served. While it holds a value (<c>Secrecy.RedactsDownloads</c>), a text-like download is rewritten through it before it is hashed
    /// or stored.
    /// </summary>
    public Func<Redactor?>? Ledger { get; init; }

    /// <summary>Report identity handed to the store with each artifact.</summary>
    public ArtifactIdentity? Identity { get; init; }

    /// <summary>Cancels the store hand-offs and the measuring still running (.NET-only). The session leaves it unset: its own token ends before the artifacts settle, and settling is bounded by the cleanup timeout.</summary>
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>The artifacts of one attempt (upstream's <c>AttemptArtifacts</c>, <c>run/artifacts.ts</c>).</summary>
internal sealed class AttemptArtifacts
{
    private readonly AttemptArtifactsOptions _options;
    private readonly object _gate = new();
    private readonly List<ArtifactRecord> _records = [];
    private readonly List<Task> _pending = [];

    internal AttemptArtifacts(AttemptArtifactsOptions options)
    {
        _options = options;
        Dir = Path.Combine([options.ArtifactsRoot, .. options.Segments]);
        Directory.CreateDirectory(Dir);
        Sink = new FileSink(this);
    }

    /// <summary>Absolute attempt artifact directory, created eagerly.</summary>
    public string Dir { get; }

    /// <summary>Report-order artifact records; the sink appends to them. A snapshot: a record that is still being measured changes in a later one.</summary>
    public IReadOnlyList<ArtifactRecord> Records
    {
        get
        {
            lock (_gate)
            {
                return [.. _records];
            }
        }
    }

    public IArtifactSink Sink { get; }

    /// <summary>
    /// Completes once every registered file has been measured and hashed. Registration itself is synchronous and cheap; the size and digest
    /// of a file (a video can be tens of megabytes) are filled in off the caller's thread, so awaiting this before the record is read is
    /// what makes them complete.
    /// </summary>
    public async Task SettleAsync(CancellationToken cancellationToken = default)
    {
        // Registrations may land while earlier ones are still hashing.
        while (true)
        {
            Task[] batch;
            lock (_gate)
            {
                if (_pending.Count == 0)
                {
                    return;
                }

                batch = [.. _pending];
                _pending.Clear();
            }

            await Task.WhenAll(batch).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void Update(int index, Func<ArtifactRecord, ArtifactRecord> change)
    {
        lock (_gate)
        {
            _records[index] = change(_records[index]);
        }
    }

    private void Track(Task task)
    {
        lock (_gate)
        {
            _pending.Add(task);
        }
    }

    private string? StepId() => _options.CurrentStepId?.Invoke();

    private static ArtifactProducer ProducerOf(string? stepId) =>
        stepId is null ? new ArtifactProducer { Kind = ArtifactProducerKind.Attempt } : new ArtifactProducer { Kind = ArtifactProducerKind.Step, StepId = stepId };

    private string AttemptIdOf() => _options.Identity?.AttemptId ?? _options.AttemptId;

    private sealed class FileSink(AttemptArtifacts owner) : IArtifactSink
    {
        public string Dir => owner.Dir;

        public string Register(ArtifactKind kind, string relativePath, ArtifactRegistration? registration = null)
        {
            var options = owner._options;
            var absolute = Path.Combine(owner.Dir, relativePath);
            var stepId = owner.StepId();
            var startedAt = registration?.StartedAt;
            var mediaType = ArtifactPaths.MediaTypeFor(relativePath);
            var reportPath = string.Join('/', [.. options.Segments, relativePath]);
            var ledger = kind == ArtifactKind.Download ? options.Ledger?.Invoke() : null;
            int index;
            string id;
            lock (owner._gate)
            {
                index = owner._records.Count;
                id = options.AttemptId + ":artifact:" + index.ToString(CultureInfo.InvariantCulture);
                owner._records.Add(new ArtifactRecord
                {
                    Id = id,
                    Kind = kind,
                    MediaType = mediaType,
                    StartedAt = startedAt,
                    Redaction = ArtifactPaths.RedactionByKind(kind),
                    Producer = ProducerOf(stepId),
                });
            }

            owner.Track(owner.RunAsync(index, kind, absolute, reportPath, mediaType, stepId, startedAt, ledger));
            return id;
        }

        public string Link(string url, ArtifactLink link)
        {
            var options = owner._options;
            var stepId = owner.StepId();
            int index;
            string id;
            lock (owner._gate)
            {
                index = owner._records.Count;
                id = options.AttemptId + ":artifact:" + index.ToString(CultureInfo.InvariantCulture);
                owner._records.Add(new ArtifactRecord
                {
                    Id = id,
                    Kind = ArtifactKind.Video,
                    MediaType = link.MediaType,
                    Url = url,
                    StartedAt = link.StartedAt,
                    Redaction = ArtifactPaths.RedactionByKind(ArtifactKind.Video),
                    Producer = ProducerOf(stepId),
                });
            }

            if (options.Store is { } store)
            {
                owner.Track(owner.PutLinkAsync(store, index, url, link, stepId));
            }

            return id;
        }
    }

    // Runs on the registering thread up to its first await, so a download's redaction label is on its record when Register returns.
    private async Task RunAsync(int index, ArtifactKind kind, string absolute, string reportPath, string mediaType, string? stepId, DateTimeOffset? startedAt, Redactor? ledger)
    {
        var token = _options.CancellationToken;
        if (ledger is not null && Secrecy.RedactsDownloads(ledger) && ArtifactPaths.IsTextLike(mediaType))
        {
            var redaction = ArtifactPaths.RedactDownload(absolute, ledger);
            Update(index, record => record with { Redaction = redaction });
        }

        // Without a store the file is streamed for its size and digest only; with one it is read whole, since the store needs the bytes
        // anyway, and hashed from that same buffer. A file that never appeared is recorded without size or digest either way.
        if (_options.Store is not { } store)
        {
            if (await ArtifactPaths.MeasureAsync(absolute, token).ConfigureAwait(false) is not { } measured)
            {
                return;
            }

            Update(index, record => record with { Path = reportPath, Size = measured.Size, Sha256 = measured.Sha256 });
            return;
        }

        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(absolute, token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return;
        }

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        Update(index, record => record with { Path = reportPath, Size = bytes.LongLength, Sha256 = sha256 });
        ArtifactRedaction labelled;
        lock (_gate)
        {
            labelled = _records[index].Redaction;
        }

        // The store's failure is its own: the record keeps its local path and simply carries no ref, and the run is never failed by
        // evidence that did not upload.
        try
        {
            var reference = await store.PutAsync(
                new StoredArtifact
                {
                    Kind = kind,
                    MediaType = mediaType,
                    Bytes = bytes,
                    Size = bytes.LongLength,
                    Sha256 = sha256,
                    Path = reportPath,
                    Redaction = labelled,
                    RunId = _options.Identity?.RunId ?? "",
                    TestId = _options.Identity?.TestId ?? "",
                    AttemptId = AttemptIdOf(),
                    StepId = stepId,
                    StartedAt = startedAt,
                },
                token).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(reference))
            {
                Update(index, record => record with { Ref = reference });
            }
        }
#pragma warning disable CA1031 // Best-effort by contract: the store's failure never fails the run. A cancel of the caller still surfaces.
        catch (Exception) when (!token.IsCancellationRequested)
#pragma warning restore CA1031
        {
        }
    }

    // Best-effort like a put: the record keeps its URL either way.
    private async Task PutLinkAsync(IArtifactStore store, int index, string url, ArtifactLink link, string? stepId)
    {
        var token = _options.CancellationToken;
        try
        {
            var reference = await store.PutLinkAsync(
                new StoredArtifactLink
                {
                    Kind = E2E.Reports.ArtifactKind.Video,
                    Url = url,
                    MediaType = link.MediaType,
                    Redaction = ArtifactRedaction.Incomplete,
                    RunId = _options.Identity?.RunId ?? "",
                    TestId = _options.Identity?.TestId ?? "",
                    AttemptId = AttemptIdOf(),
                    StartedAt = link.StartedAt,
                    StepId = stepId,
                },
                token).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(reference))
            {
                Update(index, record => record with { Ref = reference });
            }
        }
#pragma warning disable CA1031 // Best-effort by contract: the store's failure never fails the run. A cancel of the caller still surfaces.
        catch (Exception) when (!token.IsCancellationRequested)
#pragma warning restore CA1031
        {
        }
    }
}
