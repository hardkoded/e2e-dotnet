// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Globalization;
using E2E.Cli.Report;
using E2E.Engine;
using E2E.Internal;

namespace E2E.Cli;

/// <summary>
/// <c>e2e doctor [--config &lt;path&gt;] [--target &lt;names&gt;] [--headed] [--check-model] [--json]</c>: runs <see cref="Doctor.RunAsync"/> and prints one line per
/// check, or the <c>doctor-1</c> report with <c>--json</c>. The exit code is 0 when no check failed, 2 when one did, and 130 when interrupted.
/// </summary>
internal static class DoctorCommand
{
    private const string Usage = """
        Usage: e2e doctor [options]

        Check that e2e explore and e2e mcp can start here, without starting anything: check the runtime, load and resolve the config, ask each target's engine what it needs on this machine, and name the model each agent resolved to. Starts no app and no session, and calls no model unless --check-model is given. Ctrl-C stops the checks and reports what ran. A model on an e2e login passes when the login is stored; any other model reads its own credential, which doctor reports as unknown.

        Options:
          --config <path>   config file (default: the nearest e2e.config.json)
          --target <ids>    check only these targets, comma-separated or repeated (default: all targets)
          --headed          check for what a headed run needs, when the engine tells the two apart
          --check-model     call each agent's model once to prove it answers; costs one small request per model
          --json            print the doctor-1 report as JSON instead of one line per check

        Examples:
          e2e doctor
          e2e doctor --json
          e2e doctor --check-model
          e2e doctor --target web --headed

        Exit codes:
          0    no check failed (warn, skip, and unknown never fail)
          2    at least one check failed
          130  interrupted by Ctrl-C
        """;

    public static string UsageText() => Usage;

    /// <summary>
    /// Runs one check of the project. <paramref name="cwd"/> is the working directory (upstream's <c>process.cwd()</c>), the process's by
    /// default; <paramref name="createEngine"/> builds the engine of a target and <paramref name="env"/> is the environment (the process's
    /// by default), both test seams. <paramref name="interactive"/> turns colors on, unless <c>NO_COLOR</c> is set.
    /// </summary>
    public static async Task<int> RunAsync(
        IEnumerable<string> args,
        TextWriter stdout,
        TextWriter stderr,
        bool interactive,
        CancellationToken cancellationToken,
        string? cwd = null,
        Func<TargetConfig, bool, IEngine>? createEngine = null,
        IReadOnlyDictionary<string, string?>? env = null,
        Func<AgentConfig, bool, IAgentModel?>? createModel = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        var parsed = Parse(args);
        if (parsed.Help)
        {
            await stdout.WriteLineAsync(Usage).ConfigureAwait(false);
            return 0;
        }

        if (parsed.Error is not null)
        {
            await stderr.WriteLineAsync("error: " + parsed.Error).ConfigureAwait(false);
            return 2;
        }

        var report = await Doctor.RunAsync(
            new DoctorOptions
            {
                Version = ModelHttp.PackageVersion(),
                Cwd = cwd ?? Directory.GetCurrentDirectory(),
                Env = env ?? ProcessEnvironment(),
                Config = parsed.Config,
                Targets = parsed.Targets,
                Headed = parsed.Headed,
                CheckModel = parsed.CheckModel,
                CreateModel = createModel,
                Notice = (target, line) => stderr.WriteLine(Format.Bounded("[" + target + "] " + line)),
                CreateEngine = createEngine,
            },
            cancellationToken).ConfigureAwait(false);
        if (parsed.Json)
        {
            await stdout.WriteAsync(report.ToJson() + "\n").ConfigureAwait(false);
        }
        else
        {
            var colors = new Colors(interactive && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")));
            await stdout.WriteAsync(Doctor.FormatDoctorReport(report, colors)).ConfigureAwait(false);
        }

        return cancellationToken.IsCancellationRequested ? 130 : report.Ok ? 0 : 2;
    }

    private static Dictionary<string, string?> ProcessEnvironment() =>
        Environment.GetEnvironmentVariables().Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value, StringComparer.Ordinal);

    private static DoctorArgs Parse(IEnumerable<string> args)
    {
        var result = new DoctorArgs();
        var targets = new List<string>();
        var positional = 0;
        var list = args.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var arg = list[i];
            var eq = arg.StartsWith("--", StringComparison.Ordinal) ? arg.IndexOf('=', StringComparison.Ordinal) : -1;
            var name = eq >= 0 ? arg[..eq] : arg;
            switch (name)
            {
                case "-h" or "--help":
                    return new DoctorArgs { Help = true };
                case "--headed" or "--json" or "--check-model":
                    if (eq >= 0)
                    {
                        return new DoctorArgs { Error = "option '" + name + "' takes no argument" };
                    }

                    switch (name)
                    {
                        case "--headed":
                            result.Headed = true;
                            break;
                        case "--check-model":
                            result.CheckModel = true;
                            break;
                        default:
                            result.Json = true;
                            break;
                    }

                    break;
                case "--config" or "--target":
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
                        return new DoctorArgs { Error = "option '" + name + (name == "--config" ? " <path>" : " <ids>") + "' argument missing" };
                    }

                    if (name == "--config")
                    {
                        result.Config = value;
                    }
                    else if (Names(value) is { Count: > 0 } names)
                    {
                        targets.AddRange(names);
                    }
                    else
                    {
                        return new DoctorArgs { Error = "option '--target <ids>' argument '" + value + "' is invalid. name at least one target" };
                    }

                    break;
                default:
                    if (arg.StartsWith('-') && arg.Length > 1)
                    {
                        return new DoctorArgs { Error = "unknown option '" + arg + "'" };
                    }

                    positional++;
                    break;
            }
        }

        if (positional > 0)
        {
            return new DoctorArgs { Error = "too many arguments for 'doctor'. Expected 0 arguments but got " + positional.ToString(CultureInfo.InvariantCulture) + "." };
        }

        result.Targets = targets;
        return result;
    }

    /// <summary>The names in a comma-separated list, trimmed, without blanks (upstream's <c>parseNames</c>).</summary>
    private static List<string> Names(string value) => [.. value.Split(',').Select(name => name.Trim()).Where(name => name.Length > 0)];

    private sealed class DoctorArgs
    {
        public bool Help { get; init; }

        public string? Error { get; init; }

        public string? Config { get; set; }

        public IReadOnlyList<string>? Targets { get; set; }

        public bool Headed { get; set; }

        public bool CheckModel { get; set; }

        public bool Json { get; set; }
    }
}
