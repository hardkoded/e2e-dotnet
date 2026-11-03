// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Mobile;
using E2E.Mobile.Lease;

namespace E2E.BrowserStack;

/// <summary>
/// BrowserStack App Automate's real Android and iOS devices as an <see cref="IDeviceProvider"/> for <see cref="MobileOptions.Provider"/>
/// (upstream's <c>browserstack()</c>), through agent-device's built-in <c>browserstack</c> provider: one real device and isolated
/// agent-device daemon per worker slot. The session starts, and installs <see cref="BrowserStackDevicesOptions.App"/>, when the daemon
/// allocates the lease; releasing the lease stops the daemon, which ends the session. The credentials are <c>BROWSERSTACK_USERNAME</c>
/// and <c>BROWSERSTACK_ACCESS_KEY</c> in the run's environment; they reach the daemon alone and are never logged.
/// </summary>
public sealed partial class BrowserStackDevices : IDeviceProvider
{
    /// <summary>agent-device's name for BrowserStack, as the lease provider and the tenant.</summary>
    private const string Provider = "browserstack";

    /// <summary>The dashboard project sessions are grouped under when <c>Project</c> is absent.</summary>
    private const string DefaultProject = "e2e";

    private static readonly HttpClient SharedHttp = new() { Timeout = Timeout.InfiniteTimeSpan };

    private readonly BrowserStackDevicesOptions _options;
    private readonly IAgentDeviceLeases _leases;
    private readonly HttpClient _http;
    private readonly bool _noResignApp;
    private readonly object _uploadGate = new();

    /// <summary>
    /// The <c>https</c> build's upload for one run, API endpoint, and account, whose <c>bs://</c> id every slot of that run shares.
    /// Anything else uploads again, so a later run never installs an earlier run's build, and a failed upload may be tried again.
    /// </summary>
    private (string Key, Task<string> Id)? _uploaded;

    public BrowserStackDevices(BrowserStackDevicesOptions options)
        : this(options, static leaseOptions => new AgentDeviceLeases(leaseOptions), SharedHttp)
    {
    }

    internal BrowserStackDevices(BrowserStackDevicesOptions options, Func<AgentDeviceLeaseOptions, IAgentDeviceLeases> leases, HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = Validated(options);
        _leases = leases(new AgentDeviceLeaseOptions { Provider = Provider, Label = "BrowserStack" });
        _http = http;
        _noResignApp = options.NoResignApp == true;
    }

    public string Name => Provider;

    public async Task<DeviceLease> AcquireAsync(DeviceRequest request, CancellationToken cancellationToken)
    {
        if (request.AppPath is not null)
        {
            throw new InvalidOperationException("BrowserStack installs the app from `App`; leave the target's `app.appPath` out");
        }

        if (_noResignApp && request.Platform != MobilePlatform.Ios)
        {
            throw Invalid("`NoResignApp` applies to iOS only");
        }

        var credentials = BrowserStackCredentials.FromEnvironment(request.Env);
        cancellationToken.ThrowIfCancellationRequested();
        var appId = await ProviderAppAsync(request, credentials, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var sessionName = SlotSessionName(_options.SessionName, request);
        var selectors = new LeaseSelectors
        {
            Device = _options.Device,
            ProviderOsVersion = _options.OsVersion,
            ProviderApp = appId,
            ProviderProject = _options.Project ?? DefaultProject,
            ProviderBuild = _options.Build ?? request.RunId,
            ProviderSessionName = sessionName,
            ProviderDeviceOrientation = _options.Orientation?.WireName(),
            ProviderGeoLocation = _options.GeoLocation,
            ProviderTimezone = _options.Timezone,
            ProviderLanguage = _options.Language,
            ProviderLocale = _options.Locale,
            ProviderAppiumVersion = _options.AppiumVersion,
            ProviderNetworkProfile = _options.NetworkProfile,
            ProviderCustomNetwork = _options.CustomNetwork,

            // A flag is allocated only when set.
            ProviderNoResignApp = _noResignApp ? true : null,
        };
        return await _leases.AcquireAsync(
            request,
            new AgentDeviceLeaseRequest
            {
                Env = new Dictionary<string, string> { ["BROWSERSTACK_USERNAME"] = credentials.Username, ["BROWSERSTACK_ACCESS_KEY"] = credentials.AccessKey },
                Selectors = selectors,
                Summary = $"{_options.Device}, {request.Platform.WireName()} {_options.OsVersion}; session {sessionName} reserved",
            },
            cancellationToken).ConfigureAwait(false);
    }

    public Task ReleaseAsync(DeviceLease lease, DeviceReleaseContext context, CancellationToken cancellationToken) => _leases.ReleaseAsync(lease, cancellationToken);

    /// <summary>Runs in the worker, from the lease alone. BrowserStack records the whole session, so its video starts when the session did.</summary>
    public async Task<ProviderRecording?> RecordAsync(DeviceLease lease, ProviderRecordContext context, CancellationToken cancellationToken)
    {
        var calledAt = DateTimeOffset.UtcNow;
        var session = SessionRefOf(lease)
            ?? throw new InvalidOperationException($"lease {lease.Id} carries no BrowserStack build and session name to find its recording by");
        var credentials = BrowserStackCredentials.FromEnvironment(context.Env);
        var endpoint = BrowserStackSessions.ApiEndpoint(context.Env);
        var found = await BrowserStackSessions.FindSessionAsync(_http, endpoint, credentials, session, cancellationToken).ConfigureAwait(false);

        // Without a usable start time from BrowserStack, the moment recording was asked for is the closest known bound.
        return new SessionRecording(_http, endpoint, credentials, session, found.StartedAt ?? calledAt);
    }

    /// <summary>The recording of a session BrowserStack keeps: its video is linked once the session ends.</summary>
    private sealed class SessionRecording(HttpClient http, string endpoint, BrowserStackCredentials credentials, SessionRef session, DateTimeOffset startedAt) : ProviderRecording
    {
        public override DateTimeOffset StartedAt { get; } = startedAt;

        public override async Task<ProviderRecordingResult> StopAsync(ProviderRecordingStopContext context, CancellationToken cancellationToken)
        {
            var ended = await BrowserStackSessions.FindSessionAsync(http, endpoint, credentials, session, cancellationToken).ConfigureAwait(false);
            return ended.VideoUrl is null
                ? throw new InvalidOperationException($"BrowserStack session {ended.Id} ({Json(session.SessionName)}, build {Json(session.Build)}) reports no video URL")
                : new ProviderRecordingResult { Url = ended.VideoUrl, MediaType = "video/mp4" };
        }
    }

    /// <summary>
    /// What the daemon is given as <c>providerApp</c>: an <c>https</c> build is uploaded once for the run and becomes its <c>bs://</c> id,
    /// anything else is read as <c>App</c> is.
    /// </summary>
    private Task<string> ProviderAppAsync(DeviceRequest request, BrowserStackCredentials credentials, CancellationToken cancellationToken)
    {
        if (!HttpsUrl().IsMatch(_options.App))
        {
            return Task.FromResult(AppSource(_options.App, request.ProjectRoot));
        }

        var endpoint = BrowserStackSessions.ApiEndpoint(request.Env);
        var key = new JsonArray(request.RunId, endpoint, credentials.Username).ToJsonString();
        Task<string> upload;
        lock (_uploadGate)
        {
            if (_uploaded is { } existing && existing.Key == key)
            {
                return existing.Id;
            }

            upload = BrowserStackSessions.UploadAppFromUrlAsync(_http, endpoint, credentials, _options.App, cancellationToken);
            _uploaded = (key, upload);
        }

        // The URL may carry a signed token, so the log names the upload by its id only.
        _ = upload.ContinueWith(
            task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    try
                    {
                        request.Log($"uploaded the app URL as {task.Result}");
                    }
                    catch (Exception)
                    {
                        // The run's log may be closed by now.
                    }

                    return;
                }

                lock (_uploadGate)
                {
                    if (_uploaded is { } current && ReferenceEquals(current.Id, upload))
                    {
                        _uploaded = null;
                    }
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return upload;
    }

    /// <summary>Throws <c>INVALID_CONFIG</c> unless every option is one BrowserStack takes (upstream's <c>checkOption</c> and the checks in <c>browserstack()</c>).</summary>
    private static BrowserStackDevicesOptions Validated(BrowserStackDevicesOptions options)
    {
        Required(nameof(options.Device), options.Device);
        Required(nameof(options.OsVersion), options.OsVersion);
        Required(nameof(options.App), options.App);
        foreach (var (name, value) in new (string, string?)[]
        {
            (nameof(options.Project), options.Project),
            (nameof(options.Build), options.Build),
            (nameof(options.SessionName), options.SessionName),
            (nameof(options.GeoLocation), options.GeoLocation),
            (nameof(options.Timezone), options.Timezone),
            (nameof(options.Language), options.Language),
            (nameof(options.Locale), options.Locale),
            (nameof(options.AppiumVersion), options.AppiumVersion),
            (nameof(options.NetworkProfile), options.NetworkProfile),
            (nameof(options.CustomNetwork), options.CustomNetwork),
        })
        {
            if (value is not null && string.IsNullOrWhiteSpace(value))
            {
                throw Invalid($"`{name}` must be a non-empty string");
            }
        }

        if (options.Orientation is { } orientation && !Enum.IsDefined(orientation))
        {
            throw Invalid($"`Orientation` must be Portrait or Landscape, not {(int)orientation}");
        }

        if (HttpUrl().IsMatch(options.App))
        {
            throw Invalid("`App` must be an https URL, a `bs://` id, or a local path, not a plain http URL");
        }

        if (options.NetworkProfile is not null && options.CustomNetwork is not null)
        {
            throw Invalid("set `NetworkProfile` or `CustomNetwork`, not both");
        }

        return options;
    }

    private static void Required(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Invalid($"`{name}` is required, as a non-empty string");
        }
    }

    private static ConfigurationException Invalid(string message) => new("INVALID_CONFIG", $"BrowserStackDevices: {message}");

    /// <summary>
    /// The dashboard name of a slot's session, unique among the run's slots of the target so <see cref="RecordAsync"/> can find the
    /// session by it within its build.
    /// </summary>
    private static string SlotSessionName(string? sessionName, DeviceRequest request)
    {
        if (sessionName is null)
        {
            return $"e2e-{request.RunId}-{request.TargetName}-{request.Slot + 1}";
        }

        return request.Slots > 1 ? $"{sessionName}-{request.Slot + 1}" : sessionName;
    }

    /// <summary><c>App</c> as the daemon reads it: a <c>bs://</c> id as written, a local path resolved against the project root, never the daemon's working directory.</summary>
    private static string AppSource(string app, string projectRoot) =>
        Path.IsPathRooted(app) || UrlScheme().IsMatch(app) ? app : Path.GetFullPath(app, projectRoot);

    /// <summary>The build and session name <see cref="AcquireAsync"/> put on a lease's <c>Client</c>, when they are there.</summary>
    private static SessionRef? SessionRefOf(DeviceLease lease) =>
        Text(lease.Client?["providerBuild"]) is { Length: > 0 } build && Text(lease.Client?["providerSessionName"]) is { Length: > 0 } sessionName
            ? new SessionRef(build, sessionName)
            : null;

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static string Json(string text) => JsonSerializer.Serialize(text, JsonOptions);

    [GeneratedRegex(@"^https://", RegexOptions.IgnoreCase)]
    private static partial Regex HttpsUrl();

    [GeneratedRegex(@"^http://", RegexOptions.IgnoreCase)]
    private static partial Regex HttpUrl();

    [GeneratedRegex(@"^[a-z][a-z0-9+.-]*://", RegexOptions.IgnoreCase)]
    private static partial Regex UrlScheme();
}

/// <summary>The wire names of <see cref="BrowserStackOrientation"/>.</summary>
internal static class BrowserStackOrientations
{
    public static string WireName(this BrowserStackOrientation orientation) => orientation switch
    {
        BrowserStackOrientation.Portrait => "portrait",
        BrowserStackOrientation.Landscape => "landscape",
        _ => throw new ArgumentOutOfRangeException(nameof(orientation), orientation, "Unknown orientation."),
    };
}
