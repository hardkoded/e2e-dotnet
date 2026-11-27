// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Desktop;

/// <summary>
/// What a desktop worker owns outside its process, and how a later run takes it back: the state directory each worker keeps (its owner file and
/// the record of the app it launched), the sweep that quits an app a killed worker left running and removes its directory, the kill that waits for
/// an app to exit, and the cut of the run's environment the app is given. .NET-only holder name (C# has no free functions): upstream's
/// <c>launch-ownership.ts</c>.
/// </summary>
internal static partial class LaunchOwnership
{
    /// <summary>How often a launch checks whether the app's window can be read yet. Upstream's <c>LAUNCH_POLL_MS</c>.</summary>
    public const int LaunchPollMs = 100;

    /// <summary>The prefix of every worker's private state directory, which a later run sweeps once its worker is gone. Upstream's <c>STATE_PREFIX</c>.</summary>
    public const string StatePrefix = "e2e-desktop-worker-";

    /// <summary>
    /// The file in a state directory that names the process that owns it: its id, and on the next line when it started, which tells it from a later
    /// process the OS gives the same id. Upstream's <c>OWNER_FILE</c>.
    /// </summary>
    public const string OwnerFile = "owner";

    /// <summary>The file in a state directory that names the app the worker launched and its launch token, so a sweep can quit an app a killed worker left running. Upstream's <c>APP_FILE</c>.</summary>
    public const string AppFile = "app.json";

    /// <summary>The prefixes a sweep takes: this engine's, and the agent-desktop engine's before it, whose directories hold the text of fields.</summary>
    private static readonly string[] SweptPrefixes = [StatePrefix, "e2e-agent-desktop-"];

    /// <summary>What the owner file holds once its worker gave the directory up: a later sweep takes it, though the process that wrote it may still run another worker.</summary>
    private const string Abandoned = "abandoned";

    /// <summary>How old a state directory with no owner file must be before a sweep removes it.</summary>
    private static readonly TimeSpan OrphanAge = TimeSpan.FromHours(24);

    /// <summary>
    /// The variables of the run's environment the app under test gets, and nothing else: the run's secrets, model keys, and CI tokens never reach a
    /// process the runner does not control. <c>LC_*</c> passes too.
    /// </summary>
    private static readonly HashSet<string> InheritedEnvNames =
    [
        "HOME",
        "USER",
        "LOGNAME",
        "SHELL",
        "PATH",
        "TMPDIR",
        "LANG",
        "__CF_USER_TEXT_ENCODING",
    ];

    /// <summary>Milliseconds on a clock that is the same for every caller: upstream's <c>Date.now()</c>.</summary>
    public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>Waits <paramref name="ms"/>, or until the token is cancelled, whichever is first; neither throws. Upstream's <c>sleep</c>.</summary>
    public static async Task SleepAsync(int ms, CancellationToken signal)
    {
        if (ms <= 0 || signal.IsCancellationRequested)
        {
            return;
        }

        try
        {
            await Task.Delay(ms, signal).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A cancelled wait ends at once, as upstream's does: the caller reads the signal.
        }
    }

    /// <summary>
    /// What <c>init</c> needs of an <c>appPath</c> bundle: an <c>Info.plist</c> naming the bundle id, which is the target's <c>bundleId</c> when that is
    /// set too. Upstream's <c>checkBundle</c>.
    /// </summary>
    public static async Task<BundleCheck> CheckBundleAsync(string bundle, string? bundleId, CancellationToken signal)
    {
        var plist = Path.Combine(bundle, "Contents", "Info.plist");
        if (!File.Exists(plist))
        {
            return BundleCheck.Failed(
                $"app.appPath {bundle} is not an app bundle: it has no Contents/Info.plist. Build the app first"
            );
        }

        // plutil exits with an error when the key is missing or the file is no plist.
        var declared = (
            await RunAsync("plutil", ["-extract", "CFBundleIdentifier", "raw", "-o", "-", plist], signal)
                .ConfigureAwait(false)
        ).Trim();
        if (declared.Length == 0)
        {
            return BundleCheck.Failed($"{plist} declares no CFBundleIdentifier");
        }

        return bundleId is not null && bundleId != declared
            ? BundleCheck.Failed(
                $"app.bundleId \"{bundleId}\" is not the CFBundleIdentifier \"{declared}\" of app.appPath {bundle}; the build at appPath is what app.open() launches"
            )
            : BundleCheck.Ok(declared);
    }

    /// <summary>The run's environment cut to the variables the app may see (<c>INHERITED_ENV</c>, and <c>LC_*</c>). Upstream's <c>inheritedEnv</c>.</summary>
    public static IReadOnlyDictionary<string, string> InheritedEnv(IReadOnlyDictionary<string, string?> env)
    {
        ArgumentNullException.ThrowIfNull(env);
        var kept = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in env)
        {
            if (
                value is not null
                && (InheritedEnvNames.Contains(name) || name.StartsWith("LC_", StringComparison.Ordinal))
            )
            {
                kept[name] = value;
            }
        }

        return kept;
    }

    /// <summary>
    /// Removes the state directories workers left behind, and quits the app such a worker launched and left running: every process whose
    /// environment carries the token its launch record names, and the process the record names when it is still the one that started then. A
    /// directory goes once the process its owner file names is gone, or, with no owner file, once it is a day old. One still in use is never
    /// touched. A process shown to be the launch's by neither is left alone. When the processes cannot be looked up, or one does not exit, the
    /// record stays for the next run to try again. The agent-desktop engine's directories, which hold the text of fields, are swept the same way.
    /// Upstream's <c>sweepStateDirectories</c>.
    /// </summary>
    public static async Task SweepStateDirectoriesAsync(IDesktopDriver driver)
    {
        ArgumentNullException.ThrowIfNull(driver);
        var root = Path.GetTempPath();
        string[] entries;
        try
        {
            entries = [.. Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName).OfType<string>()];
        }
        catch (Exception cause) when (cause is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var entry in entries)
        {
            if (!SweptPrefixes.Any(prefix => entry.StartsWith(prefix, StringComparison.Ordinal)))
            {
                continue;
            }

            var directory = Path.Combine(root, entry);
            // A shared temporary directory (`/tmp` when TMPDIR is unset) holds other users' entries too: a record this user did not write names no
            // launch of its own, and its process id must not be killed with this user's rights.
            if (!OwnDirectory(directory))
            {
                continue;
            }

            var ownerText = (await ReadTextAsync(Path.Combine(directory, OwnerFile)).ConfigureAwait(false)).Trim();
            var lines = ownerText.Split('\n');
            var ownerId = lines[0];
            var ownerStarted = lines.Length > 1 ? lines[1] : null;
            var owned =
                int.TryParse(ownerId, NumberStyles.None, CultureInfo.InvariantCulture, out var owner) && owner > 0;
            var stale =
                ownerText == Abandoned
                || (
                    owned
                        ? !OwnerRuns(driver, owner, ownerStarted)
                        : DateTime.UtcNow - Directory.GetLastWriteTimeUtc(directory) > OrphanAge
                );
            if (!stale)
            {
                continue;
            }

            // A record cut short by the kill that left it names no process: the directory still goes.
            JsonNode? parsed = null;
            try
            {
                parsed = JsonNode.Parse(await ReadTextAsync(Path.Combine(directory, AppFile)).ConfigureAwait(false));
            }
            catch (JsonException)
            {
                // No record, or a half-written one.
            }

            var launched = parsed as JsonObject;
            var pids = new List<int>();
            var known = true;
            if (
                launched?["token"] is JsonValue tokenValue
                && tokenValue.TryGetValue<string>(out var token)
                && token != ""
            )
            {
                var carriers = await driver.ProcessesCarryingAsync(token).ConfigureAwait(false);
                if (carriers is null)
                {
                    known = false;
                }
                else
                {
                    pids.AddRange(carriers);
                }
            }

            if (
                launched?["pid"] is JsonValue pidValue
                && pidValue.TryGetValue<int>(out var pid)
                && launched["started"] is JsonValue startedValue
                && Recorded(startedValue) is { } started
                && Alive(pid)
            )
            {
                var now = driver.ProcessStarted(pid);
                bool? same = now is null ? null : SameStart(started, now.Value);
                // Running, but when it started cannot be read or matched: whether it is the launch's process is unknown.
                if (same is null)
                {
                    known = false;
                }
                else if (same == true && !pids.Contains(pid))
                {
                    pids.Add(pid);
                }
            }

            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var settled = known && await KilledAsync(pids, Now() + 2_000, budget.Token).ConfigureAwait(false);
            try
            {
                if (settled)
                {
                    Directory.Delete(directory, recursive: true);
                }
                else
                {
                    KeepOnlyRecord(directory);
                }
            }
            catch (Exception cause) when (cause is IOException or UnauthorizedAccessException)
            {
                // The next run's sweep tries again.
            }
        }
    }

    /// <summary>
    /// Whether the worker process an owner file names still runs: one with its id that started when the file says, in this engine's form or the
    /// agent-desktop engine's (<see cref="SameStart"/>). A start time that cannot be read or compared counts as the same process, so a directory
    /// in use is never taken.
    /// </summary>
    private static bool OwnerRuns(IDesktopDriver driver, int pid, string? started)
    {
        if (!Alive(pid))
        {
            return false;
        }

        if (started is null)
        {
            return true;
        }

        var now = driver.ProcessStarted(pid);
        if (now is null)
        {
            return true;
        }

        var recorded = long.TryParse(started, NumberStyles.None, CultureInfo.InvariantCulture, out var micros)
            ? new RecordedStart(micros, null)
            : new RecordedStart(null, started);
        return SameStart(recorded, now.Value) ?? true;
    }

    /// <summary>A start time as a launch record or owner file keeps it: this engine's microseconds, or the agent-desktop engine's <c>ps</c> text.</summary>
    private readonly record struct RecordedStart(long? Micros, string? Text);

    private static RecordedStart? Recorded(JsonValue value)
    {
        if (value.TryGetValue<long>(out var micros))
        {
            return new RecordedStart(micros, null);
        }

        return value.TryGetValue<string>(out var text) ? new RecordedStart(null, text) : null;
    }

    /// <summary>
    /// Whether a recorded start time names the process that started at <paramref name="now"/> (microseconds since the epoch). This engine records
    /// the microseconds; the agent-desktop engine before it recorded <c>ps -o lstart=</c> printed in UTC, to the second. Null for a record in
    /// neither form. Upstream's <c>sameStart</c>.
    /// </summary>
    private static bool? SameStart(RecordedStart recorded, long now)
    {
        if (recorded.Micros is { } micros)
        {
            return micros == now;
        }

        // `ps -o lstart=` pads a single-digit day with a space ("Thu Nov  6 10:00:00 2026"), which the format's single `d` reads once whitespace is folded.
        if (
            !DateTimeOffset.TryParseExact(
                string.Join(' ', (recorded.Text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)),
                "ddd MMM d HH:mm:ss yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var at
            )
        )
        {
            return null;
        }

        return now / 1_000_000 == at.ToUnixTimeSeconds();
    }

    /// <summary>Whether <paramref name="directory"/> is a real directory (not a symlink) that belongs to the user this process runs as.</summary>
    private static bool OwnDirectory(string directory)
    {
        var info = new DirectoryInfo(directory);
        if (!info.Exists || info.LinkTarget is not null)
        {
            return false;
        }

        return Posix.OwnerOf(directory) is not { } owner || owner == Posix.EffectiveUserId();
    }

    /// <summary>
    /// Gives up a state directory whose launch record still names an app: only the record stays, and the owner file says the directory has no
    /// worker any more, so the next sweep takes it even while this process runs on, as a program that opens one host after another does. Upstream's <c>abandon</c>.
    /// </summary>
    public static async Task AbandonAsync(string directory)
    {
        KeepOnlyRecord(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, OwnerFile), Abandoned).ConfigureAwait(false);
    }

    /// <summary>Removes everything in a state directory but its owner file and launch record.</summary>
    private static void KeepOnlyRecord(string directory)
    {
        IEnumerable<string> entries;
        try
        {
            entries = [.. Directory.EnumerateFileSystemEntries(directory)];
        }
        catch (Exception cause) when (cause is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var entry in entries)
        {
            var name = Path.GetFileName(entry);
            if (name is OwnerFile or AppFile)
            {
                continue;
            }

            if (Directory.Exists(entry))
            {
                Directory.Delete(entry, recursive: true);
            }
            else
            {
                File.Delete(entry);
            }
        }
    }

    /// <summary>
    /// Kills each process and waits until all of them exited; false when one still runs at <paramref name="until"/>. A quit cancelled, or out of
    /// time, before the kill sends none: the caller gave the app up as running, and keeps it to quit again. Upstream's <c>killed</c>.
    /// </summary>
    public static async Task<bool> KilledAsync(IReadOnlyCollection<int> pids, long until, CancellationToken signal)
    {
        if (pids.Count > 0 && signal.IsCancellationRequested)
        {
            throw new EngineException(
                "CANCELLED",
                "quitting the app was cancelled before it was killed",
                retryable: false
            );
        }

        if (pids.Count > 0 && Now() >= until)
        {
            throw new EngineException(
                "OPERATION_TIMEOUT",
                "quitting the app ran out of time before it was killed",
                retryable: false
            );
        }

        foreach (var pid in pids)
        {
            Posix.Kill(pid, Posix.SigKill);
        }

        while (pids.Any(Alive))
        {
            if (signal.IsCancellationRequested || Now() >= until)
            {
                return false;
            }

            await SleepAsync(LaunchPollMs, signal).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>Whether a process still runs: it answers signal 0, or exists but belongs to another user. Upstream's <c>alive</c>.</summary>
    public static bool Alive(int pid) => Posix.Kill(pid, 0) == 0 || Marshal.GetLastPInvokeError() == Posix.NotPermitted;

    private static async Task<string> ReadTextAsync(string file)
    {
        try
        {
            return await File.ReadAllTextAsync(file).ConfigureAwait(false);
        }
        catch (Exception cause) when (cause is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    /// <summary>Runs a tool and returns its standard output; empty when it fails or is cancelled. Arguments go through <c>ArgumentList</c>, stderr is read beside stdout, and the process tree is killed on cancel.</summary>
    private static async Task<string> RunAsync(string file, IReadOnlyList<string> arguments, CancellationToken signal)
    {
        var info = new ProcessStartInfo(file)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(info);
            if (process is null)
            {
                return "";
            }

            var output = process.StandardOutput.ReadToEndAsync(signal);
            var error = process.StandardError.ReadToEndAsync(signal);
            try
            {
                await process.WaitForExitAsync(signal).ConfigureAwait(false);
                var text = await output.ConfigureAwait(false);
                _ = await error.ConfigureAwait(false);
                return process.ExitCode == 0 ? text : "";
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                return "";
            }
        }
        catch (Exception cause) when (cause is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return "";
        }
    }
}

/// <summary>What <see cref="LaunchOwnership.CheckBundleAsync"/> found: the bundle id, or the problem. Upstream's <c>{ bundleId } | { problem }</c>.</summary>
internal sealed record BundleCheck
{
    public string? BundleId { get; init; }

    public string? Problem { get; init; }

    public static BundleCheck Ok(string bundleId) => new() { BundleId = bundleId };

    public static BundleCheck Failed(string problem) => new() { Problem = problem };
}

/// <summary>The few POSIX calls the launch ownership needs, which .NET has no managed form of.</summary>
internal static partial class Posix
{
    public const int SigKill = 9;

    /// <summary><c>EPERM</c>.</summary>
    public const int NotPermitted = 1;

    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static partial int KillNative(int pid, int signal);

    [LibraryImport("libc", EntryPoint = "geteuid")]
    private static partial uint GetEffectiveUserId();

    [LibraryImport("libc", EntryPoint = "lstat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int LStat(string path, Span<byte> buffer);

    /// <summary>Sends <paramref name="signal"/> to <paramref name="pid"/>; 0 on success. Never throws: a platform without libc answers -1.</summary>
    public static int Kill(int pid, int signal)
    {
        try
        {
            return KillNative(pid, signal);
        }
        catch (Exception cause) when (cause is DllNotFoundException or EntryPointNotFoundException)
        {
            return -1;
        }
    }

    public static uint EffectiveUserId()
    {
        try
        {
            return GetEffectiveUserId();
        }
        catch (Exception cause) when (cause is DllNotFoundException or EntryPointNotFoundException)
        {
            return uint.MaxValue;
        }
    }

    /// <summary>The user id that owns <paramref name="path"/> (<c>st_uid</c>, at offset 16 on macOS and 24 on Linux); null when it cannot be read.</summary>
    public static uint? OwnerOf(string path)
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
        {
            return null;
        }

        try
        {
            Span<byte> buffer = stackalloc byte[256];
            if (LStat(path, buffer) != 0)
            {
                return null;
            }

            var offset =
                OperatingSystem.IsMacOS() ? 16
                : RuntimeInformation.ProcessArchitecture == Architecture.X64 ? 28
                : 24;
            return BitConverter.ToUInt32(buffer[offset..]);
        }
        catch (Exception cause) when (cause is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }
}
