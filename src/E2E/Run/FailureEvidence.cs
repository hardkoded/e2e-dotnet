// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>What <see cref="FailureEvidence.CaptureAsync"/> looks at, and where it keeps what it sees. Upstream's <c>FailureEvidenceOptions</c>.</summary>
internal sealed record FailureEvidenceOptions
{
    /// <summary>One more look at the screen. Upstream's <c>session.observe</c>.</summary>
    public required Func<CancellationToken, Task<Observation>> Observe { get; init; }

    /// <summary>The engine's masked screenshot; null when it captures none. Upstream's <c>session.artifacts.screenshot</c>.</summary>
    public required Func<CancellationToken, Task<EngineScreenshot?>> Capture { get; init; }

    /// <summary>The failure of the attempt, whose details say what a locator asked for.</summary>
    public required Exception Error { get; init; }

    /// <summary>Redacts the screen and the query it is compared with.</summary>
    public required Redactor Redactor { get; init; }

    /// <summary>A secret was filled in the attempt: the app may show it outside a secure field, so no pixels are kept.</summary>
    public bool WithholdsPixels { get; init; }

    /// <summary>The app base URL's origin, whose link targets the screen lists as paths.</summary>
    public string? AppOrigin { get; init; }

    /// <summary>The attempt's artifact directory, where the screen and the screenshot go.</summary>
    public required string AttemptDir { get; init; }

    /// <summary>Bytes of text the screen lists before it is cut.</summary>
    public int MaxObservationBytes { get; init; } = StepScreens.DefaultObservationBytes;
}

/// <summary>
/// What the runner saw the moment a failure landed (upstream <c>run/failure-evidence.ts</c>). A failure message says what was asked; it
/// cannot say what the screen held instead. While the session is still open, the runner looks once more and keeps what it sees: the
/// redacted screen as <c>#id role "name" …</c> lines, a masked screenshot when pixels are allowed, the location, and for a locator that
/// matched nothing, the nodes closest to what it asked for. Everything here is best effort and bounded in time: evidence never turns a
/// failure into a different failure, and never fails a passing cleanup.
/// </summary>
internal sealed partial record FailureEvidence
{
    /// <summary>What the screen file of a failed attempt is called, beside the attempt's other files.</summary>
    internal const string ScreenFile = "screen-at-failure.txt";

    /// <summary>Where the screenshot of a failed attempt goes, under the attempt's directory.</summary>
    internal const string ScreenshotFile = "screenshots/failure.png";

    /// <summary>The ids of the artifacts the evidence names.</summary>
    internal const string ScreenArtifactId = "screen-at-failure";

    internal const string ScreenshotArtifactId = "screenshot-at-failure";

    /// <summary>The whole capture, observation and screenshot together, gets this long.</summary>
    private static readonly TimeSpan EvidenceTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Screen lines a locator failure lists as its nearest nodes, and the bytes each keeps; the page caps both.</summary>
    private const int MaxCandidates = 5;

    private const int MaxCandidateBytes = 1024;

    /// <summary>Longer tokens are ids or hashes, not words a locator names; comparing them would cost an edit-distance matrix each.</summary>
    private const int MaxNearWordLength = 24;

    private const int MaxUrlBytes = 2048;

    private const string TruncationMarker = "[observation truncated at the resolved observation byte limit]";

    private const string EngineTruncationMarker = "[observation truncated: the engine stopped listing nodes at its limit]";

    // The codes of a locator that matched nothing or too much: the port's, and upstream's that an agent action raises.
    private static readonly HashSet<string> LocatorCodes = new(["NOT_FOUND", "STRICT_MODE", "TIMEOUT", "LOCATOR_NOT_FOUND", "LOCATOR_AMBIGUOUS"], StringComparer.Ordinal);

    private static readonly Regex NonWord = new(@"[^\p{L}\p{N}]+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));

    /// <summary>
    /// Captures the evidence of one failed attempt, or as much of it as the session yields within the budget. Null when nothing was
    /// captured, so the record carries no empty block. Upstream's <c>captureFailureEvidence</c>.
    /// </summary>
    public static async Task<FailureEvidence?> CaptureAsync(FailureEvidenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        using var budget = new CancellationTokenSource(EvidenceTimeout);
        var token = budget.Token;
        var evidence = new FailureEvidence();

        // The engine is handed the budget, and the wait is abandoned at its end either way: an engine stuck on a hung app may never honor it.
        Observation? observation = null;
        try
        {
            observation = await options.Observe(token).WaitAsync(token).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Evidence is best effort: the session may be gone with the failure (a crashed page, a closed app).
        catch (Exception)
#pragma warning restore CA1031
        {
        }

        if (observation is not null)
        {
            evidence = evidence with { Url = TextRules.TruncateUtf8(options.Redactor.Redact(observation.Route), MaxUrlBytes) };
            var (lines, nodes) = Listing(observation, options);
            evidence = evidence with { Nodes = nodes };
            try
            {
                await TracePages.WriteTextAsync(Path.Combine(options.AttemptDir, ScreenFile), string.Join('\n', lines) + "\n", CancellationToken.None).ConfigureAwait(false);
                evidence = evidence with { Screen = ScreenArtifactId };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // An unwritable directory: the tree stays unrecorded.
            }

            var candidates = LocatorCandidates(options.Error, observation, options.Redactor, options.AppOrigin);
            if (candidates.Count > 0)
            {
                evidence = evidence with { Candidates = candidates };
            }
        }

        if (!token.IsCancellationRequested && !options.WithholdsPixels)
        {
            evidence = await WithScreenshotAsync(evidence, observation, options, token).ConfigureAwait(false);
        }

        return evidence == new FailureEvidence() ? null : evidence;
    }

    // A screenshot the engine could not take is not evidence the report claims. One whose masking cannot be proven holds a secret the tree does not.
    private static async Task<FailureEvidence> WithScreenshotAsync(FailureEvidence evidence, Observation? observation, FailureEvidenceOptions options, CancellationToken token)
    {
        try
        {
            var shot = await options.Capture(token).WaitAsync(token).ConfigureAwait(false);
            var secure = observation is null ? 0 : LocatorResolver.Walk(observation.Roots).Count(node => node.States.Secure);
            if (shot is null || shot.MaskedRegionCount is not { } masked || masked < secure)
            {
                return evidence;
            }

            var file = Path.Combine(options.AttemptDir, ScreenshotFile.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllBytesAsync(file, shot.Png, CancellationToken.None).ConfigureAwait(false);
            return evidence with { Screenshot = ScreenshotArtifactId, Viewport = ViewportOf(shot) };
        }
#pragma warning disable CA1031 // Evidence is best effort.
        catch (Exception)
#pragma warning restore CA1031
        {
            return evidence;
        }
    }

    // The engine reports no viewport, so it is the size of the picture in viewport pixels.
    private static StepViewport? ViewportOf(EngineScreenshot shot)
    {
        ReadOnlySpan<byte> header = shot.Png;
        if (header.Length < 24 || shot.Scale <= 0 || !header[1..4].SequenceEqual("PNG"u8))
        {
            return null;
        }

        var width = BinaryPrimitives.ReadUInt32BigEndian(header[16..]);
        var height = BinaryPrimitives.ReadUInt32BigEndian(header[20..]);
        return new StepViewport((int)Math.Round(width / shot.Scale), (int)Math.Round(height / shot.Scale), shot.Scale);
    }

    // The screen as the agent reads it, one node per line, up to the budget, ending with the marker that says it was cut. A node the page
    // does not show is left out and its children take its place, as the lines the model reads leave it out.
    private static (List<string> Lines, int Nodes) Listing(Observation observation, FailureEvidenceOptions options)
    {
        var lines = new List<string>();
        var bytes = 0;
        var cut = false;
        var markerBytes = Encoding.UTF8.GetByteCount(TruncationMarker) + 1;
        var budget = Math.Max(0, options.MaxObservationBytes - markerBytes);

        void Emit(SemanticNode node, int depth)
        {
            if (cut)
            {
                return;
            }

            var shown = !(node.States.Hidden || node.States.AriaHidden);
            if (shown)
            {
                var line = SnapshotText.FormatNode(node, depth, options.Redactor, options.AppOrigin);
                var size = Encoding.UTF8.GetByteCount(line) + 1;
                if (lines.Count > 0 && bytes + size > budget)
                {
                    cut = true;
                    return;
                }

                bytes += size;
                lines.Add(line);
            }

            foreach (var child in node.Children)
            {
                Emit(child, shown ? depth + 1 : depth);
            }
        }

        foreach (var root in observation.Roots)
        {
            Emit(root, 0);
        }

        var nodes = lines.Count;
        if (cut)
        {
            lines.Add(TruncationMarker);
        }
        else if (observation.Truncated)
        {
            lines.Add(EngineTruncationMarker);
        }

        return (lines, nodes);
    }

    /// <summary>
    /// For a locator that matched nothing or too much, the nodes on screen closest to what it asked for: the same role, a name sharing
    /// words (or near misses of them) with the one requested, or the test id. Rendered as the screen lists them, so the reader can
    /// rewrite the locator from what is there.
    /// </summary>
    internal static List<string> LocatorCandidates(Exception error, Observation observation, Redactor redactor, string? appOrigin)
    {
        if (error is not E2EException { Details: { } details } failure || !LocatorCodes.Contains(failure.Code))
        {
            return [];
        }

        // The nodes are redacted; the query is compared in the same form, so a secret test id still finds its node.
        var role = details.Role is null ? null : redactor.Redact(details.Role);
        var testId = details.TestId is null ? null : redactor.Redact(details.TestId);
        var words = Tokens(details.Name is null ? "" : redactor.Redact(details.Name));
        if (role is null && testId is null && words.Count == 0)
        {
            return [];
        }

        var scored = new List<(int Score, SemanticNode Node)>();
        foreach (var node in LocatorResolver.Walk(observation.Roots))
        {
            var nodeTestId = node.TestId is null ? null : redactor.Redact(node.TestId);
            var roleMatched = role is not null && string.Equals(node.Role, role, StringComparison.OrdinalIgnoreCase);
            var testIdMatched = testId is not null && nodeTestId is not null && (nodeTestId == testId || nodeTestId.Contains(testId, StringComparison.Ordinal) || testId.Contains(nodeTestId, StringComparison.Ordinal));
            var placeholder = node.Placeholder ?? (node.Attributes.TryGetValue("placeholder", out var attribute) ? attribute : null);
            var own = Tokens(redactor.Redact((node.Name ?? "") + " " + (node.Text ?? "") + " " + (placeholder ?? ""))).ToHashSet(StringComparer.Ordinal);
            var exact = words.Count(own.Contains);
            var roleEligible = role is null || roleMatched;
            var near = roleEligible ? words.Count(word => !own.Contains(word) && own.Any(other => NearWord(word, other))) : 0;

            // A request that named the node is answered by nodes of the asked role (any role, when none was asked) sharing a word, or a
            // near miss of one, with the name; a role alone is enough only when no name was asked for; a test id stands on its own either way.
            var nameMatched = exact + near > 0 && roleEligible;
            if (!(nameMatched || testIdMatched || (roleMatched && words.Count == 0)))
            {
                continue;
            }

            var score = (roleMatched ? 3 : 0)
                + (testIdMatched ? (nodeTestId == testId ? 6 : 3) : 0)
                + (exact * 2)
                + near
                + (words.Count > 0 && exact == words.Count ? 2 : 0);
            scored.Add((score, node));
        }

        return [.. scored
            .OrderByDescending(entry => entry.Score)
            .Take(MaxCandidates)
            .Select(entry => TextRules.TruncateUtf8(SnapshotText.FormatNode(entry.Node, 0, redactor, appOrigin), MaxCandidateBytes))];
    }

    // Whether a word on screen is a near miss for one asked for: a typo or an inflection of it (`Notes` for `Note`, `Submit` for
    // `Sumbit`), by the budget "did you mean" uses. Words under four letters or over MaxNearWordLength only match exactly, so `and`
    // never answers `add`.
    private static bool NearWord(string asked, string seen)
    {
        static bool Eligible(string word) => word.Length is >= 4 and <= MaxNearWordLength;
        return Eligible(asked) && Eligible(seen) && Suggestions.IsTypoOf(asked, seen);
    }

    private static List<string> Tokens(string text) =>
        [.. NonWord.Split(text.ToLowerInvariant()).Where(word => word.Length > 1)];
}
