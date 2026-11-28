// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Internal;
using E2E.Reports;

namespace E2E;

/// <summary>What the producer of an artifact knows about it beyond its file (upstream's <c>ArtifactRegistration</c>).</summary>
internal sealed record ArtifactRegistration
{
    /// <summary>When a time-based artifact (a video segment) began recording.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>The session that produced it when it is not the test's own; see <see cref="StepRecord.Actor"/>.</summary>
    public string? Actor { get; init; }
}

/// <summary>A provider-hosted recording (upstream's <c>ArtifactSink.link</c> argument).</summary>
internal sealed record ArtifactLink(string MediaType, DateTimeOffset StartedAt, string? Actor = null);

/// <summary>
/// Where an attempt registers the files and links it produced (upstream's <c>ArtifactSink</c>, <c>run/fixtures.ts</c>). Registration is
/// synchronous and cheap; the size, digest, and hand-off to a store finish in the background (see <c>AttemptArtifacts.SettleAsync</c>).
/// </summary>
internal interface IArtifactSink
{
    /// <summary>The absolute attempt artifact directory (upstream's <c>dir</c>).</summary>
    string Dir { get; }

    /// <summary>Registers a file under <see cref="Dir"/> (upstream's <c>register</c>) and returns the artifact's id.</summary>
    string Register(ArtifactKind kind, string relativePath, ArtifactRegistration? registration = null);

    /// <summary>Registers a video a hosted service keeps, by its URL (upstream's <c>link</c>), and returns the artifact's id.</summary>
    string Link(string url, ArtifactLink link);
}

/// <summary>
/// Opens and resets the app under test. A relative URL is resolved against
/// <see cref="BaseUrl"/>; no URL opens the base URL.
/// </summary>
public class App
{
    private readonly IEngineSession _session;
    private readonly string? _baseUrl;
    private readonly Func<CancellationToken> _token;

    /// <summary>The budget an app call on the named api has (the browser's, bounded by the test); null outside a session, where a call has none.</summary>
    internal Func<string, TimeSpan>? OperationBudget { get; set; }

    /// <summary>What bounds an open, a restart, or a link open: the default a program set, else the test's timeout (upstream's <c>lifecycleMs</c>); null outside a session.</summary>
    internal Func<TimeSpan>? LifecycleBudget { get; set; }

    private readonly StepRecorder? _steps;
    private readonly bool _pinnedApp;
    private readonly string _target;
    private readonly string? _linkBase;

    internal App(
        IEngineSession session,
        string? baseUrl,
        Func<CancellationToken> token,
        StepRecorder? steps,
        bool pinnedApp,
        string target,
        string? linkBase = null
    )
    {
        _linkBase = linkBase;
        _session = session;
        _pinnedApp = pinnedApp;
        _target = target;
        _baseUrl = baseUrl;
        _token = token;
        _steps = steps;
    }

    /// <summary>Copies an app, which <see cref="WebApp"/> extends (upstream's <c>WebApp extends App</c>).</summary>
    private protected App(App other)
    {
        _session = other._session;
        _baseUrl = other._baseUrl;
        _token = other._token;
        _steps = other._steps;
        _pinnedApp = other._pinnedApp;
        _target = other._target;
        _linkBase = other._linkBase;
        OperationBudget = other.OperationBudget;
        LifecycleBudget = other.LifecycleBudget;
        Recordings = other.Recordings;
        SinkOf = other.SinkOf;
        ArtifactsDir = other.ArtifactsDir;
        Redact = other.Redact;
        Attach = other.Attach;
    }

    /// <summary>
    /// The resolved base URL of the target's app, with the port the run allocated when <c>app.url</c> declared port 0 and no trailing
    /// slash, so <c>$"{BaseUrl}/api/users"</c> joins with one slash; null when the test declares none. <see cref="OpenAsync"/> still
    /// resolves a relative URL against <c>app.url</c> as declared: a base path that ends in <c>/</c> is kept, and one without it is replaced.
    /// </summary>
    public string? BaseUrl => _baseUrl is null ? null : Urls.BaseUrlOf(_baseUrl);

    /// <summary>
    /// Opens a path or URL, resolved against the base URL. No URL opens the base URL itself.
    /// Throws <c>APP_URL_REQUIRED</c> for a relative URL when the test declares no base URL, and <c>POLICY_DENIED</c>
    /// for a malformed URL or a scheme other than <c>http:</c> or <c>https:</c> (the exact <c>about:blank</c> is admitted). Throws
    /// <c>APP_UNREACHABLE</c>, naming the URL, when the network refused the navigation because nothing answers there (a refused connection, a
    /// name that does not resolve, an unreachable host, or a connection that timed out); any other failed navigation is <c>ENGINE_FAILURE</c>.
    /// On a device with no <c>app.bundleId</c>, throws <c>UNSUPPORTED_CAPABILITY</c> until the target's build is installed with <c>Device.InstallAppAsync</c>.
    /// </summary>
    public Task OpenAsync(string? url = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_baseUrl) && _pinnedApp)
        {
            return LaunchPinnedAppAsync(url, cancellationToken);
        }

        return RunStepAsync("app.open", url ?? "/", () =>
            OpenAtAsync(Routes.Resolve(_baseUrl, url), Token(cancellationToken))
        );
    }

    /// <summary>
    /// <c>app.open()</c> on a target with no URL whose engine pins an app, a device: it launches the pinned app fresh, and is what a test
    /// calls before it looks at the screen. A path opens as a link in the app when the target declares <c>app.linkBase</c>, and is <c>APP_URL_REQUIRED</c>
    /// without it; a link then goes through the device fixture's <c>OpenLinkAsync</c>. A refused link scheme is <c>POLICY_DENIED</c>. Upstream's <c>launchPinnedApp</c> in <c>run/fixtures.ts</c>.
    /// </summary>
    private Task LaunchPinnedAppAsync(string? url, CancellationToken cancellationToken)
    {
        return RunStepAsync("app.open", url ?? "", async () =>
        {
            if (url is not null && _linkBase is not null)
            {
                var link = TargetApps.JoinLink(_linkBase, url);
                if (TargetApps.RefusesLink(link))
                {
                    throw new ConfigurationException(
                        "POLICY_DENIED",
                        "app.open() refuses the link's scheme on target \"" + _target + "\""
                    );
                }

                await _session
                    .OpenLinkAsync(link, Lifecycle(OperationContext.Test), Token(cancellationToken))
                    .ConfigureAwait(false);
                return;
            }

            if (url is not null)
            {
                throw new ConfigurationException(
                    "APP_URL_REQUIRED",
                    "app.open(\""
                        + url
                        + "\") needs an app URL and target \""
                        + _target
                        + "\" has none: on a device app.open() launches the pinned app; declare app.linkBase to open a path as a link in it, or open one through device.openLink"
                );
            }

            await _session
                .RestartAsync(Lifecycle(OperationContext.Test), Token(cancellationToken))
                .ConfigureAwait(false);
        });
    }

    /// <summary>Goes back one entry in the history. With no earlier entry it does nothing.</summary>
    public Task BackAsync(CancellationToken cancellationToken = default)
    {
        return RunStepAsync("app.back", "", () =>
            _session.BackAsync(Operation("app.back", OperationContext.Test), Token(cancellationToken))
        );
    }

    /// <summary>
    /// Closes the current document and reopens the app at <see cref="BaseUrl"/>. Cookies and storage are kept. The reopen, which
    /// happens when a base URL is set, throws <c>APP_UNREACHABLE</c> when nothing answers, as <see cref="OpenAsync"/> does.
    /// On a device with no <c>app.bundleId</c>, throws <c>UNSUPPORTED_CAPABILITY</c> until the target's build is installed.
    /// </summary>
    public Task RestartAsync(CancellationToken cancellationToken = default)
    {
        return RunStepAsync("app.restart", "", () =>
            SteerAsync(
                "app.restart",
                (context, token) => _session.RestartAsync(context, token),
                Token(cancellationToken)
            )
        );
    }

    /// <summary>
    /// Discards cookies, storage, and history, and reopens the app at <see cref="BaseUrl"/>. The reopen, which happens when a base URL is set, throws
    /// <c>APP_UNREACHABLE</c> when nothing answers, as <see cref="OpenAsync"/> does.
    /// On a device with no <c>app.bundleId</c>, throws <c>UNSUPPORTED_CAPABILITY</c> until the target's build is installed.
    /// </summary>
    public Task ClearStateAsync(CancellationToken cancellationToken = default)
    {
        return RunStepAsync("app.clearState", "", () =>
            SteerAsync(
                "app.clearState",
                (context, token) => _session.ClearStateAsync(context, token),
                Token(cancellationToken)
            )
        );
    }

    /// <summary>
    /// Captures a redacted evidence screenshot: secure fields are masked at the source. The file is an attempt artifact under
    /// <c>screenshots/</c>, and the returned path is absolute (upstream's <c>app.screenshot</c>, <c>createApp</c> in <c>run/fixtures.ts</c>).
    /// A screenshot taken while the page navigates captures the document showing.
    /// Throws <c>UNSUPPORTED_CAPABILITY</c> on an engine that keeps no screenshots, and <c>POLICY_DENIED</c> on iOS and Android when a
    /// secure field on screen has no bounds to mask.
    /// </summary>
    /// <param name="label">Names the step and joins the file name.</param>
    /// <param name="cancellationToken">Cancels the capture.</param>
    public async Task<string> ScreenshotAsync(string? label = null, CancellationToken cancellationToken = default)
    {
        var path = "";
        var artifactsDir = ArtifactsDir
            ?? throw new EngineException(
                EngineErrorCodes.InvalidState,
                "screenshot outside an attempt: the app has no artifact directory"
            );
        await RunStepAsync("app.screenshot", label ?? "", async () =>
            {
                // .NET-only hardening: the label joins the file name and the artifact path, so a secret spelled into it stops here.
                var relative = await _session
                    .ScreenshotArtifactAsync(
                        label is null ? null : Redact(label),
                        Operation("app.screenshot", OperationContext.Test),
                        Token(cancellationToken)
                    )
                    .ConfigureAwait(false);
                Attach("screenshot", relative);
                path = Path.GetFullPath(Path.Combine(artifactsDir, relative));
            })
            .ConfigureAwait(false);
        return path;
    }

    /// <summary>
    /// Starts recording the screen, until <see cref="StopRecordingAsync"/>. One recording at a time, and none while the test's
    /// <c>video</c> records, since the engine has one recorder: set the test's video to <c>off</c>. A recording still running when the
    /// test ends is stopped and kept. Like the test's video, a recording masks nothing. Upstream's <c>app.startRecording</c>.
    /// Throws <c>UNSUPPORTED_CAPABILITY</c> while the test's video records or when the engine records no video, and
    /// <c>INVALID_ARGUMENT</c> when another recording is running.
    /// </summary>
    /// <param name="options">Names the file: <c>recordings/001-&lt;name&gt;.&lt;ext&gt;</c>.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    public async Task StartRecordingAsync(
        RecordingOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var recordings = Recordings
            ?? throw new EngineException(
                EngineErrorCodes.InvalidState,
                "recording outside an attempt: the app has no recorder"
            );
        _ = SinkOf()
            ?? throw new EngineException(
                EngineErrorCodes.InvalidState,
                "recording outside an attempt: the app has no artifact directory"
            );
        var name = options?.Name;
        await RunStepAsync("app.startRecording", name ?? "", () =>
                recordings.StartSpanAsync(
                    name,
                    Operation("app.startRecording", OperationContext.Test),
                    Token(cancellationToken)
                )
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Stops the recording and returns its videos, in play order: a file (absolute <c>Path</c>) under the attempt's <c>recordings/</c>,
    /// named <c>001-&lt;name&gt;.&lt;ext&gt;</c>, or a link when a hosted browser or device keeps the recording. There is one video, except on
    /// the web when the page was replaced while it recorded (<c>app.RestartAsync()</c>, <c>app.ClearStateAsync()</c>): then one per page.
    /// The step links each video in the trace. Never empty. Upstream's <c>app.stopRecording</c>. Throws <c>INVALID_ARGUMENT</c> when no
    /// recording is running and <c>ENGINE_FAILURE</c> when the engine wrote no video.
    /// </summary>
    /// <param name="cancellationToken">Cancels the stop.</param>
    public async Task<IReadOnlyList<RecordedVideo>> StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        var recordings = Recordings
            ?? throw new EngineException(
                EngineErrorCodes.InvalidState,
                "recording outside an attempt: the app has no recorder"
            );
        var sink = SinkOf()
            ?? throw new EngineException(
                EngineErrorCodes.InvalidState,
                "recording outside an attempt: the app has no artifact directory"
            );
        IReadOnlyList<RecordedVideo> videos = [];
        await RunStepAsync("app.stopRecording", "", async () =>
            {
                var stopped = await recordings
                    .StopSpanAsync(
                        sink,
                        Operation("app.stopRecording", OperationContext.Test),
                        Token(cancellationToken)
                    )
                    .ConfigureAwait(false);
                foreach (var id in stopped.Ids)
                {
                    _steps?.AttachArtifact(id);
                }

                videos = stopped.Videos;
            })
            .ConfigureAwait(false);
        return videos;
    }

    /// <summary>The engine's one recorder, shared with the attempt's video (upstream's <c>sessionRecordings</c>).</summary>
    internal SessionRecordings? Recordings { get; set; }

    /// <summary>Where the attempt registers the videos a recording produced (upstream's <c>environment.artifacts</c>); read when a recording stops, since asking for it creates the attempt's directory.</summary>
    internal Func<IArtifactSink?> SinkOf { get; set; } = static () => null;

    /// <summary>The attempt's artifact directory, which a screenshot's artifact-relative path is resolved against (upstream's <c>environment.artifacts.dir</c>).</summary>
    internal string? ArtifactsDir { get; set; }

    /// <summary>Replaces the secrets the attempt knows in text that reaches a file name; identity outside a session.</summary>
    internal Func<string, string> Redact { get; set; } = static text => text;

    /// <summary>Keeps a file the fixture wrote as an artifact of the running step (upstream's <c>attachArtifact</c>). Nothing outside a session keeps it.</summary>
    internal Action<string, string> Attach { get; set; } = static (_, _) => { };

    // Each call that steers the app is a recorded step of the attempt, which maps the engine error the call throws (upstream's fixture
    // runtime), or runs as it is without a timeline.
    private Task RunStepAsync(string api, string label, Func<Task> body) =>
        _steps is null
            ? body()
            : _steps.RunAsync(StepKind.App, api, label, () => FixtureRecording.Translated(api, label, body));

    // A steering hook ends at a blank surface, so the app is reopened at its base URL
    // inside the same step. Without a base URL there is nothing to reopen.
    private async Task SteerAsync(
        string api,
        Func<OperationContext, CancellationToken, Task> hook,
        CancellationToken cancellationToken
    )
    {
        await hook(Lifecycle(OperationContext.Test), cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(_baseUrl))
        {
            await OpenAtAsync(Routes.Resolve(_baseUrl, null), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Network-level failures a browser or device reports when nothing is listening where the app should be. Anything else that fails a
    /// navigation (a certificate, a 500, a crash mid-load) stays an engine failure. Upstream's <c>NOTHING_LISTENING</c>.
    /// </summary>
    private static readonly Regex NothingListening = new(
        "ERR_CONNECTION_REFUSED|ECONNREFUSED|ERR_NAME_NOT_RESOLVED|ENOTFOUND|ERR_ADDRESS_UNREACHABLE|EHOSTUNREACH|ERR_CONNECTION_TIMED_OUT|ETIMEDOUT",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2)
    );

    /// <summary>
    /// A navigation the network refused means the app is down, which is the first thing a new project runs into and the last thing an engine
    /// failure message suggests. Named by its own code, with the URL and the three ways to fix it. Upstream's <c>unreachableApp</c>.
    /// </summary>
    private static EngineException? UnreachableApp(Exception cause, string url)
    {
        var match = NothingListening.Match(cause.Message);
        return match.Success
            ? new EngineException(
                "APP_UNREACHABLE",
                $"nothing answered at {url} ({match.Value}); start the app there, point the target's app.url at where it runs, or give the target an app.command so the runner starts it",
                cause
            )
            {
                Mapped = true,
            }
            : null;
    }

    /// <summary>Opens one resolved URL; a refused connection is reported as the app being down (upstream's <c>openAt</c>).</summary>
    private async Task OpenAtAsync(string resolved, CancellationToken cancellationToken)
    {
        try
        {
            await _session
                .OpenAsync(resolved, Lifecycle(OperationContext.Test), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception cause)
            when (cause is not (OperationCanceledException or (E2EException and not EngineException))
                && UnreachableApp(cause, resolved) is { } unreachable
            )
        {
            throw unreachable;
        }
    }

    private CancellationToken Token(CancellationToken cancellationToken) => Time.Link(cancellationToken, _token());

    /// <summary>The context of an open, a restart, or a link open: <paramref name="context"/> with the lifecycle budget as its <c>Timeout</c>, or as it is outside a session.</summary>
    private OperationContext Lifecycle(OperationContext context) =>
        LifecycleBudget is { } budget
            ? context with
            {
                Timeout = Time.Between(budget(), TimeSpan.FromMilliseconds(1), Time.Unbounded),
            }
            : context;

    /// <summary>The context of an app call on <paramref name="api"/>: <paramref name="context"/> with the call's budget as its <c>Timeout</c>, or as it is outside a session.</summary>
    private OperationContext Operation(string api, OperationContext context) =>
        OperationBudget is { } budget
            ? context with
            {
                Timeout = Time.Between(budget(api), TimeSpan.FromMilliseconds(1), Time.Unbounded),
            }
            : context;
}

/// <summary>The fixtures for one test. Call <see cref="Skip"/> before the first step when a precondition is missing.</summary>
public sealed class TestContext
{
    public required App App { get; init; }

    /// <summary>The browser fixture. Its members fail with <c>UNSUPPORTED_CAPABILITY</c> on an engine without a browser.</summary>
    public required Browser Browser { get; init; }

    /// <summary>The engine platform, such as <c>web</c> or <c>document</c>.</summary>
    public required string Platform { get; init; }

    public required Agent Agent { get; init; }

    public required Screen Screen { get; init; }

    /// <summary>
    /// The values the config's services yielded, keyed by name (upstream's <c>services</c> fixture, <c>TestFixtures.services</c>). Empty when the
    /// config has none. A test runs in the process that started the services, so a value is the object the service yielded, not a copy.
    /// </summary>
    public ServiceValues Services { get; init; } = ServiceValues.Empty;

    public CancellationToken CancellationToken { get; init; }

    public void Skip(bool condition, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (condition)
        {
            throw new SkipException(reason);
        }
    }
}

/// <summary>Upstream's <c>FixtureScope</c> (<c>types/fixtures.ts</c>): how long a fixture lives.</summary>
public static class FixtureScope
{
    /// <summary>One test attempt.</summary>
    public const string Test = "test";

    /// <summary>The worker.</summary>
    public const string Worker = "worker";
}

/// <summary>
/// How long a core fixture lives (upstream's <c>CoreFixtureScope</c>, <c>run/fixtures.ts</c>): one test attempt, the worker, or, for
/// <see cref="Engine"/>, as long as the state behind the engine's own fixtures, which the engine declares (<c>isolation</c>).
/// </summary>
internal static class CoreFixtureScope
{
    public const string Test = "test";
    public const string Worker = "worker";
    public const string Engine = "engine";
}

/// <summary>
/// What the built-in fixtures of a target are read from (upstream's <c>BuiltInTarget</c>): its name, its platform, its engine's name, fixtures,
/// and isolation, and the fixture scopes of every target.
/// </summary>
internal sealed record BuiltInTarget(
    string Name,
    string Platform,
    IReadOnlyDictionary<string, string> FixtureScopes,
    ResolvedEngine? Engine = null
)
{
    /// <summary>The target as <see cref="ResolvedTarget"/> holds it.</summary>
    public static BuiltInTarget Of(ResolvedTarget target) =>
        new(target.Name, target.Platform, target.FixtureScopes, target.Engine);
}

/// <summary>
/// Upstream's <c>CORE_FIXTURES</c> table (<c>run/fixtures.ts</c>): the fixtures every attempt has, whatever its engine, each one's name and scope.
/// Differences: the port builds a fixture's value where the session is built (<c>Create</c> has no home here), so the table holds the scope
/// and, for the fixtures built from the target and the worker alone (<c>ofWorker</c>), how to build them.
/// </summary>
internal static class CoreFixtures
{
    /// <summary>Upstream's <c>CORE_FIXTURES</c>: each core fixture's scope; the value is its <c>ofWorker</c> builder, or null when an attempt builds it.</summary>
    public static readonly IReadOnlyDictionary<
        string,
        (string Scope, Func<BuiltInTarget, IReadOnlyDictionary<string, object?>, object?>? OfWorker)
    > Table = new Dictionary<string, (string, Func<BuiltInTarget, IReadOnlyDictionary<string, object?>, object?>?)>
    {
        ["agent"] = (CoreFixtureScope.Test, null),
        ["app"] = (CoreFixtureScope.Engine, null),
        ["screen"] = (CoreFixtureScope.Engine, null),
        ["platform"] = (CoreFixtureScope.Worker, (target, _) => target.Platform),
        ["request"] = (CoreFixtureScope.Test, null),
        ["services"] = (CoreFixtureScope.Worker, (_, services) => services),
        ["session"] = (CoreFixtureScope.Test, null),
    };

    /// <summary>The table's entry for <paramref name="name"/> (upstream's <c>coreFixture</c>), when it is a core fixture.</summary>
    internal static (string Scope, Func<BuiltInTarget, IReadOnlyDictionary<string, object?>, object?>? OfWorker)? Get(
        string name
    ) => Table.TryGetValue(name, out var fixture) ? fixture : null;

    /// <summary>Whether reading <paramref name="name"/> requires an attempt: every fixture except core fixtures derived from the target or worker (upstream's <c>needsAttempt</c>).</summary>
    internal static bool NeedsAttempt(string name) => Get(name) is not { OfWorker: not null };
}

/// <summary>
/// The built-in fixtures of a target (upstream's <c>BuiltInFixtures</c> and <c>builtInFixtures</c>, <c>run/fixtures.ts</c>): the core ones and the ones its
/// engine contributes. The one place that knows their names, their scopes, and which of them need an attempt.
/// </summary>
internal sealed class BuiltInFixtures : IBuiltInScopes
{
    /// <summary>Fixtures other runners hand out, each pointed at the e2e way of doing the same thing (upstream's <c>FOREIGN_FIXTURE_HINTS</c>).</summary>
    private static readonly Dictionary<string, string> ForeignFixtureHints = new()
    {
        ["page"] =
            "there is no Playwright page: open the app with app.open() and find elements through screen; browser-only APIs are on the browser fixture of a Playwright target",
        ["context"] =
            "the browser context is owned by the engine: cookies, routes, and storage are on the browser fixture of a Playwright target",
        ["driver"] =
            "there is no WebDriver session: drive the device through app, screen, and (on an agent-device target) device",
    };

    private readonly BuiltInTarget _target;
    private readonly IReadOnlyDictionary<string, object?> _services;
    private readonly IReadOnlyList<string> _contributed;

    private BuiltInFixtures(BuiltInTarget target, IReadOnlyDictionary<string, object?> services)
    {
        _target = target;
        _services = services;
        _contributed = target.Engine?.Fixtures ?? [];
        Names = [.. CoreFixtures.Table.Keys.Concat(_contributed).Concat(target.FixtureScopes.Keys).Distinct()];
    }

    /// <summary>
    /// The built-in fixtures as a <c>test.extend()</c> call can know them, before any target is (upstream's <c>DECLARED_BUILT_INS</c>): the core ones whose scope no
    /// engine changes. Every other name is <c>unresolved</c>, since an engine may contribute it, so none is missing.
    /// </summary>
    public static IBuiltInScopes Declared { get; } = new DeclaredBuiltInScopes();

    /// <summary>The target the fixtures are of, for an error to name.</summary>
    public string? TargetName => _target.Name;

    /// <summary>Every built-in fixture name: the core ones, the engine's, then the ones only other targets have.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>Upstream's <c>builtInFixtures</c>: the built-in fixtures for <paramref name="target"/> with the worker's <paramref name="services"/> values.</summary>
    public static BuiltInFixtures Of(BuiltInTarget target, IReadOnlyDictionary<string, object?>? services = null) =>
        new(target, services ?? new Dictionary<string, object?>());

    /// <summary>
    /// How long <paramref name="name"/> lives: one test attempt or the worker. The engine's own fixtures, <c>app</c>, and <c>screen</c> live as long as the
    /// state behind them, which the engine declares (<c>isolation</c>). A fixture only other targets have takes its scope from them. Null when no
    /// target has a built-in fixture of that name.
    /// </summary>
    public string? Scope(string name)
    {
        var engineScope = _target.Engine?.Isolation == EngineIsolation.Worker ? FixtureScope.Worker : FixtureScope.Test;
        var scope = CoreFixtures.Get(name)?.Scope
            ?? (
                _contributed.Contains(name) ? CoreFixtureScope.Engine
                : _target.FixtureScopes.TryGetValue(name, out var shared) ? shared
                : null
            );
        return scope == CoreFixtureScope.Engine ? engineScope : scope;
    }

    /// <summary>Whether reading <paramref name="name"/> requires an engine attempt, which is every built-in except those derived from the target or worker.</summary>
    public bool NeedsAttempt(string name) =>
        (_contributed.Contains(name) || !_target.FixtureScopes.ContainsKey(name)) && CoreFixtures.NeedsAttempt(name);

    /// <summary>The value of a built-in that needs no attempt; null for any other name.</summary>
    public object? Attemptless(string name) => CoreFixtures.Get(name)?.OfWorker?.Invoke(_target, _services);

    /// <summary>Why the target has no fixture <paramref name="name"/>, beside the fixtures <paramref name="available"/> to the code that asked (upstream's <c>unknownFixtureMessage</c>).</summary>
    public string Missing(string name, IReadOnlyList<string> available)
    {
        var contributed =
            $"engine {_target.Engine?.Name ?? "none"} contributes {(_contributed.Count == 0 ? "no fixtures" : string.Join(", ", _contributed))}";
        var hint = ForeignFixtureHints.TryGetValue(name, out var foreign)
            ? $"; {foreign}"
            : Suggestions.DidYouMean(name, available);
        return $"target \"{_target.Name}\" has no \"{name}\" fixture; available: {string.Join(", ", available)} ({contributed}){hint}";
    }
}

/// <summary>
/// What the fixture planner reads of the built-in fixtures (upstream's <c>Omit&lt;BuiltInFixtures, 'attemptless'&gt;</c>, <c>collect/fixture-plan.ts</c>):
/// the target's, or, while a <c>test.extend()</c> call is checked, the ones known before any target is (<see cref="BuiltInFixtures.Declared"/>).
/// .NET-only: C# has no <c>Omit</c>.
/// </summary>
internal interface IBuiltInScopes
{
    /// <summary>The target the fixtures are of, for an error to name; null before any target is known.</summary>
    string? TargetName { get; }

    /// <summary>Every built-in fixture name.</summary>
    IReadOnlyList<string> Names { get; }

    /// <summary>How long <paramref name="name"/> lives, <c>unresolved</c> where an engine decides, null for a name no target has.</summary>
    string? Scope(string name);

    /// <summary>Whether reading <paramref name="name"/> requires an engine attempt.</summary>
    bool NeedsAttempt(string name);

    /// <summary>Why <paramref name="name"/> is no built-in fixture, beside the fixtures <paramref name="available"/>.</summary>
    string Missing(string name, IReadOnlyList<string> available);
}

/// <summary>The scope a fixture the engine decides has before a target is known (upstream's <c>unresolved</c>, <c>collect/fixture-plan.ts</c>).</summary>
internal static class UnresolvedScope
{
    public const string Value = "unresolved";
}

internal sealed class DeclaredBuiltInScopes : IBuiltInScopes
{
    public string? TargetName => null;

    public IReadOnlyList<string> Names { get; } = [.. CoreFixtures.Table.Keys];

    public string? Scope(string name)
    {
        var scope = CoreFixtures.Get(name)?.Scope ?? CoreFixtureScope.Engine;
        return scope == CoreFixtureScope.Engine ? UnresolvedScope.Value : scope;
    }

    public bool NeedsAttempt(string name) => CoreFixtures.NeedsAttempt(name);

    public string Missing(string name, IReadOnlyList<string> available) => $"no fixture is called \"{name}\"";
}
