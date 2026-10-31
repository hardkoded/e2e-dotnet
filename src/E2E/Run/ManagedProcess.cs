// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using E2E.Internal;

namespace E2E;

/// <summary>How a spawned process counts as ready: a URL that answers, or the process itself exiting with code 0 (upstream's <c>Readiness</c>).</summary>
/// <param name="ReadyUrl">The URL to poll; null means the process exiting with code 0 is the readiness event.</param>
internal sealed record Readiness(string? ReadyUrl = null)
{
    /// <summary>The process exiting with code 0 is the readiness event.</summary>
    public static Readiness WaitForExit { get; } = new();
}

/// <summary>
/// What the runner tells a managed process about the run: whether CI mode is active (where <c>reuseExisting</c> is ignored), the environment the
/// run resolves against (what <c>inheritEnv</c> copies from; default the process environment), where to narrate run-level progress such as a
/// reused app, and where to report the process starting and becoming ready.
/// </summary>
internal sealed class ManagedProcessHooks
{
    public bool Ci { get; init; }

    public IReadOnlyDictionary<string, string?>? Env { get; init; }

    public Action<string>? Notice { get; init; }

    /// <summary>The label, and the URL the readiness probe polls; null for a command that is ready when it exits.</summary>
    public Action<string, string?>? Starting { get; init; }

    /// <summary>The label, the readiness URL (null for a command that is ready when it exits), how long the start took, and whether the process attached to one already serving the URL instead of spawning.</summary>
    public Action<string, string?, TimeSpan, bool>? Ready { get; init; }
}

/// <summary>What one started process, or everything a run or a session started, is released by (upstream's <c>AppProcesses</c>).</summary>
internal interface IAppProcesses
{
    /// <summary>Stops what was started, in reverse. Every failure is reported through <paramref name="onFailure"/> and never skips the rest.</summary>
    Task StopAsync(Action<Exception> onFailure);

    /// <summary>False once what was started has ended, so a holder can start it again (a .NET-only member: the port's holder outlives its callers).</summary>
    bool Running { get; }
}

/// <summary>
/// One command the runner owns: spawned, waited on until its readiness contract holds, and terminated signal-then-force (upstream's
/// <c>ManagedProcess</c>). <c>label</c> names it in every error (<c>target "web" command</c>, <c>service "postgres"</c>).
/// </summary>
internal sealed partial class ManagedProcess : IAsyncDisposable
{
    private static readonly string[] InheritedEnv = ["PATH", "HOME", "TMPDIR", "TMP", "TEMP", "SystemRoot", "COMSPEC"];

    // Readiness polling starts fast and backs off; a booting server answers late, not on a schedule.
    private static readonly TimeSpan ReadyPollMin = TimeSpan.FromMilliseconds(25);
    private static readonly TimeSpan ReadyPollMax = TimeSpan.FromMilliseconds(250);

    // One readiness probe never outlives this, so a half-open server cannot stall the deadline check.
    private static readonly TimeSpan ReadyProbeTimeout = TimeSpan.FromSeconds(2);

    // A wait past half its budget narrates once, so a stalled service is visible while it stalls and not only 180 s later.
    // Under this floor a short budget stays quiet: half of it is not long enough to call a stall.
    private static readonly TimeSpan ProgressNoticeMin = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan DefaultStartupTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(10);

    // How long a leader's exit waits for the last of its output to land in the log.
    private static readonly TimeSpan OutputDrain = TimeSpan.FromSeconds(2);

    // How much of the process's own output a startup failure quotes.
    private const int OutputTailLines = 20;
    private const int OutputTailBytes = 4_096;

    // command.env and inheritEnv values this long are redacted from the quoted output; shorter ones (a port, a flag) would blank ordinary
    // text without hiding anything. The shared redactor has no floor of its own.
    private const int RedactedEnvMinLength = 4;

    private static readonly HttpClient Probe = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false })
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };

    // Every spawned process still running, for the exit that cannot wait on StopAsync.
    private static readonly ConcurrentDictionary<Process, byte> Live = new();

    private readonly string _label;
    private readonly CommandConfig _command;
    private readonly string _projectRoot;
    private readonly Readiness _readiness;
    private readonly ManagedProcessHooks _hooks;
    private Process? _process;
    private OutputPump? _pump;
    private ProcessJob? _job;
    private bool _reusedExisting;
    private bool _spawned;

    // Hides command.env and inheritEnv values in a quoted startup failure; built at spawn.
    private Redactor _redact = Redactor.None;

    static ManagedProcess()
    {
        // The last resort before the process exits on the spot, so nothing it started outlives it.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => KillManagedProcessGroups();
    }

    public ManagedProcess(string label, CommandConfig command, string projectRoot, Readiness readiness, ManagedProcessHooks? hooks = null)
    {
        _label = label;
        _command = command;
        _projectRoot = projectRoot;
        _readiness = readiness;
        _hooks = hooks ?? new ManagedProcessHooks();
    }

    /// <summary>True once <see cref="StartAsync"/> spawned a process: not when it attached to one already serving the URL, found one there it may not reuse, or failed to spawn.</summary>
    public bool Spawned => _spawned;

    /// <summary>Whether the app is still there: a process this one spawned that has not exited, or one it attached to.</summary>
    public bool Running => _reusedExisting || (_process is { } process && !HasExited(process));

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    /// <summary>
    /// Kills every process tree this module spawned and has not yet released, synchronously and without waiting: the last resort before the
    /// process exits on the spot, so nothing it started outlives it. A reused process was never spawned here and is left alone.
    /// </summary>
    public static void KillManagedProcessGroups()
    {
        foreach (var process in Live.Keys)
        {
            KillTree(process);
        }
    }

    /// <summary>
    /// Spawns the process and waits for readiness. A token that is already cancelled spawns nothing; one that is cancelled during the wait
    /// ends it and takes the process down with it, and the call throws <see cref="OperationCanceledException"/>.
    /// <para>
    /// A readiness URL that already answers before the spawn belongs to a process this run did not start, and HTTP readiness cannot tell which
    /// process answered later. So the preflight probe decides up front: with <c>reuseExisting</c> (outside CI) nothing is spawned and
    /// <see cref="StopAsync"/> leaves the process alone; otherwise the launch fails with <c>APP_ALREADY_RUNNING</c> rather than letting the old
    /// server pass as the new command's readiness. The probe spends from the same <c>startupTimeout</c> budget as the wait.
    /// </para>
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var startedAt = Stopwatch.StartNew();
        var url = _readiness.ReadyUrl;
        _hooks.Starting?.Invoke(_label, url);
        await LaunchAsync(cancellationToken).ConfigureAwait(false);
        _hooks.Ready?.Invoke(_label, url, startedAt.Elapsed, _reusedExisting);
    }

    /// <summary>Gracefully terminates the process, then force-kills its tree. A reused process was never ours to stop. Safe to call before start and more than once.</summary>
    public async Task StopAsync()
    {
        if (_reusedExisting)
        {
            return;
        }

        var process = Interlocked.Exchange(ref _process, null);
        var pump = Interlocked.Exchange(ref _pump, null);
        if (process is null)
        {
            return;
        }

        try
        {
            var shutdownTimeout = _command.ShutdownTimeout ?? DefaultShutdownTimeout;

            // The leader may hand its job to a child and exit at once (`npm run dev`), so the tree is read before anything is signalled.
            var descendants = OperatingSystem.IsWindows() ? [] : await DescendantsAsync(process.Id).ConfigureAwait(false);
            if (!process.HasExited)
            {
                // Windows has no signal to ask a process to stop: the first step there is already the kill.
                if (!OperatingSystem.IsWindows())
                {
                    Terminate(process);
                }
                else
                {
                    KillTree(process);
                }

                if (!await ExitedWithin(process, shutdownTimeout).ConfigureAwait(false))
                {
                    KillTree(process);
                    await ExitedWithin(process, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }
            }

            await EndDescendantsAsync(descendants, shutdownTimeout).ConfigureAwait(false);

            // On Windows what the leader started is in its job, which ends with it whether or not the leader is still there.
            _job?.Terminate();
        }
        finally
        {
            Live.TryRemove(process, out _);
            Interlocked.Exchange(ref _job, null)?.Dispose();
            if (pump is not null)
            {
                await pump.DrainAsync(OutputDrain).ConfigureAwait(false);
                pump.Dispose();
            }

            process.Dispose();
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    /// <summary>
    /// The child's environment: the default names from the process environment, what <c>inheritEnv</c> matches in <paramref name="source"/>
    /// (only variables that are set), then <c>command.env</c>, each layer replacing the one before. Windows reads names case-insensitively
    /// (<c>Path</c>), so there names match and replace that way too. <c>inherited</c> is what <c>inheritEnv</c> copied.
    /// </summary>
    internal static (Dictionary<string, string> Env, List<(string Name, string Value)> Inherited) ChildEnv(
        CommandConfig command,
        IReadOnlyDictionary<string, string?> source,
        bool caseInsensitive)
    {
        var comparer = caseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var patterns = command.InheritEnv ?? [];
        var names = new HashSet<string>(patterns.Where(pattern => !pattern.EndsWith('*')), comparer);
        var prefixes = patterns.Where(pattern => pattern.EndsWith('*')).Select(pattern => pattern[..^1]).ToList();
        var inherited = source
            .Where(entry => entry.Value is not null
                && (names.Contains(entry.Key) || prefixes.Any(prefix => entry.Key.StartsWith(prefix, caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))))
            .Select(entry => (entry.Key, entry.Value!))
            .ToList();
        var byName = new Dictionary<string, (string Name, string Value)>(comparer);
        foreach (var key in InheritedEnv)
        {
            if (Environment.GetEnvironmentVariable(key) is { } value)
            {
                byName[key] = (key, value);
            }
        }

        foreach (var entry in inherited)
        {
            byName[entry.Key] = entry;
        }

        foreach (var entry in command.Env ?? new Dictionary<string, string>())
        {
            byName[entry.Key] = (entry.Key, entry.Value);
        }

        return (byName.Values.ToDictionary(entry => entry.Name, entry => entry.Value, StringComparer.Ordinal), inherited);
    }

    private static IReadOnlyDictionary<string, string?> ProcessEnvironment()
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            result[(string)entry.Key] = (string?)entry.Value;
        }

        return result;
    }

    /// <summary>One readiness probe: true when <paramref name="url"/> answers 200 through 499 within <paramref name="timeout"/>.</summary>
    private static async Task<bool> AnswersAsync(string url, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bound.CancelAfter(timeout);
        try
        {
            using var response = await Probe.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, bound.Token).ConfigureAwait(false);
            return (int)response.StatusCode is >= 200 and <= 499;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidOperationException or IOException or NotSupportedException or UriFormatException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
    }

    private static async Task<bool> ExitedWithin(Process process, TimeSpan timeout)
    {
        using var bound = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(bound.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    /// <summary>The processes below <paramref name="pid"/> (Unix only; <c>pgrep -P</c>, bounded), so a descendant that outlives its leader can still be ended.</summary>
    private static async Task<List<int>> DescendantsAsync(int pid)
    {
        var found = new List<int>();
        var pending = new Queue<int>([pid]);
        while (pending.Count > 0 && found.Count < 256)
        {
            var parent = pending.Dequeue();
            Process? pgrepProcess = null;
            try
            {
                var info = new ProcessStartInfo("pgrep") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                info.ArgumentList.Add("-P");
                info.ArgumentList.Add(parent.ToString(CultureInfo.InvariantCulture));
                var pgrep = Process.Start(info);
                pgrepProcess = pgrep;
                if (pgrep is null)
                {
                    break;
                }

                using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var error = pgrep.StandardError.ReadToEndAsync(bound.Token);
                var text = await pgrep.StandardOutput.ReadToEndAsync(bound.Token).ConfigureAwait(false);
                await pgrep.WaitForExitAsync(bound.Token).ConfigureAwait(false);
                await error.ConfigureAwait(false);
                foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (int.TryParse(line, NumberStyles.None, CultureInfo.InvariantCulture, out var child))
                    {
                        found.Add(child);
                        pending.Enqueue(child);
                    }
                }
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or OperationCanceledException or IOException)
            {
                break;
            }
            finally
            {
                if (pgrepProcess is not null)
                {
                    if (!HasExited(pgrepProcess))
                    {
                        KillTree(pgrepProcess);
                    }

                    pgrepProcess.Dispose();
                }
            }
        }

        return found;
    }

    /// <summary>Asks each descendant still running to stop, and kills what is left once the shutdown budget is spent.</summary>
    private static async Task EndDescendantsAsync(List<int> pids, TimeSpan shutdownTimeout)
    {
        var running = pids.Where(IsRunning).ToList();
        if (running.Count == 0)
        {
            return;
        }

        foreach (var pid in running)
        {
            Signal(pid, 15);
        }

        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < shutdownTimeout && running.Any(IsRunning))
        {
            await Task.Delay(25).ConfigureAwait(false);
        }

        foreach (var pid in running.Where(IsRunning))
        {
            Signal(pid, 9);
        }
    }

    private static bool IsRunning(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static void Signal(int pid, int signal)
    {
        try
        {
            _ = NativeKill(pid, signal);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // no libc to signal through
        }
    }

    /// <summary>Kills the process and everything it started; one already gone is fine.</summary>
    private static void KillTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // already gone
        }
    }

    /// <summary>Asks the process to stop with SIGTERM, so it can shut down cleanly; one already gone is fine.</summary>
    private static void Terminate(Process process)
    {
        try
        {
            _ = NativeKill(process.Id, 15);
        }
        catch (Exception ex) when (ex is InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
        {
            KillTree(process);
        }
    }

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int NativeKill(int pid, int signal);

    private static string Seconds(TimeSpan value) => Math.Round(value.TotalSeconds).ToString(CultureInfo.InvariantCulture);

    private static string Ms(TimeSpan value) => ((long)value.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);

    /// <summary>The preflight probe, the spawn, and the readiness wait behind <see cref="StartAsync"/>.</summary>
    private async Task LaunchAsync(CancellationToken cancellationToken)
    {
        // Without a URL to probe, the process exiting 0 is the readiness event.
        var readyUrl = _readiness.ReadyUrl;
        var startupTimeout = _command.StartupTimeout ?? DefaultStartupTimeout;
        var clock = Stopwatch.StartNew();
        if (readyUrl is not null)
        {
            var reuse = _command.ReuseExisting == true && !_hooks.Ci;
            if (_command.ReuseExisting == true && !reuse)
            {
                _hooks.Notice?.Invoke(_label + ": reuseExisting is ignored in CI, starting the command");
            }

            if (await AnswersAsync(readyUrl, Min(ReadyProbeTimeout, startupTimeout), cancellationToken).ConfigureAwait(false))
            {
                if (reuse)
                {
                    _reusedExisting = true;
                    _hooks.Notice?.Invoke(_label + ": reusing the process already serving " + Shown(readyUrl));
                    return;
                }

                throw new EngineException(
                    "APP_ALREADY_RUNNING",
                    Shown(readyUrl) + " already answered before " + _label + " started; "
                    + (_hooks.Ci ? "stop that process (reuseExisting is ignored in CI)" : "stop that process or set reuseExisting: true"));
            }
        }

        var (env, inherited) = ChildEnv(_command, _hooks.Env ?? ProcessEnvironment(), OperatingSystem.IsWindows());
        _redact = Redactor.For(inherited.Concat((_command.Env ?? new Dictionary<string, string>()).Select(entry => (entry.Key, entry.Value)))
            .Where(entry => entry.Value.Length >= RedactedEnvMinLength));

        var log = OpenLog();
        long? logOffset = log?.Length;
        Process process;
        try
        {
            process = Spawn(env);
        }
        catch
        {
            log?.Dispose();
            throw;
        }

        _process = process;
        _spawned = true;
        Live[process] = 0;
        _job = ProcessJob.Create(process);
        try
        {
            _pump = new OutputPump(process, log);
            await WaitForReadinessAsync(process, readyUrl, startupTimeout, clock, logOffset, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // A failed or cancelled start takes the process down with it, whatever ended the wait.
            var owned = _pump is not null;
            await StopAsync().ConfigureAwait(false);
            if (!owned)
            {
                log?.Dispose();
            }

            throw;
        }
    }

    private async Task WaitForReadinessAsync(Process process, string? readyUrl, TimeSpan startupTimeout, Stopwatch clock, long? logOffset, CancellationToken cancellationToken)
    {
        var half = startupTimeout / 2;
        var noticed = half < ProgressNoticeMin;
        var pollMs = ReadyPollMin;
        var exited = process.WaitForExitAsync(CancellationToken.None);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (exited.IsCompleted)
            {
                var code = process.ExitCode;
                if (readyUrl is null && code == 0)
                {
                    // What the process printed before it exited is in the log by the time the start returns.
                    // The process is gone, and its log is closed with it.
                    await StopAsync().ConfigureAwait(false);
                    return;
                }

                await StopAsync().ConfigureAwait(false);
                throw Unreachable(_label + " exited with code " + code.ToString(CultureInfo.InvariantCulture) + (readyUrl is null ? " instead of 0" : " before becoming ready"), logOffset);
            }

            var remaining = startupTimeout - clock.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                await StopAsync().ConfigureAwait(false);
                throw Unreachable(
                    readyUrl is null
                        ? _label + " did not exit within " + Ms(startupTimeout) + " ms"
                        : _label + " was not reachable at " + Shown(readyUrl) + " within " + Ms(startupTimeout) + " ms",
                    logOffset);
            }

            if (!noticed && remaining <= half)
            {
                noticed = true;
                var waitingFor = readyUrl is null ? "waiting for it to exit" : "waiting for " + Shown(readyUrl);
                var logNote = _command.Log is null ? "" : "; log: " + LogDisplayPath();
                _hooks.Notice?.Invoke(_label + " still starting after " + Seconds(startupTimeout - remaining) + "s: " + waitingFor + logNote);
            }

            // Until the notice is out, neither a probe nor a sleep runs past the half mark, so it lands on time instead of after a 2 s probe.
            var untilHalf = noticed ? TimeSpan.MaxValue : Max(ReadyPollMin, remaining - half);
            if (readyUrl is not null)
            {
                // A probe that is still waiting when the process exits ends there: connecting to a closed port can take seconds.
                using var probeEnds = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var probe = AnswersAsync(readyUrl, Min(Min(ReadyProbeTimeout, remaining), untilHalf), probeEnds.Token);
                var first = await Task.WhenAny(probe, exited).ConfigureAwait(false);
                if (first == exited)
                {
                    await probeEnds.CancelAsync().ConfigureAwait(false);
                    await probe.ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
                    continue;
                }

                if (await probe.ConfigureAwait(false))
                {
                    return;
                }
            }

            // The exit wakes the wait early so an exit is seen at once, not on the next poll.
            var pause = Min(Min(pollMs, untilHalf), Max(TimeSpan.Zero, startupTimeout - clock.Elapsed));
            await Task.WhenAny(Task.Delay(pause, cancellationToken), exited).ConfigureAwait(false);
            pollMs = Min(pollMs * 2, ReadyPollMax);
        }
    }

    /// <summary>A URL as a message shows it: without userinfo, query, or fragment, which may hold a token.</summary>
    private static string Shown(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            ? parsed.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped)
            : "the readiness URL";

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private Process Spawn(Dictionary<string, string> env)
    {
        string workingDirectory;
        try
        {
            workingDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(_command.Cwd) ? "." : _command.Cwd, _projectRoot);
        }
        catch (ArgumentException ex)
        {
            throw new EngineException("APP_UNREACHABLE", _label + " failed to start: its cwd is not a path: " + ex.Message, ex);
        }

        var info = new ProcessStartInfo(_command.Executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in _command.Args ?? [])
        {
            info.ArgumentList.Add(arg);
        }

        info.Environment.Clear();
        foreach (var (name, value) in env)
        {
            info.Environment[name] = value;
        }

        Process? process = null;
        try
        {
            process = Process.Start(info) ?? throw new EngineException("APP_UNREACHABLE", _label + " failed to start: no process was created");
            // Nothing is written to the process: closing its input tells a program that reads it that there is none.
            process.StandardInput.Close();
            return process;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            if (process is not null)
            {
                // It started but could not be set up: it is not tracked yet, so it ends here.
                KillTree(process);
                process.Dispose();
            }

            throw new EngineException("APP_UNREACHABLE", _label + " failed to start: " + ex.Message, ex);
        }
    }

    /// <summary>
    /// The startup failure with the log's tail under it, so the report alone says what the process was doing when it stalled or died. The
    /// message lands in the report, so <c>command.env</c> and <c>inheritEnv</c> values are redacted from the quote with the same redactor and
    /// markers as the traces, the way the log itself never is. Without a log there is nothing to quote, and the message says how to get one.
    /// </summary>
    private EngineException Unreachable(string message, long? logOffset)
    {
        if (logOffset is not { } offset)
        {
            return new EngineException("APP_UNREACHABLE", message + "\nset log on this command to keep its output");
        }

        var lines = OutputSince(offset).Select(line => _redact.Redact(line)).ToList();
        var detail = lines.Count == 0
            ? "no output in " + LogDisplayPath()
            : "output in " + LogDisplayPath() + " since " + _label + " started:\n" + string.Join("\n", lines.Select(line => "  " + line));
        return new EngineException("APP_UNREACHABLE", message + "\n" + detail);
    }

    /// <summary>
    /// The last lines appended to the log past <paramref name="offset"/>, terminal controls stripped, bounded in bytes before lines so a chatty
    /// process cannot turn the message into the whole log.
    /// </summary>
    private List<string> OutputSince(long offset)
    {
        try
        {
            using var stream = new FileStream(LogPath(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var end = stream.Length;
            var start = Math.Max(offset, end - OutputTailBytes);
            if (end <= start)
            {
                return [];
            }

            // One byte before a cut tells whether it landed mid-line: a fragment says nothing and goes, a line that ended right at the cut stays.
            var readFrom = start > offset ? start - 1 : start;
            stream.Seek(readFrom, SeekOrigin.Begin);
            var buffer = new byte[end - readFrom];
            stream.ReadExactly(buffer);
            var text = Encoding.UTF8.GetString(buffer);
            var cutMidLine = start > offset && !(text.StartsWith('\r') || text.StartsWith('\n'));
            var lines = LineBreak().Split(ControlSequences().Replace(text, ""))
                .Select(line => line.TrimEnd())
                .Where(line => line.Length > 0)
                .ToList();
            if (cutMidLine && lines.Count > 0)
            {
                lines.RemoveAt(0);
            }

            return lines.Count > OutputTailLines ? lines[^OutputTailLines..] : lines;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private string LogPath() => Path.GetFullPath(_command.Log ?? "", _projectRoot);

    /// <summary>The log as the config names it, relative to the project root, for messages.</summary>
    private string LogDisplayPath() => Path.GetRelativePath(_projectRoot, LogPath());

    /// <summary>
    /// Opens <c>command.log</c> for appending, creating its directory, and returns the stream the process writes stdout and stderr to. No
    /// <c>log</c> means the output is discarded. The same file across services and runs accumulates, which is what a developer reading a
    /// failed boot wants.
    /// </summary>
    private FileStream? OpenLog()
    {
        if (_command.Log is null)
        {
            return null;
        }

        var path = LogPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new EngineException("APP_UNREACHABLE", _label + " could not open its log file " + path + ": " + ex.Message, ex);
        }
    }

    [GeneratedRegex(@"\r\n|\n|\r", RegexOptions.CultureInvariant)]
    private static partial Regex LineBreak();

    [GeneratedRegex(@"\u001B(?:\[[0-?]*[ -/]*[@-~]|\][^\u0007\u001B]*(?:\u0007|\u001B\\)|[@-Z\\-_])", RegexOptions.CultureInvariant)]
    private static partial Regex ControlSequences();

    /// <summary>
    /// Reads the process's stdout and stderr together, so neither pipe fills and blocks it, and appends both to the log (or drops them
    /// when there is none). The stream is closed once the process is gone.
    /// </summary>
    private sealed class OutputPump : IDisposable
    {
        private FileStream? _log;
        private readonly Lock _gate = new();
        private readonly Task _out;
        private readonly Task _err;
        private bool _closed;

        public OutputPump(Process process, FileStream? log)
        {
            _log = log;
            _out = PumpAsync(process.StandardOutput.BaseStream);
            _err = PumpAsync(process.StandardError.BaseStream);
        }

        /// <summary>Waits for both pipes to end, up to <paramref name="timeout"/>: a descendant that outlives the process may hold them open.</summary>
        public async Task DrainAsync(TimeSpan timeout) =>
            await Task.WhenAny(Task.WhenAll(_out, _err), Task.Delay(timeout)).ConfigureAwait(false);

        public void Dispose()
        {
            lock (_gate)
            {
                _closed = true;
                _log?.Dispose();
            }
        }

        private async Task PumpAsync(Stream source)
        {
            var buffer = new byte[8192];
            try
            {
                int read;
                while ((read = await source.ReadAsync(buffer).ConfigureAwait(false)) > 0)
                {
                    lock (_gate)
                    {
                        if (_closed)
                        {
                            continue;
                        }

                        if (_log is not null)
                        {
                            try
                            {
                                _log.Write(buffer, 0, read);
                                _log.Flush();
                            }
                            catch (Exception ex) when (ex is IOException or ObjectDisposedException or UnauthorizedAccessException)
                            {
                                // The log is gone (a full disk, a deleted file): the output is dropped, and the pipes still drain so the app never blocks on them.
                                _log = null;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
            {
                // the pipe closed under the read
            }
        }
    }
}

/// <summary>
/// The Windows job object one started process runs in (a .NET-only guard: upstream signals a process group). It ends everything the process
/// started, whether or not the process is still there, and it ends it too when this process is killed outright, as a test host is once its
/// run ends and no <c>ProcessExit</c> handler runs. Elsewhere <see cref="Create"/> returns null.
/// </summary>
internal sealed class ProcessJob : IDisposable
{
    private const int JobObjectExtendedLimitInformation = 9;
    private const uint KillOnJobClose = 0x2000;

    private IntPtr _job;

    private ProcessJob(IntPtr job) => _job = job;

    public static ProcessJob? Create(Process process)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var job = CreateJobObjectW(IntPtr.Zero, null);
        if (job == IntPtr.Zero)
        {
            return null;
        }

        // JOBOBJECT_EXTENDED_LIMIT_INFORMATION: LimitFlags is the fourth field of the basic limits, after two 8-byte times.
        var size = IntPtr.Size == 8 ? 144 : 112;
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            for (var offset = 0; offset < size; offset += 4)
            {
                Marshal.WriteInt32(buffer, offset, 0);
            }

            Marshal.WriteInt32(buffer, 16, (int)KillOnJobClose);
            if (!SetInformationJobObject(job, JobObjectExtendedLimitInformation, buffer, (uint)size) || !AssignProcessToJobObject(job, process.Handle))
            {
                CloseHandle(job);
                return null;
            }

            return new ProcessJob(job);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // the process is already gone
            CloseHandle(job);
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>Ends every process in the job.</summary>
    public void Terminate()
    {
        if (_job != IntPtr.Zero)
        {
            _ = TerminateJobObject(_job, 1);
        }
    }

    public void Dispose()
    {
        var job = Interlocked.Exchange(ref _job, IntPtr.Zero);
        if (job != IntPtr.Zero)
        {
            CloseHandle(job);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObjectW(IntPtr attributes, string? name);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint size);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateJobObject(IntPtr job, uint exitCode);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
