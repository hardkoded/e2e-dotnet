// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using E2E.Internal;

namespace E2E;

/// <summary>
/// The wire form of the report document (upstream's report-1 shapes and <c>serialize*</c> functions in <c>report/build.ts</c>): the
/// <see cref="JsonObject"/> that <c>schema/report-v1.schema.json</c> describes. Upstream types the wire shapes and lets
/// <c>JSON.stringify</c> drop what is absent; the port builds the objects directly, which leaves out what is absent the same way.
/// Text is cut to the schema's bounds on code-unit boundaries that never split a surrogate pair.
/// </summary>
internal static partial class ReportWire
{
    /// <summary>What a step that names no line in the project reports as its source (upstream's <c>UNKNOWN_STEP_SOURCE</c>).</summary>
    private static readonly SourceLocation UnknownStepSource = new("unknown", 1, 1);

    /// <summary>The value of <c>observationRevision</c> when the port recorded none. Upstream requires one on a judgment step.</summary>
    internal const string UnrecordedRevision = "unrecorded";

    [GeneratedRegex(@"\A[a-f0-9]{64}\z")]
    private static partial Regex Sha256Pattern();

    [GeneratedRegex(@"\A[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\z")]
    private static partial Regex UuidV7Pattern();

    // The schema's relativePath: not rooted, no drive, no backslash, no dot segment, no empty segment.
    [GeneratedRegex(@"\A(?!/)(?![A-Za-z]:)(?!.*\\)(?!.*(?:\A|/)\.\.?(?:/|\z))(?!.*//)[^/\u0000]+(?:/[^/\u0000]+)*\z", RegexOptions.Singleline)]
    private static partial Regex RelativePathPattern();

    [GeneratedRegex(@"\A[^\s,](?:[^,]*[^\s,])?\z", RegexOptions.Singleline)]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"\S")]
    private static partial Regex NonBlank();

    /// <summary>A moment as the schema spells it: UTC, to the millisecond (upstream's <c>timestamp()</c>).</summary>
    public static string Timestamp(DateTimeOffset moment) => moment.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    /// <summary>Text cut to at most <paramref name="max"/> UTF-16 code units, never between the two halves of a surrogate pair.</summary>
    public static string Clip(string text, int max)
    {
        if (text.Length <= max)
        {
            return WellFormed(text);
        }

        var end = max;
        if (end > 0 && char.IsHighSurrogate(text[end - 1]) && char.IsLowSurrogate(text[end]))
        {
            end--;
        }

        return WellFormed(text[..end]);
    }

    /// <summary>The text with each lone surrogate replaced by U+FFFD: <c>JSON.stringify</c> escapes one, where the writer here refuses it.</summary>
    internal static string WellFormed(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            var unit = text[index];
            if (char.IsHighSurrogate(unit) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                index++;
            }
            else if (char.IsSurrogate(unit))
            {
                return string.Create(text.Length, text, static (span, source) =>
                {
                    for (var at = 0; at < source.Length; at++)
                    {
                        if (char.IsHighSurrogate(source[at]) && at + 1 < source.Length && char.IsLowSurrogate(source[at + 1]))
                        {
                            span[at] = source[at];
                            span[++at] = source[at];
                        }
                        else
                        {
                            span[at] = char.IsSurrogate(source[at]) ? '\uFFFD' : source[at];
                        }
                    }
                });
            }
        }

        return text;
    }

    private static string? ClipOrNull(string? text, int max) => string.IsNullOrEmpty(text) ? null : Clip(text, max);

    private static void Put(JsonObject target, string name, JsonNode? value)
    {
        if (value is not null)
        {
            target[name] = value;
        }
    }

    private static JsonArray Strings(IEnumerable<string> values, int max) => [.. values.Select(value => (JsonNode?)JsonValue.Create(Clip(value, max)))];

    private static string RelativePath(string path) => RelativePathPattern().IsMatch(path) ? path : "unknown";

    private static JsonObject Source(SourceLocation? source)
    {
        var where = source ?? UnknownStepSource;
        return new JsonObject { ["file"] = RelativePath(where.File), ["line"] = Math.Max(1, where.Line), ["column"] = Math.Max(1, where.Column) };
    }

    /// <summary>Upstream's <c>serializeErrorRecord</c>: the error without a stack, which never enters the report.</summary>
    public static JsonObject Error(ReportError error)
    {
        var result = new JsonObject
        {
            ["category"] = error.Category,
            ["code"] = error.Code.Length == 0 ? "ERROR" : Clip(error.Code, 128),
            ["message"] = Clip(error.Message, 8192),
            ["retryable"] = error.Retryable,
        };
        Put(result, "phase", error.Phase is "config" or "collection" or "launch" or "beforeAll" or "beforeEach" or "body" or "afterEach" or "afterAll" or "cleanup" or "report" ? error.Phase : null);
        Put(result, "source", error.Source is null ? null : Source(error.Source));
        if (error.Details is { } details)
        {
            var wire = new JsonObject();
            Put(wire, "locator", ClipOrNull(details.Locator, 2048));
            Put(wire, "expected", ClipOrNull(details.Expected, 2048));
            Put(wire, "observed", ClipOrNull(details.Observed, 2048));
            Put(wire, "matches", details.Matches);
            Put(wire, "role", ClipOrNull(details.Role, 2048));
            Put(wire, "name", ClipOrNull(details.Name, 2048));
            Put(wire, "testId", ClipOrNull(details.TestId, 2048));
            Put(wire, "waitedMs", details.WaitedMs);
            if (wire.Count > 0)
            {
                result["details"] = wire;
            }
        }

        return result;
    }

    private static JsonObject Event(StepEvent step) => Compose(
        ("kind", step.Kind.ToString().ToLowerInvariant()),
        ("startedAt", Timestamp(step.StartedAt)),
        ("durationMs", Math.Max(0, step.DurationMs)),
        ("status", step.Status.ToString().ToLowerInvariant()),
        ("name", ClipOrNull(step.Name, 256)),
        ("count", step.Count),
        ("bytes", step.Bytes),
        ("code", ClipOrNull(step.Code, 128)),
        ("detail", ClipOrNull(step.Detail, 300)));

    private static JsonObject Compose(params (string Name, object? Value)[] fields)
    {
        var result = new JsonObject();
        foreach (var (name, value) in fields)
        {
            Put(result, name, value switch
            {
                null => null,
                string text => JsonValue.Create(text),
                int number => JsonValue.Create(number),
                long number => JsonValue.Create(number),
                bool flag => JsonValue.Create(flag),
                double number => JsonValue.Create(number),
                JsonNode node => node,
                _ => throw new InvalidOperationException("not a wire value: " + value.GetType()),
            });
        }

        return result;
    }

    private static JsonObject Model(StepModelInfo model)
    {
        // IAgentModel.Name spells provider and model id together, so a step that names only the model is split at the first slash.
        var provider = model.Provider;
        var name = model.Model;
        if (provider is null && name is not null && name.IndexOf('/') is var slash and > 0)
        {
            provider = name[..slash];
            name = name[(slash + 1)..];
        }

        return Compose(
            ("provider", NonBlank().IsMatch(provider ?? "") ? Clip(provider!, 300) : "unknown"),
            ("model", NonBlank().IsMatch(name ?? "") ? Clip(name!, 300) : "unknown"),
            ("endpoint", "provider-default"),
            ("adapterVersion", ModelHttp.PackageVersion()),
            ("policyVersion", "unversioned"),
            ("calls", Math.Max(1, model.Calls)),
            ("tokenAccounting", "provider"),
            ("peakTokensPerCall", 0),
            ("inputTokens", Math.Max(0, model.InputTokens)),
            ("outputTokens", Math.Max(0, model.OutputTokens)),
            ("cacheReadTokens", model.CacheReadTokens),
            ("cacheWriteTokens", model.CacheWriteTokens),
            ("estimatedCostUsd", model.EstimatedCostUsd is { } cost && double.IsFinite(cost) && cost >= 0 ? cost : null));
    }

    private static JsonObject Cache(CacheInfo cache, CacheWrite? write)
    {
        var recordedBy = cache.RecordedBy is { } who && who.IndexOf('/') is var slash and > 0 && NonBlank().IsMatch(who[..slash]) && NonBlank().IsMatch(who[(slash + 1)..])
            ? new JsonObject { ["provider"] = Clip(who[..slash], 300), ["model"] = Clip(who[(slash + 1)..], 300) }
            : null;
        return Compose(
            ("mode", cache.Mode),
            ("recordedBy", recordedBy),
            ("reason", cache.Reason),
            ("notRecorded", cache.NotRecorded),
            ("replayedActions", Math.Max(0, cache.ReplayedActions)),
            ("totalActions", Math.Max(0, cache.TotalActions)),
            ("entry", cache.Entry is { } entry && Sha256Pattern().IsMatch(entry) ? entry : null),
            ("detail", ClipOrNull(cache.Detail, 600)),
            ("write", write switch
            {
                CacheWrite.Saved => "saved",
                CacheWrite.Kept => "kept",
                CacheWrite.Unconfirmed => "unconfirmed",
                CacheWrite.Evicted => "evicted",
                CacheWrite.NoChange => "no-change",
                CacheWrite.NotWritten => "not-written",
                _ => null,
            }));
    }

    private static JsonObject Metrics(StepMetrics? metrics) => Compose(
        ("modelCalls", Math.Max(0, metrics?.ModelCalls ?? 0)),
        ("actionSteps", Math.Max(0, metrics?.ActionSteps ?? 0)),
        ("observationBytes", Math.Max(0, metrics?.ObservationBytes ?? 0)),
        ("contextBytes", Math.Max(0, metrics?.ContextBytes ?? 0)),
        ("ledgerBytes", Math.Max(0, metrics?.LedgerBytes ?? 0)));

    /// <summary>Upstream's <c>serializeStep</c>.</summary>
    public static JsonObject Step(StepRecord step)
    {
        var result = Compose(
            ("id", step.Id),
            ("index", step.Index),
            ("kind", StepNames.Of(step.Kind)),
            ("api", step.Api),
            ("label", Clip(step.Label, 8192)),
            ("source", Source(step.Source)),
            ("phase", step.Phase switch { StepPhase.BeforeEach => "beforeEach", StepPhase.AfterEach => "afterEach", _ => null }),
            ("screen", step.Screen is { } screen ? Screen(screen) : null),
            ("status", StepNames.Of(step.Status)),
            ("startedAt", Timestamp(step.StartedAt)),
            ("durationMs", Math.Max(0, step.DurationMs)));

        // A judgment names the observation it judged and says why; the port records no revision, so it says so.
        if (step.Api == "agent.assert" && (step.Status == StepStatus.Passed || step.Metrics?.ModelCalls >= 1))
        {
            result["observationRevision"] = UnrecordedRevision;
            result["explanation"] = Clip(step.Explanation ?? "", 8192);
        }
        else if (step.Explanation is { } explanation)
        {
            result["explanation"] = Clip(explanation, 8192);
        }

        if (step.Viewport is { Width: >= 1, Height: >= 1, Scale: > 0 } viewport && double.IsFinite(viewport.Scale))
        {
            result["viewport"] = new JsonObject { ["width"] = viewport.Width, ["height"] = viewport.Height, ["scale"] = viewport.Scale };
        }

        if (step.Kind == StepKind.Agent || step.Metrics is not null)
        {
            result["metrics"] = Metrics(step.Metrics);
        }

        Put(result, "cache", step.Cache is null ? null : Cache(step.Cache, step.CacheWrite));
        result["events"] = new JsonArray([.. step.Events.Select(each => (JsonNode?)Event(each))]);
        Put(result, "turns", step.Turns is { Count: > 0 } turns
            ? new JsonArray([.. turns.TakeLast(32).Select(turn => (JsonNode?)Compose(("index", Math.Max(1, turn.Index)), ("calls", Strings(turn.Calls.Take(16), 512)), ("outcome", Clip(turn.Outcome, 2048))))])
            : null);
        Put(result, "model", step.Model is null ? null : Model(step.Model));
        Put(result, "agent", string.IsNullOrEmpty(step.Agent) ? null : step.Agent);
        Put(result, "error", step.Error is null ? null : Error(ReportError.Of(step.Error)));
        result["artifacts"] = Strings(step.Artifacts.Where(id => id.Length > 0), int.MaxValue);
        return result;
    }

    private static JsonObject Screen(StepScreen screen) => Compose(
        ("location", ClipOrNull(screen.Location, int.MaxValue)),
        ("nodes", Math.Max(0, screen.Nodes)),
        ("since", screen.Since is { } since ? Math.Max(0, since) : null),
        ("changes", Strings(screen.Changes.Take(12), 200)),
        ("more", screen.More is >= 1 ? screen.More : null));

    private static JsonObject AppLog(AppLogRecord line) => Compose(
        ("source", line.Source.ToString().ToLowerInvariant()),
        ("level", line.Level.ToString().ToLowerInvariant()),
        ("text", Clip(line.Text, 300)),
        ("at", Timestamp(line.At)),
        ("step", line.Step is { } step ? Math.Max(0, step) : null));

    private static JsonObject Failure(FailureEvidence evidence)
    {
        var result = new JsonObject();
        Put(result, "url", ClipOrNull(evidence.Url, 2048));
        if (evidence.Viewport is { } viewport)
        {
            result["viewport"] = new JsonObject { ["width"] = Math.Max(0, viewport.Width), ["height"] = Math.Max(0, viewport.Height) };
        }

        Put(result, "nodes", evidence.Nodes is { } nodes ? Math.Max(0, nodes) : null);
        Put(result, "screen", ClipOrNull(evidence.Screen, 128));
        Put(result, "screenshot", ClipOrNull(evidence.Screenshot, 128));
        Put(result, "candidates", evidence.Candidates is { } candidates ? Strings(candidates.Take(8), 1024) : null);
        return result;
    }

    /// <summary>
    /// An artifact as the report keeps it. The size and the digest are read from the file under <paramref name="resultsRoot"/>, which is
    /// where <see cref="ReportArtifact.Path"/> starts; a file that cannot be read leaves the artifact <c>incomplete</c> without them. Pixels
    /// cannot be proven free of a secret, so only a log reads <c>complete</c>.
    /// </summary>
    public static JsonObject Artifact(ReportArtifact artifact, string resultsRoot, string? stepId)
    {
        long? size = null;
        string? digest = null;
        var path = artifact.Path is { } candidate && RelativePathPattern().IsMatch(candidate) ? candidate : null;
        if (path is not null)
        {
            try
            {
                var file = Path.Combine(resultsRoot, path.Replace('/', Path.DirectorySeparatorChar));
                using var stream = File.OpenRead(file);
                size = stream.Length;
                digest = Convert.ToHexStringLower(SHA256.HashData(stream));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                path = null;
            }
        }

        var kind = artifact.Kind is "screenshot" or "video" or "download" or "log" ? artifact.Kind : "other";
        var complete = kind == "log" && path is not null;
        return Compose(
            ("id", artifact.Id),
            ("kind", kind),
            ("mediaType", MediaType(kind, artifact.Path ?? artifact.Url)),
            ("path", path),
            ("url", artifact.Url is { } url && Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Scheme is "http" or "https" && url.Length <= 2048 ? url : null),
            ("size", size ?? artifact.Size),
            ("sha256", digest),
            ("startedAt", artifact.StartedAt is { } startedAt ? Timestamp(startedAt) : null),
            ("redaction", complete ? "complete" : "incomplete"),
            ("producer", stepId is null ? new JsonObject { ["kind"] = "attempt" } : new JsonObject { ["kind"] = "step", ["stepId"] = stepId }));
    }

    private static string MediaType(string kind, string? name)
    {
        var extension = Path.GetExtension(name?.Split('?', '#')[0] ?? "").ToLowerInvariant();
        return kind switch
        {
            "video" => extension == ".mp4" ? "video/mp4" : "video/webm",
            "screenshot" => extension is ".jpg" or ".jpeg" ? "image/jpeg" : "image/png",
            "log" => "text/plain",
            _ => "application/octet-stream",
        };
    }

    private static string AttemptStatusName(AttemptStatus status) => status switch
    {
        AttemptStatus.Passed => "passed",
        AttemptStatus.TimedOut => "timed-out",
        AttemptStatus.Interrupted => "interrupted",
        AttemptStatus.Skipped => "skipped",
        _ => "failed",
    };

    private static string ResultStatusName(ResultStatus status) => status switch
    {
        ResultStatus.Passed => "passed",
        ResultStatus.Flaky => "flaky",
        ResultStatus.TimedOut => "timed-out",
        ResultStatus.Interrupted => "interrupted",
        ResultStatus.Skipped => "skipped",
        _ => "failed",
    };

    private static JsonObject Skip(ReportSkip skip) => new() { ["cause"] = skip.Cause, ["reason"] = Clip(skip.Reason, 8192) };

    /// <summary>Upstream's <c>serializeAttempt</c>.</summary>
    public static JsonObject Attempt(ReportAttempt attempt, int index, string resultsRoot)
    {
        var id = UuidV7Pattern().IsMatch(attempt.Id) ? attempt.Id : Guid.CreateVersion7().ToString();
        var skipped = attempt.Status == AttemptStatus.Skipped;
        var result = Compose(
            ("id", id),
            ("index", index),
            ("status", AttemptStatusName(attempt.Status)),
            ("startedAt", Timestamp(attempt.StartedAt ?? attempt.Steps.FirstOrDefault()?.StartedAt ?? DateTimeOffset.UnixEpoch)),
            ("durationMs", Math.Max(0, attempt.DurationMs)));
        result["steps"] = new JsonArray([.. attempt.Steps.Select(step => (JsonNode?)Step(step))]);
        if (attempt.AppLog.Count > 0)
        {
            result["appLog"] = new JsonArray([.. attempt.AppLog.Take(300).Select(line => (JsonNode?)AppLog(line))]);
        }

        var stepOf = attempt.Steps.SelectMany(step => step.Artifacts.Select(artifact => (artifact, step.Id))).GroupBy(pair => pair.artifact).ToDictionary(group => group.Key, group => group.First().Id);
        result["artifacts"] = new JsonArray([.. attempt.Artifacts.Select(artifact => (JsonNode?)Artifact(artifact, resultsRoot, stepOf.GetValueOrDefault(artifact.Id)))]);

        // A skipped attempt says why it skipped; the exception that carried the reason is not an error of the run.
        if (skipped)
        {
            result["skip"] = Skip(attempt.Skip ?? new ReportSkip("explicit", attempt.Error?.Message ?? "skipped"));
        }
        else
        {
            Put(result, "error", attempt.Error is null ? null : Error(attempt.Error));
            Put(result, "skip", attempt.Skip is null || attempt.Status == AttemptStatus.Passed ? null : Skip(attempt.Skip));
        }

        Put(result, "failure", attempt.Failure is null ? null : Failure(attempt.Failure));
        result["secondaryErrors"] = new JsonArray([.. attempt.SecondaryErrors.Select(error => (JsonNode?)Error(error))]);
        result["cleanup"] = attempt.Cleanup switch { AttemptCleanup.Failed => "failed", AttemptCleanup.Forced => "forced", _ => "complete" };
        if (attempt.Environment is { Count: > 0 } environment)
        {
            var wire = new JsonObject();
            foreach (var (name, value) in environment.Where(pair => pair.Key.Length is > 0 and <= 40).Take(8))
            {
                wire[WellFormed(name)] = Clip(value, 200);
            }

            Put(result, "environment", wire.Count > 0 ? wire : null);
        }

        return result;
    }

    /// <summary>Upstream's <c>serializeResult</c>. <paramref name="platform"/> is the platform of the result's target when the result names none.</summary>
    public static JsonObject Result(ReportResult result, string platform, string resultsRoot)
    {
        var title = result.TitlePath.Count == 0 ? "(untitled)" : string.Join(" ", result.TitlePath);
        var skipped = result.Status == ResultStatus.Skipped;
        var wire = Compose(
            ("id", Sha256Pattern().IsMatch(result.Id) ? result.Id : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(result.Id)))),
            ("testId", AttemptReport.TestId(result.File, title)),
            ("kind", "test"),
            ("declarationIndex", Math.Max(0, result.DeclarationIndex)),
            ("titlePath", Strings(result.TitlePath.Count == 0 ? ["(untitled)"] : result.TitlePath.Select(part => part.Length == 0 ? "(untitled)" : part), int.MaxValue)),
            ("file", RelativePath(result.File)),
            ("source", Source(result.Source)),
            ("targetId", result.TargetId),
            ("platform", platform),
            ("agent", result.Agent),
            ("repeat", Math.Max(0, result.Repeat)),
            ("tags", Strings(result.Tags.Where(tag => TagPattern().IsMatch(tag)).Distinct(StringComparer.Ordinal), int.MaxValue)),
            ("selected", result.Selected),
            ("status", ResultStatusName(result.Status)));
        if (skipped)
        {
            var reason = result.Attempts.LastOrDefault(attempt => attempt.Status == AttemptStatus.Skipped);
            wire["skip"] = Skip(reason?.Skip ?? new ReportSkip("explicit", reason?.Error?.Message ?? "skipped"));
        }

        wire["attempts"] = new JsonArray([.. result.Attempts.Select((attempt, index) => (JsonNode?)Attempt(attempt, index, resultsRoot))]);
        return wire;
    }

    private static JsonObject Target(ReportTarget target) => Compose(
        ("id", target.Id),
        ("index", Math.Max(0, target.Index)),
        ("platform", target.Platform),
        ("baseOrigin", target.BaseOrigin),
        ("environment", target.Environment),
        ("engine", Compose(("name", target.Provenance.Engine.Name), ("version", target.Provenance.Engine.Version), ("spiVersion", target.Provenance.Engine.SpiVersion))),
        ("capabilities", Strings(target.Provenance.Capabilities.Distinct(StringComparer.Ordinal), int.MaxValue)),
        ("artifactCapabilities", Strings(target.Provenance.ArtifactCapabilities.Distinct(StringComparer.Ordinal), int.MaxValue)),
        ("stateCapability", target.Provenance.StateCapability));

    internal static JsonObject Explore(ReportExplore explore) => Compose(
        ("goal", Clip(explore.Goal.Length == 0 ? "exploration" : explore.Goal, 2000)),
        ("budgets", new JsonObject { ["maxSteps"] = Math.Max(1, explore.Budgets.MaxSteps), ["timeoutMs"] = (long)Math.Max(1, explore.Budgets.Timeout.TotalMilliseconds) }),
        ("ended", explore.Ended switch { ReportExploreEnded.StepLimit => "step-limit", var other => other.ToString().ToLowerInvariant() }),
        ("summary", ClipOrNull(explore.Summary, 4000)),
        ("steps", new JsonArray([.. explore.Steps.Select(step => (JsonNode?)Compose(
            ("index", Math.Max(1, step.Index)),
            ("title", Clip(step.Title.Length == 0 ? "step" : step.Title, 200)),
            ("instruction", Clip(step.Instruction.Length == 0 ? "step" : step.Instruction, 2000)),
            ("status", step.Status.ToString().ToLowerInvariant()),
            ("summary", ClipOrNull(step.Summary, 4000)),
            ("errorCode", string.IsNullOrEmpty(step.ErrorCode) ? null : step.ErrorCode),
            ("startedAt", Timestamp(step.StartedAt)),
            ("durationMs", (long)Math.Max(0, step.Duration.TotalMilliseconds))))])),
        ("findings", new JsonArray([.. explore.Findings.Select(finding => (JsonNode?)Compose(
            ("id", finding.Id),
            ("index", Math.Max(0, finding.Index)),
            ("step", finding.Step is >= 1 ? finding.Step : null),
            ("kind", finding.Kind.ToString().ToLowerInvariant()),
            ("severity", Math.Clamp(finding.Severity, 1, 5)),
            ("title", Clip(finding.Title.Length == 0 ? "finding" : finding.Title, 200)),
            ("expected", Clip(finding.Expected, 2000)),
            ("actual", Clip(finding.Actual, 2000)),
            ("reproduction", Strings(finding.Reproduction.Take(20), 500)),
            ("path", ClipOrNull(finding.Path, 2048)),
            ("reportedAt", Timestamp(finding.ReportedAt))))])));

    /// <summary>The document with every result serialized now, for a caller that keeps none.</summary>
    public static JsonObject Document(Report1Document document, string resultsRoot) =>
        Document(document, document.Run.Results.ToDictionary(result => result.Id, result => (JsonNode)Result(result, result.Platform.Length > 0 ? result.Platform : "web", resultsRoot), StringComparer.Ordinal));

    /// <summary>
    /// The document as the schema spells it. <paramref name="results"/> holds the wire form of each result by id, already redacted, so a
    /// run of many tests does not serialize every result again after each attempt.
    /// </summary>
    public static JsonObject Document(Report1Document document, IReadOnlyDictionary<string, JsonNode> results, string? runnerName = null)
    {
        var run = document.Run;
        var wire = Compose(
            ("id", UuidV7Pattern().IsMatch(run.Id) ? run.Id : Guid.CreateVersion7().ToString()),
            ("specVersion", "0.1"),
            ("runner", new JsonObject { ["name"] = runnerName ?? "e2e", ["version"] = run.RunnerVersion.Length == 0 ? "0.0.0" : run.RunnerVersion }),
            ("status", (run.Status ?? ReportRunStatus.Passed).ToString().ToLowerInvariant()),
            ("exitCode", run.ExitCode ?? 0),
            ("startedAt", Timestamp(run.StartedAt ?? DateTimeOffset.UnixEpoch)),
            ("finishedAt", run.FinishedAt is { } finished ? Timestamp(finished) : null),
            ("project", new JsonObject { ["id"] = Clip(run.Project.Id, 256), ["configDigest"] = run.Project.ConfigDigest }),
            ("environment", run.Environment is { } environment
                ? new JsonObject { ["ci"] = environment.Ci, ["trustNoticeShown"] = environment.TrustNoticeShown, ["os"] = environment.Os, ["arch"] = environment.Arch, ["runtime"] = environment.Runtime }
                : new JsonObject { ["ci"] = false, ["trustNoticeShown"] = false, ["os"] = "unknown", ["arch"] = "unknown", ["runtime"] = "unknown" }),
            ("vcs", run.Vcs is { } vcs && Git.CommitHash.IsMatch(vcs.Commit)
                ? Compose(("commit", vcs.Commit), ("branch", ClipOrNull(vcs.Branch, 512)), ("dirty", vcs.Dirty))
                : null));
        wire["targets"] = new JsonArray([.. run.Targets.Select(target => (JsonNode?)Target(target))]);
        wire["serialGroups"] = new JsonArray();
        wire["results"] = new JsonArray([.. run.Results.Select(result => results[result.Id].DeepClone())]);
        wire["errors"] = new JsonArray([.. run.Errors.Select(error => (JsonNode?)Error(error))]);
        var summary = run.Summary ?? BuildReport.ComputeSummary(run.Results);
        wire["summary"] = Compose(
            ("discovered", summary.Discovered),
            ("selected", summary.Selected),
            ("executed", summary.Executed),
            ("passed", summary.Passed),
            ("failed", summary.Failed),
            ("interrupted", summary.Interrupted),
            ("flaky", summary.Flaky),
            ("skipped", summary.Skipped));
        var limits = run.Limits ?? ReportLimits.Default;
        wire["limits"] = Compose(
            ("maxAgentContextBytes", limits.MaxAgentContextBytes),
            ("maxLedgerBytes", limits.MaxLedgerBytes),
            ("maxObservationBytes", limits.MaxObservationBytes),
            ("maxEventsPerStep", limits.MaxEventsPerStep),
            ("maxModelTokensPerCall", limits.MaxModelTokensPerCall));
        var usage = run.Usage ?? BuildReport.ComputeUsage(run.Results, summary.Discovered);
        wire["usage"] = Compose(
            ("discoveredResults", usage.DiscoveredResults),
            ("maxAgentContextBytes", usage.MaxAgentContextBytes),
            ("maxLedgerBytes", usage.MaxLedgerBytes),
            ("maxObservationBytes", usage.MaxObservationBytes),
            ("artifactBytes", usage.ArtifactBytes),
            ("downloads", usage.Downloads),
            ("events", usage.Events),
            ("modelTokens", usage.ModelTokens),
            ("modelCachedTokens", usage.ModelCachedTokens),
            ("maxModelCallsInStep", usage.MaxModelCallsInStep),
            ("maxActionStepsInStep", usage.MaxActionStepsInStep),
            ("estimatedCostUsd", usage.EstimatedCostUsd));
        Put(wire, "explore", run.Explore is null ? null : Explore(run.Explore));
        return new JsonObject { ["schemaVersion"] = "report-1", ["run"] = wire };
    }

    /// <summary>Replaces each secret in every text of <paramref name="node"/> by <paramref name="redact"/>, in place.</summary>
    public static void Redact(JsonNode node, Func<string, string> redact)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var name in obj.Select(pair => pair.Key).ToList())
                {
                    switch (obj[name])
                    {
                        case JsonValue value when value.TryGetValue<string>(out var text):
                            obj[name] = WellFormed(redact(text));
                            break;
                        case JsonNode child:
                            Redact(child, redact);
                            break;
                    }
                }

                break;
            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    switch (array[index])
                    {
                        case JsonValue value when value.TryGetValue<string>(out var text):
                            array[index] = WellFormed(redact(text));
                            break;
                        case JsonNode child:
                            Redact(child, redact);
                            break;
                    }
                }

                break;
        }
    }
}
