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

    /// <summary>The target base URL relative URLs resolve against, or null when the test declares none.</summary>
    public string? BaseUrl => _baseUrl;

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
