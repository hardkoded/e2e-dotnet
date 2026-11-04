// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.RegularExpressions;
using E2E.Cli.Report;
using E2E.Internal;
using E2E.Mobile;

namespace E2E.Cli;

/// <summary>
/// <c>e2e explore [goal]</c>: runs <see cref="Explore.ExploreAsync"/> and reports it as upstream's list reporter does, streaming each
/// finding and step as it happens and ending with the findings, the assessment, and a summary. The exit code is the outcome's:
/// 0 when no issue was reported, 1 when one was or nothing could be explored, 2 for a configuration or policy error, 3 for an
/// engine, app, or model provider failure, and 130 when interrupted.
/// </summary>
internal static partial class ExploreCommand
{
    private const string Usage = """
        Usage: e2e explore [options] [goal]

        Run the agent against the app with a goal instead of a test: it plans one exploration step at a time, drives the app, reports every defect it has evidence of, and ends with an assessment. The model is the one the selected agent holds, as for a test.

        goal                what to explore, in a sentence (default: "Explore the app and find bugs")

        Selection:
          --config <path>   config file (default: the nearest e2e.config.json)
          --target <id>     the target to explore (default: the config's target)
          --agent <name>    the configured agent to explore with (default: agents.default)

        Budgets:
          --max-steps <n>   exploration steps at most, {STEPS} (default: {STEPS_DEFAULT})
          --timeout <ms>    wall clock in milliseconds, {TIMEOUT} (default: {TIMEOUT_DEFAULT})

        Execution:
          --headed          show the UI while the agent explores

        Output:
          --output <dir>    output directory: results/ holds finding screenshots and trace pages (default: output in the config, else .e2e)
          --debug           print phase timings and the agent step table to stderr (also E2E_DEBUG=1)
          --ai-trace        record every model call to ai-trace-<process id>.json next to the results directory (unbox-ai) (also E2E_AI_TRACE=1)
          --trace [mode]    keep a trace page of the exploration (bare: on); one attempt, so retry modes keep none. Modes: {MODES} (also E2E_TRACE)
          --video [mode]    record a video of the exploration (bare: on), when the engine supports it; one attempt, so retry modes record none. Modes: {MODES} (also E2E_VIDEO)

        Accounts: every entry of the config's credentials, and every E2E_USER_<NAME>_USERNAME and E2E_USER_<NAME>_PASSWORD pair in the environment, is an account the agent can sign in with. The environment replaces a configured username or password. It fills the password by secret; the password never reaches a model.

        Not supported by the .NET port: --session, --reporter (it refuses these).

        Examples:
          e2e explore
          e2e explore "Explore the checkout flow like a first-time buyer and report anything off"
          e2e explore --target web --max-steps 4 --headed
          e2e explore --agent ux "Review onboarding as a first-time user"
          e2e explore "Hunt for broken forms and dead links" --video

        Exit codes:
          0    the exploration ran and reported no issue (warnings do not count)
          1    at least one issue was reported, or nothing could be explored
          2    CLI, config, or agent policy error
          3    engine, app process, or model provider failure
          130  interrupted by Ctrl-C
        """;

    /// <summary>The flags that take a value.</summary>
    private static readonly HashSet<string> ValueFlags = new(["--config", "--target", "--agent", "--max-steps", "--timeout", "--output"], StringComparer.Ordinal);

    /// <summary>Upstream flags the port has no feature for, and that change what the run means: they are refused.</summary>
    private static readonly HashSet<string> RefusedFlags = new(["--session", "--reporter"], StringComparer.Ordinal);

    /// <summary>The flags that take no value.</summary>
    private static readonly HashSet<string> SwitchFlags = new(["--headed", "--debug", "--ai-trace"], StringComparer.Ordinal);

    /// <summary>The flags that take a recording mode if the next argument is not an option: a bare flag is <c>on</c>.</summary>
    private static readonly HashSet<string> ModeFlags = new(["--trace", "--video"], StringComparer.Ordinal);

    public static string UsageText() => Usage
        .Replace("{MODES}", RecordingModes.List, StringComparison.Ordinal)
        .Replace("{STEPS_DEFAULT}", Explore.StepBounds.Default.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{STEPS}", Explore.StepBounds.Min.ToString(CultureInfo.InvariantCulture) + " through " + Explore.StepBounds.Max.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{TIMEOUT_DEFAULT}", Milliseconds(Explore.TimeoutBounds.Default).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{TIMEOUT}", Milliseconds(Explore.TimeoutBounds.Min).ToString(CultureInfo.InvariantCulture) + " through " + Milliseconds(Explore.TimeoutBounds.Max).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>
    /// Runs one exploration. <paramref name="explore"/> is <see cref="Explore.ExploreAsync"/>; a test passes its own. <paramref name="cwd"/> is the working directory (upstream's <c>process.cwd()</c>), the process's by default.
    /// <paramref name="interactive"/> turns colors on, unless <c>NO_COLOR</c> is set.
    /// </summary>
    public static async Task<int> RunAsync(
        IEnumerable<string> args,
        TextWriter stdout,
        TextWriter stderr,
        bool interactive,
        Func<ExploreOptions, CancellationToken, Task<ExploreOutcome>> explore,
        CancellationToken cancellationToken,
        string? cwd = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        ArgumentNullException.ThrowIfNull(explore);
        cwd = cwd is null ? null : Path.GetFullPath(cwd);

        var parsed = Parse(args);
        if (parsed.Help)
        {
            await stdout.WriteLineAsync(UsageText()).ConfigureAwait(false);
            return 0;
        }

        if (parsed.Error is not null)
        {
            await stderr.WriteLineAsync("error: " + parsed.Error).ConfigureAwait(false);
            return 2;
        }

        var config = TryLoadConfig(parsed, cwd, out var configFailed);
        List<(string Name, UserCredential Credential)> accounts;
        try
        {
            // A config that does not load is the run's error to report, so the environment's accounts wait for it.
            accounts = configFailed ? [] : ScanCredentials(config?.Credentials.Keys);
        }
        catch (E2EException ex)
        {
            // An account the environment half sets is a configuration error, whatever class the credential reader gives it.
            await stderr.WriteLineAsync("error " + ex.Code + ": " + Format.Bounded(ex.Message)).ConfigureAwait(false);
            return 2;
        }

        // The record is the model's and the app's text, redacted where the run writes it to its events, as upstream's emitter does. The
        // errors this command prints itself are redacted here, for the config's secrets and the accounts' passwords.
        var redactor = Redactor.For(
            (config?.AllSecrets ?? []).Concat(accounts.Select(account => account.Credential.Password)).Concat(DeviceTargets.Credentials()));

        // The list reporter renders the run's events: the header, each finding and step as they happen, the record, and the summary.
        var reporter = new ListReporter(
            new StdoutOutput(stdout),
            new ListReporterOptions { Live = false, Colors = interactive && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")) });
        using var run = HostRun.Begin([]);
        using var listening = RunEvents.Subscribe(new RunEventSubscriber("list", runEvent => reporter.Handle(runEvent.Fact)));

        var options = new ExploreOptions
        {
            Goal = parsed.Goal,
            Cwd = cwd,
            ConfigPath = parsed.Config,
            Target = parsed.Target,
            Agent = parsed.Agent,
            MaxSteps = parsed.MaxSteps,
            Timeout = parsed.Timeout,
            Headed = parsed.Headed,
            CreateMobileEngine = DeviceTargets.Create,
            CreateWindowsEngine = WindowsTargets.Create,
            Output = parsed.Output,
            Debug = parsed.Debug,
            AiTrace = parsed.AiTrace,
            Trace = parsed.Trace,
            Video = parsed.Video,
            Credentials = accounts.ToDictionary(account => account.Name, account => account.Credential, StringComparer.Ordinal),
            Notice = message => stderr.WriteLine(Notice(message)),
        };

        ExploreOutcome outcome;
        try
        {
            outcome = await explore(options, cancellationToken).ConfigureAwait(false);
        }
        catch (E2EException ex) when (ex.Code == EngineErrorCodes.Cancelled && cancellationToken.IsCancellationRequested)
        {
            // Opening the host reports an interrupt as CANCELLED, not as OperationCanceledException.
            return 130;
        }
        catch (E2EException ex)
        {
            await stderr.WriteLineAsync("error " + ex.Code + ": " + Format.Bounded(redactor.Redact(ex.Message))).ConfigureAwait(false);
            return Runner.ExitCodeOf(ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 130;
        }

        // The run's end reaches the reporter as `run-finished`, then the rows that close the output.
        await run.FinishAsync().ConfigureAwait(false);
        reporter.Rows([]);

        return outcome.ExitCode;
    }

    /// <summary>
    /// The accounts the environment gives the agent besides the config's: one for each <c>E2E_USER_&lt;NAME&gt;_USERNAME</c>, named in
    /// lower case, read with <see cref="Credentials.User"/> so a missing or short password is its error and not a silent skip. A name
    /// that maps to the variables of a configured credential is that credential, which the config already carries.
    /// </summary>
    internal static List<(string Name, UserCredential Credential)> ScanCredentials(IEnumerable<string>? configured = null)
    {
        var covered = (configured ?? []).Select(name => ConfigSecrets.EnvName("E2E_USER", name)).ToHashSet(StringComparer.Ordinal);
        var names = Environment.GetEnvironmentVariables().Keys
            .Cast<string>()
            .Select(key => UsernameVariable().Match(key))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value.ToLowerInvariant())
            .Where(name => !covered.Contains(ConfigSecrets.EnvName("E2E_USER", name)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        return [.. names.Select(name => (name, Credentials.User(name)))];
    }

    [GeneratedRegex(@"^E2E_USER_([A-Z0-9_]+)_USERNAME\z")]
    private static partial Regex UsernameVariable();

    private static string Notice(string message) => "e2e explore: " + Format.Bounded(message);

    private static int Milliseconds(TimeSpan time) => (int)time.TotalMilliseconds;

    /// <summary>The config the exploration will load, for its target name and secrets. A config that does not load is the run's error to report.</summary>
    private static E2EConfig? TryLoadConfig(ExploreArgs args, string? workingDirectory, out bool failed)
    {
        failed = false;
        try
        {
            var cwd = Path.GetFullPath(workingDirectory ?? Directory.GetCurrentDirectory());
            var path = args.Config is null ? E2EConfig.Find(cwd) : Path.GetFullPath(args.Config, cwd);
            return path is not null && File.Exists(path) ? E2EConfig.Load(path) : null;
        }
        catch (Exception ex) when (ex is E2EException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            failed = true;
            return null;
        }
    }

    /// <summary>Where the list reporter writes: the command's standard output, with no live window.</summary>
    private sealed class StdoutOutput(TextWriter stdout) : IListReporterOutput
    {
        public void Write(string line) => stdout.WriteLine(line);
    }

    private static ExploreArgs Parse(IEnumerable<string> args)
    {
        var result = new ExploreArgs();
        var positional = new List<string>();
        var list = args.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var arg = list[i];
            var eq = arg.StartsWith("--", StringComparison.Ordinal) ? arg.IndexOf('=', StringComparison.Ordinal) : -1;
            var name = eq >= 0 ? arg[..eq] : arg;
            if (name is "-h" or "--help")
            {
                return new ExploreArgs { Help = true };
            }

            if (RefusedFlags.Contains(name))
            {
                return new ExploreArgs { Error = "option '" + name + "' is not supported by the .NET port" };
            }

            if (SwitchFlags.Contains(name))
            {
                if (eq >= 0)
                {
                    return new ExploreArgs { Error = "option '" + name + "' takes no argument" };
                }

                switch (name)
                {
                    case "--headed":
                        result.Headed = true;
                        break;
                    case "--debug":
                        result.Debug = true;
                        break;
                    case "--ai-trace":
                        result.AiTrace = true;
                        break;
                    default:
                        throw new InvalidOperationException("no handler for switch " + name);
                }

                continue;
            }

            if (ModeFlags.Contains(name))
            {
                // As commander's optional value: greedy, so a goal after the flag is read as the mode and refused with the way to write it.
                string? value = null;
                if (eq >= 0)
                {
                    value = arg[(eq + 1)..];
                }
                else if (i + 1 < list.Count && !LooksLikeOption(list[i + 1]))
                {
                    value = list[++i];
                }

                if (ParseMode(name, value, out var mode) is { } problem)
                {
                    return new ExploreArgs { Error = problem };
                }

                if (name == "--trace")
                {
                    result.Trace = mode;
                }
                else
                {
                    result.Video = mode;
                }

                continue;
            }

            if (ValueFlags.Contains(name))
            {
                string value;
                if (eq >= 0)
                {
                    value = arg[(eq + 1)..];
                }
                else if (i + 1 < list.Count)
                {
                    value = list[++i];
                }
                else
                {
                    return new ExploreArgs { Error = "option '" + name + " " + ValueLabel(name) + "' argument missing" };
                }

                if (Apply(result, name, value) is { } problem)
                {
                    return new ExploreArgs { Error = problem };
                }

                continue;
            }

            if (arg.StartsWith('-') && arg.Length > 1)
            {
                return new ExploreArgs { Error = "unknown option '" + arg + "'" };
            }

            positional.Add(arg);
        }

        if (positional.Count > 1)
        {
            return new ExploreArgs
            {
                Error = "too many arguments for 'explore'. Expected 1 argument but got " + positional.Count.ToString(CultureInfo.InvariantCulture) + ".",
            };
        }

        result.Goal = positional.FirstOrDefault();
        return result;
    }

    private static bool LooksLikeOption(string arg) => arg.Length > 1 && arg[0] == '-';

    /// <summary>
    /// <c>--trace [mode]</c> or <c>--video [mode]</c>: a bare flag is <c>on</c>. A value that is not a mode is refused with the way to
    /// write one. Upstream's <c>parseRecordingMode</c>.
    /// </summary>
    private static string? ParseMode(string flag, string? value, out RecordingMode mode)
    {
        if (value is null)
        {
            mode = RecordingMode.On;
            return null;
        }

        if (RecordingModes.TryParse(value, out mode))
        {
            return null;
        }

        var invalid = "option '" + flag + " [mode]' argument '" + value + "' is invalid. ";
        return flag == "--trace" && RecordingModes.TryLegacyTraceSpelling(value, block: false, out var was, out var meant) && was is not null
            ? invalid + "\"" + value + "\" is the old spelling of " + flag + " " + RecordingModes.Name(meant) + "; the modes are " + RecordingModes.List
            : invalid + "expected a mode (" + RecordingModes.List + "), got \"" + value + "\"; write " + flag + "=<mode>, or put test files before " + flag;
    }

    private static string? Apply(ExploreArgs result, string name, string value)
    {
        switch (name)
        {
            case "--config":
                result.Config = value;
                return null;
            case "--target":
                result.Target = value;
                return null;
            case "--agent":
                result.Agent = value;
                return null;
            case "--output":
                result.Output = value;
                return null;
            case "--max-steps":
                var bounds = Explore.StepBounds;
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var steps) || steps < bounds.Min || steps > bounds.Max)
                {
                    return Invalid(name, value, bounds.Min, bounds.Max);
                }

                result.MaxSteps = steps;
                return null;
            default:
                var time = Explore.TimeoutBounds;
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var ms) || ms < Milliseconds(time.Min) || ms > Milliseconds(time.Max))
                {
                    return Invalid(name, value, Milliseconds(time.Min), Milliseconds(time.Max));
                }

                result.Timeout = TimeSpan.FromMilliseconds(ms);
                return null;
        }
    }

    private static string Invalid(string name, string value, int min, int max) =>
        "option '" + name + " " + ValueLabel(name) + "' argument '" + value + "' is invalid. must be an integer from "
        + min.ToString(CultureInfo.InvariantCulture) + " through " + max.ToString(CultureInfo.InvariantCulture);

    private static string ValueLabel(string name) => name switch
    {
        "--config" => "<path>",
        "--target" => "<id>",
        "--agent" => "<name>",
        "--max-steps" => "<n>",
        "--timeout" => "<ms>",
        _ => "<dir>",
    };

    private sealed class ExploreArgs
    {
        public bool Help { get; init; }

        public string? Error { get; init; }

        public string? Goal { get; set; }

        public string? Config { get; set; }

        public string? Target { get; set; }

        public string? Agent { get; set; }

        public int? MaxSteps { get; set; }

        public TimeSpan? Timeout { get; set; }

        public bool Headed { get; set; }

        public string? Output { get; set; }

        public bool Debug { get; set; }

        public bool AiTrace { get; set; }

        public RecordingMode? Trace { get; set; }

        public RecordingMode? Video { get; set; }
    }
}
