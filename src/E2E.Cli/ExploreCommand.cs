// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.RegularExpressions;
using E2E.Cli.Report;
using E2E.Internal;

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
          --output <dir>    results directory: finding screenshots (default: .e2e/results)

        Accounts: every E2E_USER_<NAME>_USERNAME and E2E_USER_<NAME>_PASSWORD pair in the environment is an account the agent can sign in with. It fills the password by secret; the password never reaches a model.

        Not supported by the .NET port: --session, --reporter (it refuses these), and --trace, --video, --debug, --ai-trace (it prints a notice and goes on).

        Examples:
          e2e explore
          e2e explore "Explore the checkout flow like a first-time buyer and report anything off"
          e2e explore --target web --max-steps 4 --headed
          e2e explore --agent ux "Review onboarding as a first-time user"

        Exit codes:
          0    the exploration ran and reported no issue (warnings do not count)
          1    at least one issue was reported, or nothing could be explored
          2    CLI, config, or agent policy error
          3    engine, app process, or model provider failure
          130  interrupted by Ctrl-C
        """;

    private const int DefaultColumns = 80;

    /// <summary>The flags that take a value.</summary>
    private static readonly HashSet<string> ValueFlags = new(["--config", "--target", "--agent", "--max-steps", "--timeout", "--output"], StringComparer.Ordinal);

    /// <summary>Upstream flags the port has no feature for, and that change what the run means: they are refused.</summary>
    private static readonly HashSet<string> RefusedFlags = new(["--session", "--reporter"], StringComparer.Ordinal);

    /// <summary>Upstream flags the port has no feature for, and that only add output: a notice says so and the run goes on.</summary>
    private static readonly HashSet<string> IgnoredFlags = new(["--debug", "--ai-trace", "--trace", "--video"], StringComparer.Ordinal);

    public static string UsageText() => Usage
        .Replace("{STEPS_DEFAULT}", Explore.StepBounds.Default.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{STEPS}", Explore.StepBounds.Min.ToString(CultureInfo.InvariantCulture) + " through " + Explore.StepBounds.Max.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{TIMEOUT_DEFAULT}", Milliseconds(Explore.TimeoutBounds.Default).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{TIMEOUT}", Milliseconds(Explore.TimeoutBounds.Min).ToString(CultureInfo.InvariantCulture) + " through " + Milliseconds(Explore.TimeoutBounds.Max).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>
    /// Runs one exploration. <paramref name="explore"/> is <see cref="Explore.ExploreAsync"/>; a test passes its own.
    /// <paramref name="interactive"/> turns colors on, unless <c>NO_COLOR</c> is set.
    /// </summary>
    public static async Task<int> RunAsync(
        IEnumerable<string> args,
        TextWriter stdout,
        TextWriter stderr,
        bool interactive,
        Func<ExploreOptions, CancellationToken, Task<ExploreOutcome>> explore,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        ArgumentNullException.ThrowIfNull(explore);

        var parsed = Parse(args, out var notices);
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

        foreach (var notice in notices)
        {
            await stderr.WriteLineAsync(Notice(notice)).ConfigureAwait(false);
        }

        List<(string Name, UserCredential Credential)> accounts;
        try
        {
            accounts = ScanCredentials();
        }
        catch (E2EException ex)
        {
            // An account the environment half sets is a configuration error, whatever class the credential reader gives it.
            await stderr.WriteLineAsync("error " + ex.Code + ": " + Format.Bounded(ex.Message)).ConfigureAwait(false);
            return 2;
        }

        var redactor = Redactor.For(accounts.Select(account => account.Credential.Password));
        var pc = new Colors(interactive && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")));
        var columns = interactive ? WindowWidth() : DefaultColumns;
        var gate = new object();
        ExploreView? view = null;
        var startedAt = DateTimeOffset.Now;

        void Print(string line)
        {
            stdout.WriteLine(line);
        }

        void OnProgress(ExploreProgress progress)
        {
            lock (gate)
            {
                if (progress is ExploreProgress.Started started)
                {
                    view = new ExploreView(pc, started, columns);
                    Print("");
                    Print(view.Header(pc.Badge(Badge(parsed))));
                    return;
                }

                if (view is null)
                {
                    return;
                }

                view.Progress(progress);
                if (progress is ExploreProgress.StepFinished && view.LastStep is { } last)
                {
                    foreach (var line in view.StepLines(last))
                    {
                        Print(line);
                    }
                }
                else if (progress is ExploreProgress.FindingReported reported)
                {
                    Print(view.FindingLine(reported.Finding));
                }
            }
        }

        var options = new ExploreOptions
        {
            Goal = parsed.Goal,
            ConfigPath = parsed.Config,
            Target = parsed.Target,
            Agent = parsed.Agent,
            MaxSteps = parsed.MaxSteps,
            Timeout = parsed.Timeout,
            Headed = parsed.Headed,
            Output = parsed.Output,
            Credentials = accounts.ToDictionary(account => account.Name, account => account.Credential, StringComparer.Ordinal),
            Notice = message => stderr.WriteLine(Notice(message)),
            OnProgress = OnProgress,
        };

        ExploreOutcome outcome;
        try
        {
            outcome = await explore(options, cancellationToken).ConfigureAwait(false);
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

        lock (gate)
        {
            if (view is not null)
            {
                foreach (var line in view.Record().Concat(view.FindingsSection()).Concat(view.Assessment()))
                {
                    Print(line);
                }
            }

            // The verdict is the findings; any other error is a failure of its own.
            if (outcome.Error is { } error && view?.IsVerdict(error) != true)
            {
                Print("");
                Print(" " + pc.Bold(pc.Red(error.Code)) + ": " + Format.Bounded(redactor.Redact(error.Message)));
            }

            Print("");
            foreach (var row in (view ?? new ExploreView(pc, new ExploreProgress.Started(outcome.Explore.Goal, outcome.Explore.Budgets), columns)).SummaryRows())
            {
                Print(row);
            }

            Print(Format.PadTitle(pc, "Start at") + Format.FormatClock(startedAt));
            Print(Format.PadTitle(pc, "Duration") + Format.FormatTime(DateTimeOffset.Now - startedAt));
            Print("");
        }

        return outcome.ExitCode;
    }

    /// <summary>
    /// The accounts the environment gives the agent: one for each <c>E2E_USER_&lt;NAME&gt;_USERNAME</c>, named in lower case, read
    /// with <see cref="Credentials.User"/> so a missing or short password is its error and not a silent skip.
    /// </summary>
    internal static List<(string Name, UserCredential Credential)> ScanCredentials()
    {
        var names = Environment.GetEnvironmentVariables().Keys
            .Cast<string>()
            .Select(key => UsernameVariable().Match(key))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        return [.. names.Select(name => (name, Credentials.User(name)))];
    }

    [GeneratedRegex(@"^E2E_USER_([A-Z0-9_]+)_USERNAME\z")]
    private static partial Regex UsernameVariable();

    private static string Notice(string message) => "e2e explore: " + Format.Bounded(message);

    private static int Milliseconds(TimeSpan time) => (int)time.TotalMilliseconds;

    private static int WindowWidth()
    {
        try
        {
            return Console.WindowWidth > 0 ? Console.WindowWidth : DefaultColumns;
        }
        catch (IOException)
        {
            return DefaultColumns;
        }
    }

    /// <summary>The target the exploration runs on: the one named, else the config's.</summary>
    private static string Badge(ExploreArgs args)
    {
        if (args.Target is not null)
        {
            return "|" + Format.Bounded(args.Target) + "|";
        }

        try
        {
            var cwd = Directory.GetCurrentDirectory();
            var path = args.Config is null ? E2EConfig.Find(cwd) : Path.GetFullPath(args.Config, cwd);
            return path is null ? "" : "|" + Format.Bounded(E2EConfig.Load(path).Target.Name) + "|";
        }
        catch (E2EException)
        {
            // The run reports the config's error itself.
            return "";
        }
    }

    private static ExploreArgs Parse(IEnumerable<string> args, out List<string> notices)
    {
        notices = [];
        var result = new ExploreArgs();
        var positional = new List<string>();
        using var e = args.GetEnumerator();
        while (e.MoveNext())
        {
            var arg = e.Current;
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

            if (IgnoredFlags.Contains(name))
            {
                notices.Add(name + " is not supported by the .NET port; ignored");
                continue;
            }

            if (name == "--headed")
            {
                if (eq >= 0)
                {
                    return new ExploreArgs { Error = "option '--headed' takes no argument" };
                }

                result.Headed = true;
                continue;
            }

            if (ValueFlags.Contains(name))
            {
                string value;
                if (eq >= 0)
                {
                    value = arg[(eq + 1)..];
                }
                else if (e.MoveNext())
                {
                    value = e.Current;
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
    }
}
