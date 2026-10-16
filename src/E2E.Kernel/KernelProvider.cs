// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Kernel;

/// <summary>
/// Kernel's create-browser body plus the lease scope and the replay the web engine defines (upstream <c>KernelOptions</c>).
/// Neither <see cref="Scope"/> nor <see cref="Replay"/> nor <see cref="ReplayParams"/> goes to Kernel as a create-browser field.
/// </summary>
public sealed record KernelOptions : KernelBrowserParams
{
    /// <summary>
    /// <see cref="BrowserProviderScope.Worker"/> (default): one browser per worker slot, leased in prepare and released in
    /// finish. <see cref="BrowserProviderScope.Attempt"/>: a fresh browser per test attempt, reattached after a CDP
    /// transport drop; rules out <c>Headers</c>, <c>BasicAuth</c>, and <c>UserAgent</c>.
    /// </summary>
    public BrowserProviderScope? Scope { get; init; }

    /// <summary>
    /// Whether an attempt that records video is recorded as a Kernel replay of the browser's screen, saved as the attempt's
    /// <c>video/replay.mp4</c> (the default), or as the web engine's screencast of the page (<c>false</c>). A headless
    /// browser, which Kernel cannot replay, always gets the screencast. Upstream's <c>replay</c> is a boolean or
    /// <see cref="KernelReplayParams"/>; the port splits it in two properties, this one and <see cref="ReplayParams"/>.
    /// </summary>
    public bool Replay { get; init; } = true;

    /// <summary>Kernel's start-replay body (<c>framerate</c>, <c>max_duration_in_seconds</c>, <c>record_audio</c>) for the replay <see cref="Replay"/> asks for.</summary>
    public KernelReplayParams? ReplayParams { get; init; }
}

/// <summary>
/// Kernel browsers for <see cref="WebEngineOptions.Browser"/> (upstream <c>kernel()</c>): one hosted Chromium per worker slot,
/// or per attempt with <see cref="BrowserProviderScope.Attempt"/>, created when the engine asks and deleted when it gives the
/// lease back. Every browser is tagged with the run, target, slot, and attempt, and saves downloads to its own disk, read back
/// through Kernel's browser filesystem API. <c>KERNEL_API_KEY</c> comes from the run's environment, and <c>KERNEL_BASE_URL</c>
/// points at another Kernel API.
/// </summary>
public sealed class KernelProvider : IBrowserProvider
{
    private const string KernelApiKey = "KERNEL_API_KEY";

    private const string KernelBaseUrl = "KERNEL_BASE_URL";

    private const string DefaultBaseUrl = "https://api.onkernel.com/";

    /// <summary>
    /// Kernel terminates a browser idle this long: the backstop for a run that died before the engine could release or sweep its
    /// leases. Kernel's own default is shorter than a slow suite's gaps between attempts.
    /// </summary>
    private const int DefaultTimeoutSeconds = 600;

    private const string ReplayFile = "replay.mp4";

    /// <summary>Where each Kernel browser saves downloads on its own disk, for the engine to read back through the browser filesystem API.</summary>
    private const string DownloadsDir = "/tmp/e2e-downloads";

    private static readonly HttpClient SharedHttp = new() { Timeout = Timeout.InfiniteTimeSpan };

    private readonly KernelBrowserParams _params;

    private readonly HttpClient _http;

    /// <summary>Kernel replays headful browsers only; without a replay the engine records the screencast.</summary>
    private readonly KernelReplayParams? _replay;

    public KernelProvider(KernelOptions? options = null)
        : this(options, SharedHttp)
    {
    }

    internal KernelProvider(KernelOptions? options, HttpClient http)
    {
        options ??= new KernelOptions();
        _params = options;
        _http = http;
        Scope = options.Scope ?? BrowserProviderScope.Worker;
        _replay = !options.Replay || options.Headless == true ? null : options.ReplayParams ?? new KernelReplayParams();
        Downloads = new BrowserProviderDownloads
        {
            Dir = DownloadsDir,
            ReadAsync = (lease, file, context, cancellationToken) => ClientFor(context.Env).ReadFileAsync(lease.Id, file, cancellationToken),
        };
    }

    public string Name => "kernel";

    public BrowserProviderScope Scope { get; }

    public BrowserProviderDownloads? Downloads { get; }

    public async Task<BrowserLease> AcquireAsync(BrowserRequest request, CancellationToken cancellationToken)
    {
        var client = ClientFor(request.Env);
        var tags = new Dictionary<string, string>(_params.Tags ?? new Dictionary<string, string>())
        {
            ["e2e_run"] = request.RunId,
            ["e2e_target"] = request.TargetName,
            ["e2e_slot"] = request.Slot.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        if (request.AttemptId is not null)
        {
            tags["e2e_attempt"] = request.AttemptId;
        }

        var browser = await client.CreateAsync(_params with { TimeoutSeconds = _params.TimeoutSeconds ?? DefaultTimeoutSeconds, Tags = tags }, cancellationToken).ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            string outcome;
            try
            {
                await client.DeleteAsync(browser.SessionId, deadline.Token).ConfigureAwait(false);
                outcome = "deleted";
            }
            catch (Exception cause)
            {
                outcome = $"deleting it failed, so Kernel ends it after timeout_seconds: {cause.Message}";
            }

            throw new InvalidOperationException($"browser {browser.SessionId} arrived after the request was cancelled; {outcome}");
        }

        request.Log(browser.LiveViewUrl is null ? $"browser {browser.SessionId}" : $"browser {browser.SessionId}, watch at {browser.LiveViewUrl}");
        return new BrowserLease { Id = browser.SessionId, CdpEndpoint = browser.CdpWsUrl };
    }

    public async Task ReleaseAsync(BrowserLease lease, BrowserReleaseContext context, CancellationToken cancellationToken) =>
        await ClientFor(context.Env).DeleteAsync(lease.Id, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<string>> SweepAsync(BrowserReleaseContext context, CancellationToken cancellationToken)
    {
        var client = ClientFor(context.Env);
        var open = await client.ListActiveAsync(new Dictionary<string, string> { ["e2e_run"] = context.RunId, ["e2e_target"] = context.TargetName }, cancellationToken).ConfigureAwait(false);
        var settled = await Task.WhenAll(open.Select(async id =>
        {
            try
            {
                return (Deleted: await client.DeleteAsync(id, cancellationToken).ConfigureAwait(false) ? id : null, Failed: (string?)null);
            }
            catch (Exception cause)
            {
                return (Deleted: null, Failed: $"{id} ({cause.Message})");
            }
        })).ConfigureAwait(false);
        var deleted = settled.Select(result => result.Deleted).OfType<string>().ToList();
        var failed = settled.Select(result => result.Failed).OfType<string>().ToList();
        if (failed.Count > 0)
        {
            throw new InvalidOperationException($"deleted {(deleted.Count == 0 ? "none" : string.Join(", ", deleted))}; could not delete {string.Join(", ", failed)}, so Kernel ends it after timeout_seconds");
        }

        return deleted;
    }

    /// <summary>Records each attempt as a Kernel replay of the lease's screen, started with the replay body and saved as <c>replay.mp4</c>.</summary>
    public async Task<ProviderRecording?> RecordAsync(BrowserLease lease, ProviderRecordContext context, CancellationToken cancellationToken)
    {
        if (_replay is null)
        {
            return null;
        }

        var client = ClientFor(context.Env);
        var startedAt = DateTimeOffset.UtcNow;
        var replayId = await client.StartReplayAsync(lease.Id, _replay, cancellationToken).ConfigureAwait(false);
        return new KernelReplayRecording(client, lease.Id, replayId, startedAt);
    }

    /// <summary>The Kernel client for the run's API key, read from the run's environment and not the process's.</summary>
    private KernelClient ClientFor(IReadOnlyDictionary<string, string?> env)
    {
        var apiKey = KernelEnvironment.EnvValue(env, KernelApiKey) ?? throw new InvalidOperationException($"{KernelApiKey} is not set");
        var baseUrl = KernelEnvironment.EnvValue(env, KernelBaseUrl) ?? DefaultBaseUrl;
        return Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? new KernelClient(_http, apiKey, uri)
            : throw new InvalidOperationException($"{KernelBaseUrl} is not an http(s) URL");
    }

    /// <summary>A Kernel replay in progress. A retried stop (the download failed after the replay stopped) downloads again without stopping twice.</summary>
    private sealed class KernelReplayRecording(KernelClient client, string sessionId, string replayId, DateTimeOffset startedAt) : ProviderRecording
    {
        private bool _stopped;

        public override DateTimeOffset StartedAt => startedAt;

        public override async Task<ProviderRecordingResult> StopAsync(ProviderRecordingStopContext context, CancellationToken cancellationToken)
        {
            if (!_stopped)
            {
                await client.StopReplayAsync(sessionId, replayId, cancellationToken).ConfigureAwait(false);
                _stopped = true;
            }

            await client.DownloadReplayAsync(sessionId, replayId, Path.Combine(context.Dir, ReplayFile), cancellationToken).ConfigureAwait(false);
            return new ProviderRecordingResult { File = ReplayFile };
        }
    }
}
