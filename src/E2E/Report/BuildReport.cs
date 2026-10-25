// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using E2E.Engine;
using E2E.Internal;
using VcsInfo = E2E.Reports.VcsInfo;

namespace E2E;

/// <summary>What <see cref="BuildReport.Build"/> reads (upstream's <c>BuildReportOptions</c>). The port has no resolved config, so the pieces of it the report reads are here.</summary>
internal sealed record BuildReportOptions
{
    public required string RunId { get; init; }

    /// <summary>The project's root: sources are reported relative to it. Null keeps them as they are.</summary>
    public string? ProjectRoot { get; init; }

    public IReadOnlyList<TargetConfig> Targets { get; init; } = [];

    /// <summary>
    /// Targets already described, for a session that knows its engine and its app but holds no <see cref="TargetConfig"/> (.NET-only). They
    /// come after <see cref="Targets"/>, and a name in both reads as the config's.
    /// </summary>
    public IReadOnlyList<ReportTarget> ReportTargets { get; init; } = [];

    public required DateTimeOffset StartedAt { get; init; }

    public required ReportRunStatus Status { get; init; }

    public required int ExitCode { get; init; }

    public IReadOnlyList<ReportResult> Results { get; init; } = [];

    public IReadOnlyList<ReportError> RunErrors { get; init; } = [];

    /// <summary>What each target's engine declares, by target name. A target missing here reads as having no engine.</summary>
    public IReadOnlyDictionary<string, TargetProvenance> TargetProvenance { get; init; } = new Dictionary<string, TargetProvenance>();

    /// <summary>The exploration record of an exploration run; null for a test run.</summary>
    public ReportExplore? Explore { get; init; }

    /// <summary>The commit the code under test came from; null outside git and CI.</summary>
    public VcsInfo? Vcs { get; init; }

    /// <summary>The change the test filters judged, or why none ran; null when the run has no filters to apply (upstream's <c>changes</c>).</summary>
    public ReportChanges? Changes { get; init; }

    /// <summary>The test filters the run applied, in order; written only when there is one (upstream's <c>filters</c>).</summary>
    public IReadOnlyList<ReportFilter> Filters { get; init; } = [];

    public bool Ci { get; init; }

    /// <summary>When the run finished; the time of the build when null (upstream's <c>timestamp()</c>).</summary>
    public DateTimeOffset? FinishedAt { get; init; }
}

/// <summary>
/// The run-level blocks of the report document (upstream's <c>report/build.ts</c>): the targets, the summary, the status, the usage, and
/// the document that holds them. Not ported: serial groups, <c>carried</c> (a <c>--last-failed</c> rerun), and
/// <c>failOnSkippedFailure</c>, since the port has no serial-suite feature, no rerun, and no such config key.
/// </summary>
internal static class BuildReport
{
    /// <summary>Upstream's <c>ENGINE_SPI_VERSION</c>.</summary>
    internal const int EngineSpiVersion = 1;

    /// <summary>
    /// What a target's engine declaration says about it: the one description the runner grades against and the report records. A target
    /// without an engine is agent-tools-only and honestly reports no capabilities (upstream's <c>describeTarget</c>).
    /// </summary>
    /// <remarks>
    /// .NET-only mapping: the port's <see cref="IEngine"/> has no name, so the name is its <see cref="IEngine.Platform"/>; its
    /// capabilities are flags, so each is spelled in camel case as upstream spells its capability names; and it has no state hooks, so
    /// <c>stateCapability</c> is false.
    /// </remarks>
    public static TargetProvenance DescribeTarget(IEngine? engine) =>
        engine is null
            ? new TargetProvenance(new ReportEngine("none", "unversioned", EngineSpiVersion), [], [], false)
            : DescribeTarget(engine.Platform, engine.Version, engine.Capabilities);

    /// <summary>The description of an engine known by its platform, version, and capabilities.</summary>
    public static TargetProvenance DescribeTarget(string platform, string version, EngineCapabilities capabilities)
    {
        var artifacts = new List<string>();
        if (capabilities.HasFlag(EngineCapabilities.Screenshot))
        {
            artifacts.Add("screenshot");
        }

        if (capabilities.HasFlag(EngineCapabilities.Video))
        {
            artifacts.Add("video");
        }

        return new TargetProvenance(
            new ReportEngine(platform, string.IsNullOrWhiteSpace(version) ? "unversioned" : version, EngineSpiVersion),
            [.. Enum.GetValues<EngineCapabilities>().Where(each => each != EngineCapabilities.None && capabilities.HasFlag(each)).Select(each => char.ToLowerInvariant(each.ToString()[0]) + each.ToString()[1..]).Order(StringComparer.Ordinal)],
            artifacts,
            false);
    }

    /// <summary>
    /// A recorded declaration as the report writes it: project-relative, line and column at least 1. A file outside the project becomes
    /// <paramref name="fallbackFile"/>; no recorded source stays null (upstream's <c>projectSource</c>).
    /// </summary>
    public static SourceLocation? ProjectSource(string? projectRoot, SourceLocation? source, string fallbackFile)
    {
        if (source is null)
        {
            return null;
        }

        var file = source.File;
        if (projectRoot is not null && Path.IsPathRooted(file))
        {
            file = Source.ProjectRelativePath(projectRoot, file) ?? fallbackFile;
        }

        if (file.StartsWith('/') || (file.Length >= 2 && char.IsAsciiLetter(file[0]) && file[1] == ':'))
        {
            file = fallbackFile;
        }

        return new SourceLocation(file, Math.Max(1, source.Line), Math.Max(1, source.Column));
    }

    /// <summary>The run's counts from its results; the status counts are over the selected ones (upstream's <c>computeSummary</c>).</summary>
    public static ReportSummary ComputeSummary(IReadOnlyList<ReportResult> results)
    {
        int selected = 0, executed = 0, passed = 0, failed = 0, interrupted = 0, flaky = 0, skipped = 0;
        foreach (var result in results)
        {
            if (result.Attempts.Count > 0)
            {
                executed++;
            }

            if (!result.Selected)
            {
                continue;
            }

            selected++;
            switch (result.Status)
            {
                case ResultStatus.Passed:
                    passed++;
                    break;
                case ResultStatus.Flaky:
                    flaky++;
                    break;
                case ResultStatus.Failed or ResultStatus.TimedOut:
                    failed++;
                    break;
                case ResultStatus.Interrupted:
                    interrupted++;
                    break;
                case ResultStatus.Skipped:
                    skipped++;
                    break;
            }
        }

        return new ReportSummary(results.Count, selected, executed, passed, failed, interrupted, flaky, skipped);
    }

    /// <summary>
    /// A run is <c>blocked</c>, not failed, when it did not pass and every non-passing result carries a blockable error code: credentials,
    /// the environment, or the agent's own budget prevented a product verdict, and nothing contradicts that. One genuine failure keeps the
    /// run failed, and so does any run-level error: it is its own fact about the run, not a blocked step. Derivation requires positive
    /// evidence, never absence of it (upstream's <c>deriveRunStatus</c>, without serial groups and <c>failOnSkippedFailure</c>).
    /// </summary>
    public static ReportRunStatus DeriveRunStatus(ReportRunStatus status, IReadOnlyList<ReportResult> results, IReadOnlyList<ReportError> runErrors)
    {
        if (status is not (ReportRunStatus.Failed or ReportRunStatus.Error) || runErrors.Count > 0)
        {
            return status;
        }

        var sawFailure = false;
        foreach (var result in results)
        {
            if (result.Status is not (ResultStatus.Failed or ResultStatus.TimedOut or ResultStatus.Interrupted))
            {
                continue;
            }

            sawFailure = true;
            if (result.Attempts.LastOrDefault()?.Error?.Code is not { } code || !StepExecutors.BlockableCodes.Contains(code))
            {
                return status;
            }
        }

        return sawFailure ? ReportRunStatus.Blocked : status;
    }

    /// <summary>Aggregates what the run used against its limits (upstream's <c>computeUsage</c>).</summary>
    public static ReportUsage ComputeUsage(IReadOnlyList<ReportResult> results, int discovered)
    {
        int maxContext = 0, maxLedger = 0, maxObservation = 0, downloads = 0, maxModelCalls = 0, maxActionSteps = 0;
        long artifactBytes = 0, modelTokens = 0, events = 0;
        long? cachedTokens = null;
        var cost = 0.0;
        var costSeen = false;

        foreach (var attempt in results.SelectMany(result => result.Attempts))
        {
            foreach (var artifact in attempt.Artifacts)
            {
                artifactBytes += artifact.Size ?? 0;
                if (artifact.Kind == "download")
                {
                    downloads++;
                }
            }

            foreach (var step in attempt.Steps)
            {
                events += step.Events.Count;
                if (step.Metrics is { } metrics)
                {
                    maxContext = Math.Max(maxContext, metrics.ContextBytes);
                    maxLedger = Math.Max(maxLedger, metrics.LedgerBytes);
                    maxObservation = Math.Max(maxObservation, metrics.ObservationBytes);
                    maxModelCalls = Math.Max(maxModelCalls, metrics.ModelCalls);
                    maxActionSteps = Math.Max(maxActionSteps, metrics.ActionSteps);
                }

                if (step.Model is not { } model)
                {
                    continue;
                }

                modelTokens += (long)model.InputTokens + model.OutputTokens;
                if (model.CacheReadTokens is { } cacheRead)
                {
                    cachedTokens = (cachedTokens ?? 0) + cacheRead;
                }

                if (model.EstimatedCostUsd is { } estimated)
                {
                    cost += estimated;
                    costSeen = true;
                }
            }
        }

        return new ReportUsage
        {
            DiscoveredResults = discovered,
            MaxAgentContextBytes = maxContext,
            MaxLedgerBytes = maxLedger,
            MaxObservationBytes = maxObservation,
            ArtifactBytes = artifactBytes,
            Downloads = downloads,
            Events = events,
            ModelTokens = modelTokens,
            ModelCachedTokens = cachedTokens,
            MaxModelCallsInStep = maxModelCalls,
            MaxActionStepsInStep = maxActionSteps,
            EstimatedCostUsd = costSeen && double.IsFinite(cost) ? cost : null,
        };
    }

    /// <summary>The target as the report writes it (upstream's <c>serializeTarget</c>). The port has no app environment key, so every target is <c>test</c>.</summary>
    public static ReportTarget SerializeTarget(TargetConfig target, int index, TargetProvenance provenance) =>
        new(target.Name, index, target.Platform, provenance, "test", BaseOrigin(target.App.Url));

    // The origin as a URL spells it: scheme, host, and a port the scheme does not imply, with no credentials. Only a web origin has one.
    internal static string? BaseOrigin(string? url) =>
        url is not null && Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Scheme is "http" or "https"
            ? parsed.Scheme + "://" + (parsed.HostNameType == UriHostNameType.IPv6 ? "[" + parsed.IdnHost + "]" : parsed.IdnHost) + (parsed.IsDefaultPort ? "" : ":" + parsed.Port.ToString(System.Globalization.CultureInfo.InvariantCulture))
            : null;

    /// <summary>Builds the report document of a run (upstream's <c>buildReport</c>).</summary>
    public static Report1Document Build(BuildReportOptions options)
    {
        var targets = options.Targets
            .Select((target, index) => SerializeTarget(target, index, options.TargetProvenance.TryGetValue(target.Name, out var found) ? found : DescribeTarget(null)))
            .ToList();
        foreach (var described in options.ReportTargets.Where(each => targets.All(known => known.Id != each.Id)))
        {
            targets.Add(described with { Index = targets.Count });
        }

        var targetIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var target in targets)
        {
            targetIndex[target.Id] = target.Index;
        }

        // Report order is completion-time independent.
        var results = options.Results
            .OrderBy(result => result.File, StringComparer.Ordinal)
            .ThenBy(result => result.DeclarationIndex)
            .ThenBy(result => targetIndex.GetValueOrDefault(result.TargetId))

            // The port declares no index for a test, so ties keep apart by what is left of the declaration, not by completion order.
            .ThenBy(result => result.Source.Line)
            .ThenBy(result => string.Join('\u001f', result.TitlePath), StringComparer.Ordinal)
            .ThenBy(result => result.Id, StringComparer.Ordinal)
            .Select(result => result with { Platform = result.Platform.Length > 0 ? result.Platform : targets.FirstOrDefault(target => target.Id == result.TargetId)?.Platform ?? "unknown", Source = ProjectSource(options.ProjectRoot, result.Source.File == "unknown" ? null : result.Source, result.File) ?? new SourceLocation(result.File, 1, 1) })
            .ToList();
        var summary = ComputeSummary(options.Results);
        return new Report1Document(new ReportRun(options.RunId, ModelHttp.PackageVersion(), targets, results, options.Explore)
        {
            Status = DeriveRunStatus(options.Status, options.Results, options.RunErrors),
            ExitCode = options.ExitCode,
            StartedAt = options.StartedAt,
            FinishedAt = options.FinishedAt ?? DateTimeOffset.UtcNow,
            Environment = new ReportEnvironment(options.Ci, false, OperatingSystemName() + " " + OsRelease(), ArchitectureName(), RuntimeInformation.FrameworkDescription),
            Vcs = options.Vcs,
            Errors = options.RunErrors,
            Summary = summary,
            Limits = ReportLimits.Default,
            Usage = ComputeUsage(options.Results, summary.Discovered),
            Changes = options.Changes,
            Filters = options.Filters,
        });
    }

    private static string OsRelease()
    {
        var version = Environment.OSVersion.Version;
        return version.Major + "." + version.Minor + "." + Math.Max(0, version.Build);
    }

    private static string OperatingSystemName() => ScreenshotContext.OperatingSystemName();

    // Node's os.arch() spelling.
    private static string ArchitectureName() => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64 => "x64",
        Architecture.X86 => "ia32",
        Architecture.Arm64 => "arm64",
        Architecture.Arm => "arm",
        var other => other.ToString().ToLowerInvariant(),
    };
}
