// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel;

namespace E2E.Desktop.MacOs;

/// <summary>
/// Launching an app: always a new instance, through LaunchServices (<c>open -n</c>). The app gets launchd's environment plus the
/// variables the caller names, and never this process's environment, so the run's secrets stay behind.
/// </summary>
internal static class Launch
{
    /// <summary>The variable that carries a launch's token into the process it starts.</summary>
    public const string TokenVariable = "E2E_DESKTOP_LAUNCH";

    /// <summary>The environment entry of a launch's token.</summary>
    public static string TokenMarker(string token) => $"{TokenVariable}={token}";

    /// <summary>
    /// The argument that carries a launch's token into an app whose environment macOS hides: <c>-E2EDesktopLaunch=&lt;token&gt;</c>, one
    /// entry, so no app reads the token as a file or a URL to open.
    /// </summary>
    public static string TokenArgument(string token) => $"-E2EDesktopLaunch={token}";

    /// <summary>What <see cref="Start"/> was asked to start. Rust's <c>launch::LaunchRequest</c>.</summary>
    public sealed record Request
    {
        /// <summary>A <c>.app</c> bundle path, launched as it is.</summary>
        public string? Path { get; init; }

        /// <summary>A bundle id or an app name, looked up among the installed apps.</summary>
        public string? BundleId { get; init; }

        public IReadOnlyList<string> Arguments { get; init; } = [];

        /// <summary>The whole environment <c>open</c> runs with, which the app inherits.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Environment { get; init; } = [];

        /// <summary>Marks the process the launch starts.</summary>
        public required string Token { get; init; }

        public bool Activate { get; init; }

        /// <summary>The whole launch's budget: <c>open</c>, then the search for the process it started.</summary>
        public TimeSpan Timeout { get; init; }

        /// <summary>
        /// How long the search for the process may take once <c>open</c> has exited, within <see cref="Timeout"/>: a slow <c>open</c>
        /// (a cold start, Gatekeeper checking a new build) does not eat into it.
        /// </summary>
        public TimeSpan Claim { get; init; }

        /// <summary>When the caller asked, which <see cref="Timeout"/> counts from: the lookup and the signature check before <c>open</c> take time.</summary>
        public Instant Asked { get; init; }

        /// <summary>Names the launch, so <see cref="Input.CancelInput"/> can stop it before <c>open</c> runs.</summary>
        public uint? InputId { get; init; }
    }

    /// <summary>The folders an app named by its name is looked for in, in order.</summary>
    private static List<string> AppFolders()
    {
        var folders = new List<string>
        {
            "/Applications",
            "/Applications/Utilities",
            "/System/Applications",
            "/System/Applications/Utilities",
        };
        if (Environment.GetEnvironmentVariable("HOME") is { } home)
        {
            folders.Add($"{home}/Applications");
        }

        return folders;
    }

    /// <summary>The path of the bundle of an installed app: by bundle id first, then by name in the usual folders, in any case, then by name through LaunchServices.</summary>
    private static string? Installed(string target)
    {
        var workspace = ObjC.Send(ObjC.Class("NSWorkspace"), "sharedWorkspace");
        var url = ObjC.WithString(target, name => ObjC.Send(workspace, "URLForApplicationWithBundleIdentifier:", name));
        if (url != 0 && ObjC.Send(url, "path") is var urlPath && urlPath != 0)
        {
            return Cf.ToManaged(urlPath);
        }

        var stem = target;
        while (stem.EndsWith(".app", StringComparison.Ordinal))
        {
            stem = stem[..^4];
        }

        var wanted = $"{stem}.app".ToLowerInvariant();
        foreach (var folder in AppFolders())
        {
            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(folder).ToList();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                if (Path.GetFileName(entry).ToLowerInvariant() == wanted)
                {
                    return entry;
                }
            }
        }

        // An app LaunchServices knows by name anywhere else, such as a build in a project's directory or a subfolder of
        // Applications. Deprecated for `URLForApplicationWithBundleIdentifier`, which takes no name.
        var full = ObjC.WithString(target, name => ObjC.Send(workspace, "fullPathForApplication:", name));
        return full == 0 ? null : Cf.ToManaged(full);
    }

    /// <summary>The process ids of an app's copies running now.</summary>
    public static List<int> RunningCopies(string bundleId)
    {
        var apps = ObjC.WithString(bundleId, name =>
            ObjC.Send(ObjC.Class("NSRunningApplication"), "runningApplicationsWithBundleIdentifier:", name)
        );
        var pids = new List<int>();
        if (apps == 0)
        {
            return pids;
        }

        var count = (int)Native.CFArrayGetCount(apps);
        for (var index = 0; index < count; index++)
        {
            pids.Add(ObjC.SendInt(Native.CFArrayGetValueAtIndex(apps, index), "processIdentifier"));
        }

        return pids;
    }

    /// <summary>The bundle id of a bundle on disk.</summary>
    private static string? BundleIdOf(string bundle)
    {
        var plist = Path.Combine(bundle, "Contents/Info.plist");
        var dictionary = ObjC.WithString(plist, file =>
            ObjC.Send(ObjC.Class("NSDictionary"), "dictionaryWithContentsOfFile:", file)
        );
        if (dictionary == 0)
        {
            return null;
        }

        var value = ObjC.WithString("CFBundleIdentifier", key => ObjC.Send(dictionary, "objectForKey:", key));
        return value != 0 && Native.CFGetTypeID(value) == Cf.StringType ? Cf.ToManaged(value) : null;
    }

    /// <summary>Whether macOS hides the environment of the bundle's process from other processes, from its signature (<c>codesign -dvv</c>).</summary>
    private static bool HidesEnvironment(string bundle) =>
        Subprocess.RunForStderr("/usr/bin/codesign", ["-dvv", bundle], [], TimeSpan.FromSeconds(30)) is { } signature
        && HiddenBy(signature);

    /// <summary>
    /// Whether a signature, as <c>codesign -dvv</c> prints it, is one whose process macOS hides the environment of: Apple's own apps
    /// (a platform identifier), Apple's App Store apps (Numbers, Pages, Xcode: signed by Apple Mac OS Application Signing, with no
    /// platform identifier), and builds signed with the <c>restrict</c> flag. Every Mac App Store app carries that signature, so each
    /// gets the token as an argument too, which the claim reads only when the environment is hidden.
    /// </summary>
    internal static bool HiddenBy(string signature) =>
        signature
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Any(line =>
                line.StartsWith("Platform identifier=", StringComparison.Ordinal)
                || line == "Authority=Apple Mac OS Application Signing"
                || (
                    line.StartsWith("CodeDirectory ", StringComparison.Ordinal)
                    && line.Contains("restrict", StringComparison.Ordinal)
                )
            );

    /// <summary>
    /// Starts a new instance of the app with <c>open -n</c>, run with exactly the environment given (LaunchServices hands the app the
    /// environment of the process that asks), and finds the process it started: the new copy whose environment carries the launch
    /// token. An app whose environment macOS hides also gets the token as its last argument (<c>-E2EDesktopLaunch=&lt;token&gt;</c>, which
    /// AppKit ignores), which stays readable. A copy with neither is never taken: it may be one the user opened meanwhile.
    /// </summary>
    public static Launched Start(Request request)
    {
        var started = request.Asked;
        using var run = Input.TypingRun.Start(request.InputId);
        string bundle;
        if (request.Path is { } path)
        {
            if (!File.Exists(Path.Combine(path, "Contents/Info.plist")))
            {
                throw Failure.Refused(Codes.AppNotFound, $"{path} is not an app bundle");
            }

            bundle = Standardize(path);
        }
        else if (request.BundleId is { } target)
        {
            bundle = Installed(target)
                ?? throw Failure.Refused(Codes.AppNotFound, $"no installed app has the name or bundle id \"{target}\"");
        }
        else
        {
            throw Failure.Refused(Codes.InvalidArgs, "a launch needs a bundle path or a bundle id");
        }

        var bundleId = BundleIdOf(bundle)
            ?? throw Failure.Refused(Codes.AppNotFound, $"{bundle} declares no CFBundleIdentifier");
        var before = RunningCopies(bundleId);
        var token = request.Token;

        var arguments = new List<string> { "-n" };
        if (!request.Activate)
        {
            arguments.Add("-g");
        }

        arguments.Add("--env");
        arguments.Add(TokenMarker(token));
        arguments.Add(bundle);
        arguments.Add("--args");
        arguments.AddRange(request.Arguments);
        if (HidesEnvironment(bundle))
        {
            arguments.Add(TokenArgument(token));
        }

        // The lookup and `codesign` above can take a while: an app the caller gave up on is never started.
        if (run.Cancelled() || started.Elapsed >= request.Timeout)
        {
            throw Failure.Refused(Codes.Timeout, $"the launch of {bundle} was given up before open ran");
        }

        Subprocess child;
        try
        {
            child = Subprocess.Start("/usr/bin/open", arguments, request.Environment);
        }
        catch (Win32Exception error)
        {
            throw Failure.Refused(Codes.Internal, $"could not run open: {error.Message}");
        }

        using (child)
        {
            while (true)
            {
                var exit = child.TryWait();
                if (exit == 0)
                {
                    break;
                }

                if (exit is not null)
                {
                    throw Failure
                        .Refused(Codes.ActionFailed, $"open could not start {bundle}: {child.Stderr.Trim()}")
                        .WithDetail("kind", "not_started");
                }

                if (started.Elapsed >= request.Timeout)
                {
                    child.Kill();
                    throw Failure.Unconfirmed(Codes.Timeout, $"opening {bundle} did not finish in time");
                }

                Thread.Sleep(10);
            }
        }

        var marker = TokenMarker(token);
        var claimUntil = Instant.Min(Instant.After(request.Claim), started + request.Timeout);
        while (true)
        {
            var fresh = RunningCopies(bundleId).Where(pid => !before.Contains(pid)).ToList();
            foreach (var pid in fresh)
            {
                var ours = ProcessInfo.Carries(pid, marker) ?? ProcessInfo.HoldsArgument(pid, TokenArgument(token));
                if (ours)
                {
                    return LaunchedApp(pid, bundle, bundleId);
                }
            }

            if (Instant.Now >= claimUntil)
            {
                throw Failure
                    .Unconfirmed(Codes.Timeout, $"{bundle} was opened, but no new process carries this launch's token")
                    .WithDetail("kind", "unclaimed");
            }

            Thread.Sleep(50);
        }
    }

    /// <summary>The path as an <c>NSURL</c> reads it back: absolute, without a trailing slash.</summary>
    private static string Standardize(string path)
    {
        var full = Path.GetFullPath(path);
        return full.Length > 1 ? full.TrimEnd('/') : full;
    }

    private static Launched LaunchedApp(int pid, string bundle, string bundleId)
    {
        var app = ObjC.SendPid(ObjC.Class("NSRunningApplication"), "runningApplicationWithProcessIdentifier:", pid);
        var localized = app == 0 ? 0 : ObjC.Send(app, "localizedName");
        var name = localized == 0 ? null : Cf.ToManaged(localized);
        return new Launched(pid, name ?? Path.GetFileNameWithoutExtension(bundle), bundleId, ProcessInfo.Started(pid));
    }
}
