// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Desktop;

/// <summary>
/// The desktop engine for e2e: a desktop body that implements the core engine contract, validated by the same rules and graded by the same
/// capabilities as any other engine. It drives the target's macOS app on this machine, one worker at a time. A test launches the app with
/// <c>app.open()</c>; every test ends with the app quit. A run that includes this target stops before any test starts on any OS but macOS.
/// Port of upstream's <c>desktop()</c> in <c>@e2e-dev/desktop</c> (<c>engine.ts</c>); the session each attempt gets is <see cref="DesktopSession"/>.
/// </summary>
public sealed partial class DesktopEngine : IEngine<DesktopFixtures>, IDoctorEngine, IPreparableEngine
{
    /// <summary>The version of the engine. Upstream's <c>version</c>, read from its <c>package.json</c>.</summary>
    public const string EngineVersion = "0.0.1";

    /// <summary>What <c>perform</c> honors without physical input, on macOS: the accessibility API, and keys sent into the app's own event queue, which reach its key window while it stays in the background.</summary>
    private static readonly string[] Actions =
    [
        "tap",
        "secondaryTap",
        "fill",
        "clear",
        "check",
        "uncheck",
        "focus",
        "scrollIntoView",
        "selectOption",
        "swipe",
        "press",
    ];

    /// <summary>What physical input adds: a double click and the pointer over a control, which need the real pointer, and taps at a point.</summary>
    private static readonly string[] HeadedActions = [.. Actions, "doubleTap", "hover"];

    private static readonly string[] HeadedPointerActions = ["tap", "doubleTap", "secondaryTap", "hover"];

    private readonly Func<IDesktopDriver>? _loadDriver;
    private readonly List<DesktopSession> _sessions = [];

    /// <summary>Creates the engine. Upstream's <c>desktop(options)</c>.</summary>
    public DesktopEngine(DesktopOptions? options = null)
        : this(options, null) { }

    /// <summary>Creates the engine over a driver of the caller's making: the seam unit tests script. Upstream's <c>loadDriver</c> parameter of <c>DesktopSurface</c>.</summary>
    internal DesktopEngine(DesktopOptions? options, Func<IDesktopDriver>? loadDriver)
    {
        Options = DesktopOptionsResolver.Resolve(options);
        _loadDriver = loadDriver;
    }

    /// <summary>The options with defaults applied.</summary>
    public ResolvedDesktopOptions Options { get; }

    /// <summary>The host the engine asks whether it is macOS. .NET-only: upstream's tests replace <c>process.platform</c>.</summary>
    internal DesktopHost Host { get; init; } = DesktopHost.Current;

    /// <summary>The sessions this engine started that have not ended: the attempts running on it right now. The framework adapters read the test's from it.</summary>
    public IReadOnlyList<DesktopSession> ActiveSessions
    {
        get
        {
            lock (_sessions)
            {
                return [.. _sessions];
            }
        }
    }

    /// <inheritdoc />
    public string Name => "desktop";

    /// <inheritdoc />
    public string Platform => DesktopOptionsResolver.Platform;

    /// <inheritdoc />
    public string Version => EngineVersion;

    /// <summary>
    /// Observation, actions, location, a keyboard, the window swipe, screenshots, and the pinned app. No history: a desktop app has no back stack the
    /// engine can see. No video. No tap modifiers: an accessibility press holds no keys.
    /// </summary>
    public EngineCapabilities Capabilities =>
        EngineCapabilities.Observation
        | EngineCapabilities.Actions
        | EngineCapabilities.Location
        | EngineCapabilities.Keyboard
        | EngineCapabilities.Scroll
        | EngineCapabilities.Screenshot
        | EngineCapabilities.PinnedApp;

    /// <summary>The desktop engine reads the object form of <c>app.launchArguments</c>, spelling it as Apple user defaults.</summary>
    bool IEngine.LaunchArgumentValues => true;

    /// <summary>The locator action kinds this engine declares. Upstream's <c>actions</c>.</summary>
    public IReadOnlyCollection<string> ActionKinds => Options.Headed ? HeadedActions : Actions;

    /// <summary>The pointer action kinds this engine declares: only with <c>Headed</c>. Upstream's <c>pointerActions</c>.</summary>
    public IReadOnlyCollection<string> PointerActionKinds => Options.Headed ? HeadedPointerActions : [];

    /// <summary>
    /// A desktop target launches an app: <c>bundleId</c> names an installed one (a bundle id or an app name), or <c>appPath</c> names a built
    /// <c>.app</c> bundle. Upstream's <c>validateApp</c>.
    /// </summary>
    void IEngine.ValidateApp(EngineAppDeclaration app, EngineAppCheckInfo info)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(info);
        var where = $"target \"{info.TargetName}\"";
        if (app.Url is not null)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                $"{where} declares app.url, but a desktop() target drives an installed app; name it with app.bundleId or app.appPath"
            );
        }

        if (app.BundleId is null && app.AppPath is null)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                $"{where} needs app.bundleId or app.appPath: desktop() launches an installed app, such as app: {{ bundleId: 'com.example.app' }} or app: {{ appPath: 'build/Example.app' }}"
            );
        }

        if (app.AppPath is not null && !AppBundlePath().IsMatch(app.AppPath))
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                $"{where} app.appPath must name a macOS .app bundle, got {System.Text.Json.JsonSerializer.Serialize(app.AppPath)}"
            );
        }

        if (app.Permissions is not null || app.AllPermissions)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                $"{where} declares app.permissions, which a desktop() target cannot set"
            );
        }
    }

    /// <summary>Loads the native driver once in the runner, so a machine without it fails before any test starts. Upstream's <c>prepare</c>.</summary>
    public Task<EnginePrepareResult> PrepareAsync(EnginePrepareInfo info, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(info);
        DesktopOptionsResolver.RequireMacOS(Host.IsMacOS, Host.Description);
        var surface = NewSurface();
        return Task.FromResult(new EnginePrepareResult { Workers = surface.Prepare() });
    }

    /// <summary>Nothing outlives an attempt on this engine: each session quits its app and removes its state directory. .NET-only: upstream's engine has no <c>finish</c>, and <see cref="IPreparableEngine"/> declares it.</summary>
    public Task FinishAsync(EngineFinishInfo info, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>The doctor of the desktop engine. Upstream's <c>doctor</c>.</summary>
    public async Task<IReadOnlyList<EngineDoctorCheck>> DoctorAsync(
        EngineDoctorInfo info,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(info);
        return Host.IsMacOS
            ? await NewSurface().DoctorAsync(info).ConfigureAwait(false)
            :
            [
                new EngineDoctorCheck(
                    "platform",
                    EngineDoctorStatus.Fail,
                    DesktopOptionsResolver.UnsupportedPlatform(Host.Description)
                ),
            ];
    }

    /// <summary>Starts one attempt: a session over a surface of its own. The app is launched by <c>app.open()</c>, as on a device. Upstream's <c>init</c> and <c>startAttempt</c>.</summary>
    public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        DesktopOptionsResolver.RequireMacOS(Host.IsMacOS, Host.Description);
        var surface = NewSurface();
        var cleanup = (int)Math.Min(E2EDefaults.CleanupTimeout.TotalMilliseconds, int.MaxValue);
        try
        {
            await surface
                .InitAsync(
                    new DesktopInitInfo
                    {
                        ProjectRoot = options.ProjectRoot ?? Directory.GetCurrentDirectory(),
                        App = new EngineAppInfo
                        {
                            BundleId = options.BundleId,
                            AppPath = options.AppPath,
                            LaunchArguments = options.LaunchArguments,
                            LaunchArgumentValues = options.LaunchArgumentValues,
                        },
                        Headed = options.Headed,
                        Env = options.Env ?? EnvironmentVariables(),
                        Signal = cancellationToken,
                    }
                )
                .ConfigureAwait(false);
            surface.StartAttempt(
                new DesktopAttemptContext
                {
                    ArtifactsDir = options.ArtifactsDir ?? Path.Combine(Path.GetTempPath(), "e2e-desktop-artifacts"),
                    Screen = options.Screen,
                    Environment = options.Environment,
                }
            );
        }
        catch
        {
            await surface.DisposeAsync(CancellationToken.None, cleanup).ConfigureAwait(false);
            throw;
        }

        var session = new DesktopSession(this, surface, options.ActionTimeout);
        lock (_sessions)
        {
            _sessions.Add(session);
        }

        return session;
    }

    internal void Unregister(DesktopSession session)
    {
        lock (_sessions)
        {
            _sessions.Remove(session);
        }
    }

    /// <summary>The fixtures of a session on this engine: its <c>Desktop</c>. A session no desktop engine started is a <c>WORKER_PROTOCOL</c> failure.</summary>
    public DesktopFixtures CreateFixtures(IEngineSession session, Session controls) =>
        session is DesktopSession desktop
            ? new DesktopFixtures(desktop.Desktop)
            : throw new EngineException(
                "WORKER_PROTOCOL",
                "a desktop engine builds its fixtures from the desktop session it started"
            );

    internal DesktopSurface NewSurface() => new(Options, Platform, _loadDriver);

    private static Dictionary<string, string?> EnvironmentVariables()
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            env[(string)entry.Key] = entry.Value as string;
        }

        return env;
    }

    [GeneratedRegex(@"\.app/?\z", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AppBundlePath();
}

/// <summary>Whether the machine is a Mac, and what it is called in a message (Node's <c>process.platform</c> name). .NET-only: upstream reads <c>process.platform</c>, which its tests replace.</summary>
internal sealed record DesktopHost(bool IsMacOS, string Description)
{
    public static DesktopHost Current { get; } =
        new(OperatingSystem.IsMacOS(), DesktopOptionsResolver.ProcessPlatform());
}
