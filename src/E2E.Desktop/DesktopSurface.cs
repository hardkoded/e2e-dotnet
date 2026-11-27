// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;

namespace E2E.Desktop;

/// <summary>One window of the app under test, as <c>desktop.windows()</c> lists it. Upstream's <c>DesktopWindow</c>.</summary>
/// <param name="Id">The window server's id of the window, valid while the window stays open.</param>
/// <param name="Title">The window's title.</param>
/// <param name="Focused">Whether it is the app's key window.</param>
public sealed record DesktopWindow(string Id, string Title, bool Focused);

/// <summary>What a desktop worker is given when it initializes. Upstream's <c>EngineInitInfo</c>, which the port's engine builds from its start options.</summary>
internal sealed record DesktopInitInfo
{
    public required string ProjectRoot { get; init; }

    public required EngineAppInfo App { get; init; }

    public bool Headed { get; init; }

    public IReadOnlyDictionary<string, string?> Env { get; init; } = new Dictionary<string, string?>();

    public CancellationToken Signal { get; init; }

    public Action<string> Log { get; init; } = static _ => { };
}

/// <summary>What one attempt is given when it starts. Upstream's <c>EngineAttemptContext</c>.</summary>
internal sealed record DesktopAttemptContext
{
    public required string ArtifactsDir { get; init; }

    public Action<Observation>? Screen { get; init; }

    public Action<IReadOnlyDictionary<string, string>>? Environment { get; init; }
}

/// <summary>
/// One worker's desktop session: the macOS app the target declares, driven through the engine's native driver (<see cref="IDesktopDriver"/>). The
/// app is launched in the background, and every action goes through the accessibility API or into the app's own event queue, so the user's
/// pointer, focus, and typing stay where they are, unless the target opts into physical input with <c>headed</c>. Port of upstream's
/// <c>DesktopSurface</c> in <c>surface.ts</c>.
/// </summary>
internal sealed partial class DesktopSurface
{
    /// <summary>How long a quit waits for a launch whose outcome was unknown to report the process it started.</summary>
    private const int PendingSettleMs = 3_000;

    /// <summary>How much of a quit's budget is kept for a killed process to exit.</summary>
    private const int ExitWaitMs = 500;

    /// <summary>How long a quit looks for an app a launch that answered without one may still have started.</summary>
    private const int LateAppMs = 2_000;

    /// <summary>How often it looks.</summary>
    private const int LateAppPollMs = 100;

    /// <summary>How long a launch waits for its process, and then for the app's window to become readable, before it reports why it did not.</summary>
    private const int LaunchReadyMs = 10_000;

    /// <summary>The cause of an input that was never sent, because the operation's budget ran out first.</summary>
    private static readonly Exception NotSent = new InvalidOperationException(
        "the operation ran out of budget before anything was sent"
    );

    /// <summary>The cause of a read that found the app's windows but could read none of them.</summary>
    private static readonly Exception Unreadable = new InvalidOperationException("no window of the app could be read");

    /// <summary>The cause of a read of an app that quit since it launched.</summary>
    private static readonly Exception Quit = new InvalidOperationException("the app quit");

    /// <summary>The cause of a screenshot withheld because its secure fields could not be shown masked, which the harness reads as masking it could not prove.</summary>
    private static readonly Exception Unmasked = new InvalidOperationException(
        "the secure fields of the screenshot could not be placed"
    );

    /// <summary>Launch arguments every launch gets first: no window or document the app saved at its last quit comes back.</summary>
    private static readonly string[] FreshStateArguments = ["-ApplePersistenceIgnoreState", "YES"];

    /// <summary>The most elements one driver snapshot reads (<c>MAX_NODES</c> in <c>crates/e2e-desktop-napi/src/snapshot.rs</c>): a larger window is read in part.</summary>
    private const string MaxSnapshotNodes = "8,000";

    private readonly Func<IDesktopDriver> _loadDriver;
    private IDesktopDriver? _driver;
    private string? _home;
    private EngineAppInfo _app = new();
    private string _projectRoot = Directory.GetCurrentDirectory();

    /// <summary>What <c>app.open()</c> launches: the target's <c>appPath</c> bundle, resolved, else its <c>bundleId</c>.</summary>
    private LaunchTarget? _launchTarget;

    /// <summary>Whether the app is launched in the foreground: with <c>desktop({ headed })</c>, or a run that asked for a visible surface (<c>--headed</c>). Physical input stays with <c>desktop({ headed })</c> alone, since the engine declares it when it is created.</summary>
    private bool _foreground;

    /// <summary>The app's name, for messages and the observed location.</summary>
    private string? _appName;

    /// <summary>The process the engine launched, which it alone quits; another instance of the same app is never touched.</summary>
    private int? _pid;

    /// <summary>When that process started, which a later process given the same id does not share.</summary>
    private long? _started;

    /// <summary>The token the launch put in that process's environment.</summary>
    private string? _token;

    /// <summary>
    /// A launch whose outcome is unknown (it timed out, or failed after it may have started the app), so the next quit can still find and end the
    /// process it started.
    /// </summary>
    private PendingLaunch? _pending;

    private Attempt? _attempt;

    /// <summary>The launch or quit running now, which the next one waits for.</summary>
    private Task _lifecycle = Task.CompletedTask;

    private readonly object _lifecycleLock = new();

    /// <summary>The window a test chose with <c>desktop.useWindow</c>, which every read and command then uses until the app quits.</summary>
    private string? _chosenWindow;

    /// <summary>
    /// The window of the newest screen handed to the harness, by observe or locate: the one a point is measured in, the agent's as core hit-tests it,
    /// against the newest observation. <c>Sheet</c> says whether a sheet held its focus then: one that opened since was not where the point was taken.
    /// </summary>
    private PointWindow? _pointWindow;

    /// <summary>The newest binding of every node id an observation or a locate handed out, newest last.</summary>
    private readonly Dictionary<string, NodeBinding> _bindings = new(StringComparer.Ordinal);

    private readonly List<string> _bindingOrder = [];

    /// <summary>The id handed out for each node identity since the app launched; see <see cref="DesktopNodes.ProjectSnapshot"/>.</summary>
    private readonly Dictionary<string, string> _ids = new(StringComparer.Ordinal);

    private int _sequence;
    private string _driverVersion = "unknown";

    /// <summary>The run's environment cut to <c>INHERITED_ENV</c>: all the app is given of it.</summary>
    private IReadOnlyDictionary<string, string> _appEnv = new Dictionary<string, string>();

    /// <summary><paramref name="loadDriver"/> is how the native driver is loaded; tests pass a scripted one. Upstream's <c>loadDriver</c> parameter.</summary>
    public DesktopSurface(ResolvedDesktopOptions options, string platform, Func<IDesktopDriver>? loadDriver = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
        Platform = platform;
        _loadDriver = loadDriver ?? DesktopDriverCalls.LoadNativeDriver;
    }

    public ResolvedDesktopOptions Options { get; }

    /// <summary>The worker's private state directory, which the tests read for the owner file and the launch record. .NET-only: upstream's tests read the private <c>home</c> field by a cast.</summary>
    internal string? Home => _home;

    /// <summary>The id handed out for each node identity, which the tests read for what a window's reads retire. .NET-only: upstream's tests read the private <c>ids</c> field by a cast.</summary>
    internal IReadOnlyDictionary<string, string> Ids => _ids;

    public string Platform { get; }

    /// <summary>What a target launches: a <c>.app</c> bundle on disk, or an installed app by bundle id or name.</summary>
    private sealed record LaunchTarget(string? Path, string? BundleId, string Label);

    /// <summary>
    /// A launch whose outcome is unknown: its token, and the driver's answer, which may still name the process it started, or say it started none
    /// (<c>not_started</c>: refused before <c>open</c> ran).
    /// </summary>
    private sealed record PendingLaunch(string Target, string Token, Task<LaunchOutcome> Settled);

    /// <summary>What a launch answered: the process, none started (<see cref="NotStarted"/>), or nothing known.</summary>
    private sealed record LaunchOutcome(Launched? Launched, bool NotStarted);

    /// <summary>What a state directory's launch record keeps: the launch's token, and once the process is known, its id and when it started.</summary>
    private sealed record LaunchRecord(string Token, int? Pid = null, long? Started = null);

    /// <summary>One attempt's bookkeeping.</summary>
    private sealed class Attempt(string artifactsDir, Action<Observation>? screen)
    {
        public string ArtifactsDir { get; } = artifactsDir;

        public int Screenshots;

        public Action<Observation>? Screen { get; } = screen;
    }

    /// <summary>The window a screen was read from, and the sheet that held its focus then: where a point taken in it was taken.</summary>
    private sealed record PointWindow(string Id, string? Sheet);

    // --- lifecycle ---

    /// <summary>Loads the native driver once in the runner, so a machine without it fails before any test starts. One worker per target: two would share one screen and one app. Upstream's <c>prepare</c>.</summary>
    public int Prepare()
    {
        _ = _loadDriver();
        return 1;
    }

    /// <summary>Upstream's <c>init</c>.</summary>
    public async Task InitAsync(DesktopInitInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        _projectRoot = info.ProjectRoot;
        _app = info.App;
        // An engine handle can serve one host after another: nothing of the last one's app carries over. A launch it could not settle stays in its
        // launch record, which the sweep below takes.
        _appName = null;
        _pid = null;
        _started = null;
        _token = null;
        _pending = null;
        Forget();
        _foreground = Options.Headed || info.Headed;
        var driver = _driver = _loadDriver();
        _appEnv = LaunchOwnership.InheritedEnv(info.Env);
        // A worker that was killed left its launch record behind, and maybe the app it launched.
        await LaunchOwnership.SweepStateDirectoriesAsync(driver).ConfigureAwait(false);
        // A private state directory per worker, named for the process that owns it so a later sweep can tell it was left behind.
        _home = Directory.CreateTempSubdirectory(LaunchOwnership.StatePrefix).FullName;
        var processId = Environment.ProcessId;
        var started = driver.ProcessStarted(processId);
        await File.WriteAllTextAsync(
                Path.Combine(_home, LaunchOwnership.OwnerFile),
                started is null
                    ? processId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : $"{processId}\n{started}"
            )
            .ConfigureAwait(false);
        var status = driver.Status();
        _driverVersion = driver.Version;
        if (!status.Accessibility)
        {
            throw new EngineException(
                "ENGINE_FAILURE",
                "the desktop engine has no Accessibility permission. In System Settings > Privacy & Security > Accessibility, allow the app that starts e2e (your terminal, IDE, or CI agent), then run again",
                retryable: false
            );
        }

        _launchTarget = await ResolveLaunchTargetAsync(info.Signal).ConfigureAwait(false);
        info.Log($"desktop driver {_driverVersion} on macOS");
    }

    /// <summary>
    /// What <c>app.open()</c> launches. An <c>appPath</c> on macOS is an <c>.app</c> bundle, which is what gets launched even when <c>bundleId</c> is set
    /// too, and its <c>CFBundleIdentifier</c> must then equal <c>bundleId</c>.
    /// </summary>
    private async Task<LaunchTarget?> ResolveLaunchTargetAsync(CancellationToken signal)
    {
        if (_app.AppPath is null)
        {
            return _app.BundleId is null ? null : new LaunchTarget(null, _app.BundleId, _app.BundleId);
        }

        var bundle = Path.GetFullPath(_app.AppPath, _projectRoot);
        var checkedBundle = await LaunchOwnership.CheckBundleAsync(bundle, _app.BundleId, signal).ConfigureAwait(false);
        if (checkedBundle.Problem is { } problem)
        {
            throw new EngineException("ENGINE_FAILURE", problem, retryable: false);
        }

        var real = new DirectoryInfo(bundle).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? bundle;
        return new LaunchTarget(real, null, checkedBundle.BundleId!);
    }

    /// <summary>Upstream's <c>startAttempt</c>.</summary>
    public void StartAttempt(DesktopAttemptContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_attempt is not null)
        {
            throw InvalidState("an attempt is already running on this desktop engine");
        }

        _attempt = new Attempt(context.ArtifactsDir, context.Screen);
        Forget();
        context.Environment?.Invoke(
            new Dictionary<string, string> { ["platform"] = Platform, ["desktop-driver"] = _driverVersion }
        );
    }

    /// <summary>
    /// Quits the app the attempt launched, so the next test starts from a closed app as the first one did. A quit that fails is reported, and the
    /// engine keeps the process as its own, so <see cref="DisposeAsync"/> tries again. Upstream's <c>endAttempt</c>.
    /// </summary>
    public async Task EndAttemptAsync(CancellationToken signal, int timeoutMs)
    {
        _attempt = null;
        Forget();
        await QuitAsync(signal, timeoutMs).ConfigureAwait(false);
    }

    /// <summary>Upstream's <c>dispose</c>.</summary>
    public async Task DisposeAsync(CancellationToken signal, int timeoutMs)
    {
        var quit = false;
        try
        {
            await QuitAsync(signal, timeoutMs).ConfigureAwait(false);
            quit = true;
        }
        finally
        {
            // An app that would not quit, or a launch whose process was never found, stays named in the launch record, which the next run's sweep quits.
            var leftRunning = !quit && (_pid is not null || _pending is not null);
            if (_home is { } home)
            {
                try
                {
                    if (leftRunning)
                    {
                        await LaunchOwnership.AbandonAsync(home).ConfigureAwait(false);
                    }
                    else
                    {
                        Directory.Delete(home, recursive: true);
                    }
                }
                catch (Exception cause) when (cause is IOException or UnauthorizedAccessException)
                {
                    // Best effort, as upstream: the sweep takes what is left.
                }
            }

            _home = null;
            _driver = null;
            _attempt = null;
            Forget();
        }
    }

    /// <summary>Upstream's <c>doctor</c>.</summary>
    public async Task<IReadOnlyList<EngineDoctorCheck>> DoctorAsync(EngineDoctorInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        var checks = new List<EngineDoctorCheck>();
        IDesktopDriver? driver = null;
        try
        {
            driver = _loadDriver();
        }
        catch (Exception cause)
        {
            checks.Add(new EngineDoctorCheck("desktop-driver", EngineDoctorStatus.Fail, cause.Message));
        }

        if (driver is not null)
        {
            var status = driver.Status();
            checks.Add(
                new EngineDoctorCheck("desktop-driver", EngineDoctorStatus.Pass, $"desktop driver {driver.Version}")
            );
            checks.Add(
                status.Accessibility
                    ? new EngineDoctorCheck(
                        "accessibility",
                        EngineDoctorStatus.Pass,
                        "Accessibility permission granted"
                    )
                    : new EngineDoctorCheck(
                        "accessibility",
                        EngineDoctorStatus.Fail,
                        "Allow the app that starts e2e in System Settings > Privacy & Security > Accessibility"
                    )
            );
            checks.Add(
                status.ScreenRecording
                    ? new EngineDoctorCheck(
                        "screen-recording",
                        EngineDoctorStatus.Pass,
                        "Screen Recording permission granted"
                    )
                    : new EngineDoctorCheck(
                        "screen-recording",
                        EngineDoctorStatus.Warn,
                        "Screenshots need Screen Recording: allow the app that starts e2e in System Settings > Privacy & Security > Screen Recording"
                    )
            );
        }

        if (info.App.AppPath is { } appPath)
        {
            var bundle = Path.GetFullPath(appPath, info.ProjectRoot);
            // What init checks of the bundle, so a bundle doctor passes is one init accepts.
            var checkedBundle =
                Directory.Exists(bundle) || File.Exists(bundle)
                    ? await LaunchOwnership
                        .CheckBundleAsync(bundle, info.App.BundleId, info.Signal)
                        .ConfigureAwait(false)
                    : BundleCheck.Failed($"app.appPath {bundle} does not exist; build the app or fix the path");
            checks.Add(
                checkedBundle.Problem is { } problem
                    ? new EngineDoctorCheck("app-path", EngineDoctorStatus.Fail, problem)
                    : new EngineDoctorCheck("app-path", EngineDoctorStatus.Pass, bundle)
            );
        }

        return checks;
    }

    // --- the app ---

    /// <summary>
    /// <c>app.open()</c>: the copy of the app the engine launched is quit, and the app launched fresh, in the background unless <c>headed</c> or the
    /// run's <c>--headed</c>, with the target's launch arguments. A copy the engine did not launch (the developer's own) is left alone, and never
    /// read or driven. A launch that failed after the app started still leaves the engine owning the new process, so the end of the attempt quits
    /// it. Upstream's <c>restart</c>.
    /// </summary>
    public async Task RestartAsync(DesktopOperation context)
    {
        // The budget counts from the call, so time spent waiting for another launch or quit is part of it.
        var operation = context.WithDeadline();
        await Serially(async () =>
            {
                await LaunchFreshAsync(operation).ConfigureAwait(false);
                return 0;
            })
            .ConfigureAwait(false);
    }

    /// <summary><see cref="RestartAsync"/> once no other launch or quit of this surface is running.</summary>
    private async Task LaunchFreshAsync(DesktopOperation operation)
    {
        var target = _launchTarget
            ?? throw Unsupported("launching the app needs the target's app.bundleId or app.appPath");
        var driver = RequireDriver();
        // A failed launch that left no process is nothing to clean up before a new one.
        await QuitNowAsync(operation.Signal, operation.TimeoutMs, reportMissing: false).ConfigureAwait(false);
        if (operation.TimeoutMs <= 0)
        {
            throw new EngineException(
                "OPERATION_TIMEOUT",
                $"launch {target.Label}: the operation's budget ran out before it was sent",
                retryable: false,
                NotSent
            );
        }

        // Marks the process this launch starts, so it is told apart from every other copy by evidence, never by timing.
        var token = Guid.NewGuid().ToString();
        // Written before anything is sent, so the app stays findable by its token if this worker is killed mid-launch.
        await WriteRecordAsync(new LaunchRecord(token)).ConfigureAwait(false);
        // The write takes time too: a launch sent with no budget left would start the app after the open gave up.
        if (operation.TimeoutMs <= 0)
        {
            throw new EngineException(
                "OPERATION_TIMEOUT",
                $"launch {target.Label}: the operation's budget ran out before it was sent",
                retryable: false,
                NotSent
            );
        }

        // Started by `CallAsync`, which starts nothing once the signal aborted: a cancelled open never launches the app.
        Task<Envelope<Launched>>? started = null;
        // A cut-off launch whose `open` has not run yet starts no app.
        var inputId = RequireDriver().InputId();
        Task<Envelope<Launched>> Start() =>
            started = driver.LaunchAsync(
                new LaunchRequest
                {
                    Path = target.Path,
                    BundleId = target.BundleId,
                    Arguments = [.. FreshStateArguments, .. DesktopLaunchArguments.LaunchArgumentList(_app)],
                    Environment = _appEnv,
                    Token = token,
                    // In the background unless `headed` or the run's `--headed`: otherwise the app takes the keyboard from whoever is at the Mac.
                    Activate = _foreground,
                    TimeoutMs = operation.TimeoutMs,
                    ClaimMs = LaunchReadyMs,
                    InputId = inputId,
                }
            );
        Launched launched;
        try
        {
            launched = await DesktopDriverCalls
                .CallAsync(
                    Start,
                    new DriverCall
                    {
                        Label = $"launch {target.Label}",
                        Input = true,
                        Signal = operation.Signal,
                        TimeoutMs = operation.TimeoutMs,
                        OnCutOff = () => driver.CancelInput(inputId),
                    }
                )
                .ConfigureAwait(false);
        }
        catch (Exception cause)
        {
            // A launch the driver refused started nothing; any other may have started the app, which the next quit looks for.
            if (started is not null && !(cause is DriverFailure { NotDelivered: true }))
            {
                _pending = new PendingLaunch(target.Label, token, SettleLaunchAsync(started));
            }

            throw LaunchFailure(cause, target);
        }

        _appName = launched.App == "" ? target.Label : launched.App;
        _pid = launched.Pid;
        _token = token;
        _started = launched.Started ?? driver.ProcessStarted(launched.Pid);
        // The process and when it started, so a later run can quit it, and never a later process given the same id.
        await WriteRecordAsync(new LaunchRecord(token, launched.Pid, _started)).ConfigureAwait(false);
        Forget();
        await WindowReadableAsync(operation).ConfigureAwait(false);
    }

    private static async Task<LaunchOutcome> SettleLaunchAsync(Task<Envelope<Launched>> started)
    {
        try
        {
            var answer = await started.ConfigureAwait(false);
            return answer.Ok
                ? new LaunchOutcome(answer.Data, false)
                : new LaunchOutcome(null, answer.Error?.Disposition?.Delivery == "not_delivered");
        }
        catch (Exception)
        {
            return new LaunchOutcome(null, false);
        }
    }

    /// <summary>The engine error for a failed launch, naming what to fix where the driver says what went wrong.</summary>
    private static Exception LaunchFailure(Exception cause, LaunchTarget target)
    {
        if (cause is DriverFailure { NotDelivered: true } refused)
        {
            if (refused.Code == "APP_NOT_FOUND")
            {
                return new EngineException(
                    "ENGINE_FAILURE",
                    $"{refused.Message}; check the target's app.bundleId or app.appPath",
                    retryable: false,
                    cause
                );
            }

            if (DetailIs(refused, "kind", "not_started"))
            {
                return new EngineException(
                    "ENGINE_FAILURE",
                    $"launch {target.Label}: {refused.Message}",
                    retryable: false,
                    cause
                );
            }
        }

        if (cause is DriverFailure unclaimed && DetailIs(unclaimed, "kind", "unclaimed"))
        {
            return new EngineException(
                "ACTION_MAY_HAVE_COMMITTED",
                $"{unclaimed.Message}, so none can be shown to be its own; the end of the attempt looks for it again",
                retryable: false,
                cause
            );
        }

        return DesktopDriverCalls.Translate(cause, $"launch {target.Label}", true);
    }

    /// <summary>Whether a failure's detail is the string <paramref name="expected"/>, whether the driver wrote it as text or as a JSON value.</summary>
    private static bool DetailIs(DriverFailure failure, string key, string expected) =>
        failure.Details.GetValueOrDefault(key) switch
        {
            string text => text == expected,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString() == expected,
            _ => false,
        };

    /// <summary>Puts the launch record in the state directory, renamed into place so a worker killed mid-write never leaves half a record.</summary>
    private async Task WriteRecordAsync(LaunchRecord record)
    {
        if (_home is null)
        {
            return;
        }

        var file = Path.Combine(_home, LaunchOwnership.AppFile);
        var fields = new Dictionary<string, object?> { ["token"] = record.Token };
        if (record.Pid is { } pid)
        {
            fields["pid"] = pid;
        }

        if (record.Started is { } started)
        {
            fields["started"] = started;
        }

        await File.WriteAllTextAsync($"{file}.partial", JsonSerializer.Serialize(fields)).ConfigureAwait(false);
        File.Move($"{file}.partial", file, overwrite: true);
    }

    /// <summary>
    /// Waits until the launched app's window can be read: a window exists soon after the launch, but its accessibility element can arrive a moment
    /// later, and an observation then would show the agent an empty screen. A window that stays unreadable for <c>LAUNCH_READY_MS</c> fails the open:
    /// every later step would only time out on an empty screen. Any other failure of a read is reported as it is. An app in the background is never
    /// brought forward: one that opens no window there (a document-based app opens its first one only when it comes forward) fails the open, and
    /// needs <c>headed</c>.
    /// </summary>
    private async Task WindowReadableAsync(DesktopOperation operation)
    {
        var budgetBound = operation.TimeoutMs < LaunchReadyMs;
        var deadline = LaunchOwnership.Now() + Math.Min(operation.TimeoutMs, LaunchReadyMs);
        var sawWindow = false;
        // Whether a read finished and listed no window of the app: only that shows it opened none. A read that ran out of time saw nothing.
        var listedNone = false;
        var timedOut = false;
        while (LaunchOwnership.Now() < deadline)
        {
            if (operation.Signal.IsCancellationRequested)
            {
                throw new EngineException(
                    "CANCELLED",
                    $"opening {_appName ?? "the app"} was cancelled",
                    retryable: false
                );
            }

            ProjectedScreen? screen = null;
            var read = false;
            try
            {
                // Each read gets what is left of the readiness wait, not of the whole operation, shared by its calls.
                screen = await CaptureAsync(
                        new DesktopOperation(
                            operation.Origin,
                            (int)Math.Max(deadline - LaunchOwnership.Now(), 0),
                            operation.Signal
                        )
                    )
                    .ConfigureAwait(false);
                read = true;
            }
            catch (EngineException cause)
            {
                // The last read ran out of budget: the window never became readable in time.
                if (cause.Code == "OPERATION_TIMEOUT")
                {
                    timedOut = true;
                    break;
                }

                // A window not readable yet, or one replaced while it was read, is what this wait looks at below; so is an app that quit.
                var stale = cause.Code == "NODE_STALE" && cause.Retryable;
                if (!(stale || ReferenceEquals(cause.InnerException, Quit)))
                {
                    throw;
                }

                if (stale)
                {
                    sawWindow = true;
                }
            }

            // A window whose root the app did not answer for is there but not readable yet, which is what this wait is for.
            if (screen is not null && !screen.RootUnread)
            {
                return;
            }

            if (screen is not null)
            {
                sawWindow = true;
            }
            else if (read)
            {
                listedNone = true;
            }

            // An app that quit (or crashed) on its own never shows a window.
            if (_pid is { } pid && !await RunningAsync(pid).ConfigureAwait(false))
            {
                Forget();
                DropProcess();
                throw new EngineException(
                    "ENGINE_FAILURE",
                    $"{_appName ?? "the app"} quit right after it launched, before it showed a window",
                    retryable: false
                );
            }

            await LaunchOwnership
                .SleepAsync(
                    (int)Math.Min(LaunchOwnership.LaunchPollMs, Math.Max(deadline - LaunchOwnership.Now(), 0)),
                    operation.Signal
                )
                .ConfigureAwait(false);
        }

        // The open's own budget ran out first: that is a timeout, not a window that cannot be read.
        if (budgetBound)
        {
            throw new EngineException(
                "OPERATION_TIMEOUT",
                $"{_appName ?? "the app"} launched, but the open ran out of time before its window could be read",
                retryable: false
            );
        }

        // A locked screen hides every window from accessibility: neither read tells it from an app with no window, so macOS is asked.
        if (RequireDriver().ScreenLocked() == true)
        {
            throw new EngineException(
                "ENGINE_FAILURE",
                $"{_appName ?? "the app"} launched, but the Mac's screen is locked, so no window can be read through accessibility; unlock it and run again",
                retryable: false
            );
        }

        if (!sawWindow && listedNone && !_foreground)
        {
            throw new EngineException(
                "ENGINE_FAILURE",
                $"{_appName ?? "the app"} launched in the background and opened no window. Some apps, such as document-based ones, show a window only once they come forward; set desktop({{ headed: true }}) for them, or run with --headed",
                retryable: false
            );
        }

        // In the foreground there is no background to blame: the app listed no window at all.
        if (!sawWindow && listedNone)
        {
            throw new EngineException(
                "ENGINE_FAILURE",
                $"{_appName ?? "the app"} launched, but listed no window within {LaunchReadyMs / 1000} s: it may open none (a menu-bar app) or take longer to show its first window",
                retryable: false
            );
        }

        throw new EngineException(
            "ENGINE_FAILURE",
            $"{_appName ?? "the app"} launched, but its window cannot be read through accessibility{(timedOut ? " (a read ran out of time)" : "")}. The window may be off screen",
            retryable: false
        );
    }

    /// <summary>The windows of the app the engine launched, for the <c>desktop</c> fixture; none while it is not running. Upstream's <c>windows</c>.</summary>
    public async Task<IReadOnlyList<DesktopWindow>> WindowsAsync(DesktopOperation operation)
    {
        var windows = await OwnWindowsAsync(operation).ConfigureAwait(false);
        return
        [
            .. windows.Select(window => new DesktopWindow(window.Id, window.Title ?? "", window.IsFocused == true)),
        ];
    }

    /// <summary>
    /// <c>desktop.useWindow()</c>: reads and acts in the window <paramref name="id"/> names from now on, until the app quits, rather than the focused
    /// one. With no id, goes back to the focused window. Upstream's <c>useWindow</c>.
    /// </summary>
    public async Task UseWindowAsync(string? id, DesktopOperation operation)
    {
        if (id is not null)
        {
            var windows = await OwnWindowsAsync(operation).ConfigureAwait(false);
            if (!windows.Any(window => window.Id == id))
            {
                throw NotActionable($"the app has no window {id}; desktop.windows() lists the ones it has");
            }
        }

        _chosenWindow = id;
        // A point taken in the window read before is not in this one's coordinates: the next point action measures from the window now in use.
        _pointWindow = null;
    }

    private EngineException ChosenClosed() =>
        InvalidState(
            $"the window desktop.useWindow() chose ({_chosenWindow ?? ""}) is closed; choose another, or call desktop.useWindow() to go back to the focused one"
        );

    /// <summary><c>desktop.closeApp()</c>: the app quit, as at the end of every test. Upstream's <c>closeApp</c>.</summary>
    public Task CloseAppAsync(DesktopOperation operation) => QuitAsync(operation.Signal, operation.TimeoutMs);

    /// <summary>
    /// Runs launches and quits of this surface one at a time: two at once would each launch a copy into the one process id and launch record the
    /// engine keeps, and the end of the attempt would quit only one of them.
    /// </summary>
    private Task<T> Serially<T>(Func<Task<T>> run)
    {
        Task<T> next;
        lock (_lifecycleLock)
        {
            next = RunAfterAsync(_lifecycle, run);
            _lifecycle = next.ContinueWith(static _ => { }, TaskScheduler.Default);
        }

        return next;

        static async Task<T> RunAfterAsync(Task previous, Func<Task<T>> run)
        {
            await previous.ConfigureAwait(false);
            return await run().ConfigureAwait(false);
        }
    }

    /// <summary><see cref="QuitNowAsync"/> once no other launch or quit of this surface is running.</summary>
    private Task QuitAsync(CancellationToken signal, int timeoutMs) =>
        Serially(async () =>
        {
            await QuitNowAsync(signal, timeoutMs).ConfigureAwait(false);
            return 0;
        });

    /// <summary>
    /// Quits the app the engine launched, forcefully: a test needs it gone, not asked to save. It is ended by its process id, so another instance
    /// of the same app (the developer's own copy) is left alone, and waited for, so the next launch cannot find it still running. A process that
    /// exited on its own, or whose id the OS gave to another process since, is not running any more, and nothing is killed. Until the process is
    /// known to be gone, the engine keeps it as its own, so a quit that failed is tried again. A failed launch that turns out to have left no
    /// process is reported, so the user can look for a copy by hand, unless <paramref name="reportMissing"/> is false, as before a new launch.
    /// </summary>
    private async Task QuitNowAsync(CancellationToken signal, int timeoutMs, bool reportMissing = true)
    {
        var deadline = LaunchOwnership.Now() + timeoutMs;
        var pending = _pending;
        if (_pid is null && pending is not null)
        {
            // The driver may still report the process the launch started: wait for it, short of the quit's budget by `ExitWaitMs`, so a process found at the end can still be seen to exit.
            var wait = Math.Min(Math.Max(timeoutMs - ExitWaitMs, 0), PendingSettleMs);
            var (timedOut, settled) = await SettleWithinAsync(pending.Settled, wait, signal).ConfigureAwait(false);
            if (signal.IsCancellationRequested)
            {
                throw new EngineException(
                    "CANCELLED",
                    $"looking for the process a launch of {pending.Target} started was cancelled",
                    retryable: false
                );
            }

            // The driver gave the launch up before `open` ran: it started no app, and there is nothing to look for.
            if (settled is { NotStarted: true })
            {
                _pending = null;
                if (_home is not null)
                {
                    DeleteQuietly(Path.Combine(_home, LaunchOwnership.AppFile));
                }

                return;
            }

            // A process carrying the launch's token is its own, whether or not the driver reported it.
            var carriers = await RequireDriver().ProcessesCarryingAsync(pending.Token).ConfigureAwait(false)
                ?? throw new EngineException(
                    "ENGINE_FAILURE",
                    $"could not tell whether a launch of {pending.Target} started the app: looking for its token among the processes failed; quitting tries again",
                    retryable: false
                );
            var owned = new HashSet<int>(carriers);
            // The process the launch answered with, while it is still that process: its id may be another's by now.
            if (
                !timedOut
                && settled?.Launched is { } answered
                && (
                    carriers.Contains(answered.Pid)
                    || (
                        answered.Started is { } answeredStart
                        && RequireDriver().ProcessStarted(answered.Pid) == answeredStart
                    )
                )
            )
            {
                owned.Add(answered.Pid);
            }

            // A launch that answered without a process may have started the app a moment later: look again, within the budget.
            if (owned.Count == 0 && !timedOut)
            {
                var whole = LaunchOwnership.Now() + LateAppMs;
                foreach (
                    var pid in await LateCarriersAsync(
                            pending.Token,
                            Math.Min(deadline - ExitWaitMs, whole),
                            whole,
                            signal,
                            pending.Target
                        )
                        .ConfigureAwait(false)
                )
                {
                    owned.Add(pid);
                }
            }

            if (!await LaunchOwnership.KilledAsync([.. owned], deadline, signal).ConfigureAwait(false))
            {
                throw new EngineException(
                    "ENGINE_FAILURE",
                    $"the process a launch of {pending.Target} started did not exit; quitting tries again",
                    retryable: false
                );
            }

            // A launch still running may yet start the app: it stays pending.
            if (owned.Count == 0 && timedOut)
            {
                throw new EngineException(
                    "ENGINE_FAILURE",
                    $"could not tell whether a launch of {pending.Target} started the app: it had not finished; quitting tries again",
                    retryable: false
                );
            }

            _pending = null;
            foreach (var pid in owned)
            {
                RequireDriver().ForgetProcess(pid);
            }

            if (_home is not null)
            {
                DeleteQuietly(Path.Combine(_home, LaunchOwnership.AppFile));
            }

            if (owned.Count > 0 || !reportMissing)
            {
                return;
            }

            throw new EngineException(
                "ENGINE_FAILURE",
                $"a launch of {pending.Target} failed, and no process it started was found; if the app is running, quit it by hand",
                retryable: false
            );
        }

        var current = _pid;
        Forget();
        if (current is not { } running)
        {
            return;
        }

        if (
            await RunningAsync(running).ConfigureAwait(false)
            && !await LaunchOwnership.KilledAsync([running], deadline, signal).ConfigureAwait(false)
        )
        {
            throw new EngineException("ENGINE_FAILURE", $"the app (process {running}) did not exit", retryable: false);
        }

        _driver?.ForgetProcess(running);
        DropProcess();
        if (_home is not null)
        {
            DeleteQuietly(Path.Combine(_home, LaunchOwnership.AppFile));
        }
    }

    private static void DeleteQuietly(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception cause) when (cause is IOException or UnauthorizedAccessException)
        {
            // Best effort, as upstream's `rm(..., { force: true }).catch(() => undefined)`.
        }
    }

    /// <summary>
    /// The processes carrying a launch's token, looked for until one appears or <paramref name="until"/> passes; none when none did through the whole
    /// wait, which ends at <paramref name="whole"/>. A cancel throws <c>CANCELLED</c>, and a scan that failed or a wait cut short of
    /// <paramref name="whole"/> <c>ENGINE_FAILURE</c>, so the launch stays pending.
    /// </summary>
    private async Task<IReadOnlyList<int>> LateCarriersAsync(
        string token,
        long until,
        long whole,
        CancellationToken signal,
        string target
    )
    {
        // None found is a finding only after a whole wait of scans that answered: a cancel, a scan that failed, or a wait the budget cut short
        // proves nothing, and the launch's record must stay for the next quit or sweep.
        var failed = false;
        while (LaunchOwnership.Now() < until && !signal.IsCancellationRequested)
        {
            await LaunchOwnership.SleepAsync(LateAppPollMs, CancellationToken.None).ConfigureAwait(false);
            var carriers = await RequireDriver().ProcessesCarryingAsync(token).ConfigureAwait(false);
            if (carriers is null)
            {
                failed = true;
            }
            else if (carriers.Count > 0)
            {
                return carriers;
            }
        }

        if (signal.IsCancellationRequested)
        {
            throw new EngineException(
                "CANCELLED",
                $"looking for the process a launch of {target} started was cancelled",
                retryable: false
            );
        }

        if (failed)
        {
            throw new EngineException(
                "ENGINE_FAILURE",
                $"could not tell whether a launch of {target} started the app: looking for its token among the processes failed; quitting tries again",
                retryable: false
            );
        }

        if (until < whole)
        {
            throw new EngineException(
                "ENGINE_FAILURE",
                $"could not tell whether a launch of {target} started the app: the quit ran out of time to look for it; quitting tries again",
                retryable: false
            );
        }

        return [];
    }

    /// <summary>
    /// Whether the process the engine launched still runs: the id alone does not show that, since another process can take it over once the app
    /// quits, so it must have started when the launch's did, or else carry the launch's token. A process that cannot be told either way throws, and
    /// stays the engine's, so a quit tries again and the launch record stays.
    /// </summary>
    private async Task<bool> RunningAsync(int pid)
    {
        if (!LaunchOwnership.Alive(pid))
        {
            return false;
        }

        var driver = RequireDriver();
        if (_started is { } started)
        {
            var now = driver.ProcessStarted(pid);
            if (now is { } at)
            {
                return at == started;
            }

            // Gone between the two looks; still there, its start unreadable, it is the app only if it carries the token.
            if (!LaunchOwnership.Alive(pid))
            {
                return false;
            }
        }

        var carriers = _token is null ? null : await driver.ProcessesCarryingAsync(_token).ConfigureAwait(false);
        if (carriers is null)
        {
            throw new EngineException(
                "ENGINE_FAILURE",
                $"could not tell whether process {pid} is still the app the engine launched; quitting tries again",
                retryable: false
            );
        }

        return carriers.Contains(pid);
    }

    /// <summary>Forgets the launched process, once it is gone.</summary>
    private void DropProcess()
    {
        _pid = null;
        _started = null;
        _token = null;
    }

    /// <summary>Every binding of the screen before: after a launch or a quit, none names a control that still exists.</summary>
    private void Forget()
    {
        _pointWindow = null;
        _chosenWindow = null;
        _bindings.Clear();
        _bindingOrder.Clear();
        _ids.Clear();
    }

    /// <summary>The node id for an identity: the one it had before, else a new one, never handed out before since the app launched.</summary>
    private string IdFor(string? identity)
    {
        string? id = null;
        if (identity is not null)
        {
            _ids.TryGetValue(identity, out id);
        }

        if (id is null)
        {
            id = "n" + (++_sequence).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (identity is not null)
            {
                _ids[identity] = id;
            }
        }

        return id;
    }

    /// <summary>The driver's answer within <paramref name="ms"/>, or a timeout; a cancelled wait answers at once. Upstream's <c>settleWithin</c>.</summary>
    private static async Task<(bool TimedOut, T? Value)> SettleWithinAsync<T>(
        Task<T> promise,
        int ms,
        CancellationToken signal
    )
    {
        using var timer = new CancellationTokenSource();
        var limit = Task.Delay(Math.Max(ms, 0), timer.Token);
        var aborted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = signal.Register(() => aborted.TrySetResult());
        try
        {
            var winner = await Task.WhenAny(promise, limit, aborted.Task).ConfigureAwait(false);
            return winner == promise ? (false, await promise.ConfigureAwait(false)) : (true, default);
        }
        finally
        {
            await timer.CancelAsync().ConfigureAwait(false);
        }
    }
}
