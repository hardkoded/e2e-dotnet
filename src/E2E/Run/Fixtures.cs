// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;
using E2E.Reports;

namespace E2E;

/// <summary>What the producer of an artifact knows about it beyond its file (upstream's <c>ArtifactRegistration</c>).</summary>
internal sealed record ArtifactRegistration
{
    /// <summary>When a time-based artifact (a video segment) began recording.</summary>
    public DateTimeOffset? StartedAt { get; init; }
}

/// <summary>A provider-hosted recording (upstream's <c>ArtifactSink.link</c> argument).</summary>
internal sealed record ArtifactLink(string MediaType, DateTimeOffset StartedAt);

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
public sealed class App
{
    private readonly IEngineSession _session;
    private readonly string? _baseUrl;
    private readonly Func<CancellationToken> _token;

    private readonly StepRecorder? _steps;
    private readonly bool _pinnedApp;
    private readonly string _target;

    internal App(IEngineSession session, string? baseUrl, Func<CancellationToken> token, StepRecorder? steps, bool pinnedApp, string target)
    {
        _session = session;
        _pinnedApp = pinnedApp;
        _target = target;
        _baseUrl = baseUrl;
        _token = token;
        _steps = steps;
    }

    /// <summary>
    /// The resolved base URL of the target's app, with the port the run allocated when <c>app.url</c> declared port 0 and no trailing
    /// slash, so <c>$"{BaseUrl}/api/users"</c> joins with one slash; null when the test declares none. <see cref="OpenAsync"/> still
    /// resolves a relative URL against the base path.
    /// </summary>
    public string? BaseUrl => _baseUrl is null ? null : Urls.BaseUrlOf(_baseUrl);

    /// <summary>
    /// Opens a path or URL, resolved against the base URL. No URL opens the base URL itself.
    /// Throws <c>APP_URL_REQUIRED</c> for a relative URL when the test declares no base URL, and <c>POLICY_DENIED</c>
    /// for a malformed URL or a scheme other than <c>http:</c> or <c>https:</c> (the exact <c>about:blank</c> is admitted).
    /// </summary>
    public Task OpenAsync(string? url = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_baseUrl) && _pinnedApp)
        {
            return LaunchPinnedAppAsync(url, cancellationToken);
        }

        return RunStepAsync("app.open", url ?? "/", () => _session.OpenAsync(Routes.Resolve(_baseUrl, url), Token(cancellationToken)));
    }

    /// <summary>
    /// <c>app.open()</c> on a target with no URL whose engine pins an app, a device: it launches the pinned app fresh, and is what a test
    /// calls before it looks at the screen. A path has nowhere to go there; a link goes through the device fixture's <c>OpenLinkAsync</c>.
    /// Upstream's <c>launchPinnedApp</c> in <c>run/fixtures.ts</c>.
    /// </summary>
    private Task LaunchPinnedAppAsync(string? url, CancellationToken cancellationToken)
    {
        return RunStepAsync("app.open", "", async () =>
        {
            if (url is not null)
            {
                throw new ConfigurationException(
                    "APP_URL_REQUIRED",
                    "app.open(\"" + url + "\") needs an app URL and target \"" + _target + "\" has none: on a device app.open() launches the pinned app, and a link goes through device.openLink");
            }

            await _session.RestartAsync(Token(cancellationToken)).ConfigureAwait(false);
        });
    }

    /// <summary>Goes back one entry in the history. With no earlier entry it does nothing.</summary>
    public Task BackAsync(CancellationToken cancellationToken = default)
    {
        return RunStepAsync("app.back", "", () => _session.BackAsync(Token(cancellationToken)));
    }

    /// <summary>Closes the current document and reopens the app at <see cref="BaseUrl"/>. Cookies and storage are kept.</summary>
    public Task RestartAsync(CancellationToken cancellationToken = default)
    {
        return RunStepAsync("app.restart", "", () => SteerAsync(_session.RestartAsync, Token(cancellationToken)));
    }

    /// <summary>Discards cookies, storage, and history, and reopens the app at <see cref="BaseUrl"/>.</summary>
    public Task ClearStateAsync(CancellationToken cancellationToken = default)
    {
        return RunStepAsync("app.clearState", "", () => SteerAsync(_session.ClearStateAsync, Token(cancellationToken)));
    }

    /// <summary>
    /// Captures a redacted evidence screenshot: secure fields are masked at the source. The file is an attempt artifact under
    /// <c>screenshots/</c>, and the returned path is absolute (upstream's <c>app.screenshot</c>, <c>createApp</c> in <c>run/fixtures.ts</c>).
    /// Throws <c>UNSUPPORTED_CAPABILITY</c> on an engine that keeps no screenshots.
    /// </summary>
    /// <param name="label">Names the step and joins the file name.</param>
    /// <param name="cancellationToken">Cancels the capture.</param>
    public async Task<string> ScreenshotAsync(string? label = null, CancellationToken cancellationToken = default)
    {
        var path = "";
        var artifactsDir = ArtifactsDir ?? throw new EngineException(EngineErrorCodes.InvalidState, "screenshot outside an attempt: the app has no artifact directory");
        await RunStepAsync("app.screenshot", label ?? "", async () =>
        {
            // .NET-only hardening: the label joins the file name and the artifact path, so a secret spelled into it stops here.
            var relative = await _session.ScreenshotArtifactAsync(label is null ? null : Redact(label), Token(cancellationToken)).ConfigureAwait(false);
            Attach("screenshot", relative);
            path = Path.GetFullPath(Path.Combine(artifactsDir, relative));
        }).ConfigureAwait(false);
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
    public async Task StartRecordingAsync(RecordingOptions? options = null, CancellationToken cancellationToken = default)
    {
        var recordings = Recordings ?? throw new EngineException(EngineErrorCodes.InvalidState, "recording outside an attempt: the app has no recorder");
        _ = SinkOf() ?? throw new EngineException(EngineErrorCodes.InvalidState, "recording outside an attempt: the app has no artifact directory");
        var name = options?.Name;
        await RunStepAsync("app.startRecording", name ?? "", () => recordings.StartSpanAsync(name, Token(cancellationToken))).ConfigureAwait(false);
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
        var recordings = Recordings ?? throw new EngineException(EngineErrorCodes.InvalidState, "recording outside an attempt: the app has no recorder");
        var sink = SinkOf() ?? throw new EngineException(EngineErrorCodes.InvalidState, "recording outside an attempt: the app has no artifact directory");
        IReadOnlyList<RecordedVideo> videos = [];
        await RunStepAsync("app.stopRecording", "", async () =>
        {
            var stopped = await recordings.StopSpanAsync(sink, Token(cancellationToken)).ConfigureAwait(false);
            foreach (var id in stopped.Ids)
            {
                _steps?.AttachArtifact(id);
            }

            videos = stopped.Videos;
        }).ConfigureAwait(false);
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

    // Each call that steers the app is a recorded step of the attempt, or runs as it is without a timeline.
    private Task RunStepAsync(string api, string label, Func<Task> body) =>
        _steps is null ? body() : _steps.RunAsync(StepKind.App, api, label, body);

    // A steering hook ends at a blank surface, so the app is reopened at its base URL
    // inside the same step. Without a base URL there is nothing to reopen.
    private async Task SteerAsync(Func<CancellationToken, Task> hook, CancellationToken cancellationToken)
    {
        await hook(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(_baseUrl))
        {
            await _session.OpenAsync(Routes.Resolve(_baseUrl, null), cancellationToken).ConfigureAwait(false);
        }
    }

    private CancellationToken Token(CancellationToken cancellationToken) =>
        Time.Link(cancellationToken, _token());
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
