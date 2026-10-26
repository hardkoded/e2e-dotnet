// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using E2E.Internal;

namespace E2E;

/// <summary>
/// The command a target's <c>app.command</c> starts the app with (upstream's <c>CommandConfig</c>). It is a program and its arguments,
/// never a shell line: the executable is started directly and each argument is passed as it is.
/// </summary>
public sealed class CommandConfig
{
    /// <summary>The program to start. Required.</summary>
    public required string Executable { get; init; }

    /// <summary>Its arguments, passed one by one. <c>{port}</c> is replaced by the port the app is served on.</summary>
    public IReadOnlyList<string>? Args { get; init; }

    /// <summary>The working directory, relative to the config's directory. Unset, that directory.</summary>
    public string? Cwd { get; init; }

    /// <summary>Variables the process gets, on top of <see cref="InheritEnv"/>. <c>{port}</c> in a value is replaced.</summary>
    public IReadOnlyDictionary<string, string>? Env { get; init; }

    /// <summary>
    /// Variable names, or prefixes ending in <c>*</c>, copied from the environment of the run. The process gets only these,
    /// <see cref="Env"/>, and <c>PATH</c>, <c>HOME</c>, <c>TMPDIR</c>, <c>TMP</c>, <c>TEMP</c>, <c>SystemRoot</c>, and <c>COMSPEC</c>.
    /// </summary>
    public IReadOnlyList<string>? InheritEnv { get; init; }

    /// <summary>How long the app has to become ready. 60 seconds when unset.</summary>
    public TimeSpan? StartupTimeout { get; init; }

    /// <summary>How long the app has to stop before it is killed. 10 seconds when unset.</summary>
    public TimeSpan? ShutdownTimeout { get; init; }

    /// <summary>A file inside the config's directory that takes the output of the process, appended to across runs.</summary>
    public string? Log { get; init; }

    /// <summary>Attach to an app that already answers at the readiness URL instead of failing. Ignored in CI.</summary>
    public bool? ReuseExisting { get; init; }
}

/// <summary>
/// The shape of an <c>app.command</c>, checked once where it is declared (upstream's <c>config/command.ts</c>), so the rest of the
/// resolution only ever sees checked values.
/// </summary>
internal static partial class Command
{
    private static readonly string[] CommandKeys =
        ["executable", "args", "cwd", "env", "inheritEnv", "startupTimeout", "shutdownTimeout", "log", "reuseExisting"];

    private const long MaxTimerMs = int.MaxValue;

    /// <summary>
    /// Checks the shape of one command and returns it (upstream's <c>normalizeCommand</c>): only the keys a command takes, a non-empty
    /// executable, string args and env values, <c>inheritEnv</c> names and trailing-<c>*</c> prefixes, when set positive integer
    /// timeouts, a non-empty <c>log</c>, and a boolean <c>reuseExisting</c>. A misspelled key would otherwise be dropped without a word.
    /// </summary>
    public static CommandConfig Normalize(JsonElement command, string label)
    {
        if (command.ValueKind != JsonValueKind.Object)
        {
            throw Invalid(label + " must be an object");
        }

        RejectUnknownKeys(label, command);
        if (Get(command, "executable") is not { ValueKind: JsonValueKind.String } executable || string.IsNullOrWhiteSpace(executable.GetString()))
        {
            throw Invalid(label + ".executable is required");
        }

        IReadOnlyList<string>? args = null;
        if (Get(command, "args") is { } rawArgs)
        {
            if (rawArgs.ValueKind != JsonValueKind.Array)
            {
                throw Invalid(label + ".args must be an array of strings");
            }

            args = rawArgs.EnumerateArray().Select((item, index) => CommandString(item, label + ".args[" + Number(index) + "]")).ToList().AsReadOnly();
        }

        IReadOnlyDictionary<string, string>? env = null;
        if (Get(command, "env") is { } rawEnv)
        {
            if (rawEnv.ValueKind != JsonValueKind.Object)
            {
                throw Invalid(label + ".env must be an object of variable name to string");
            }

            // A null value is dropped, as a process start drops it: a variable that is unset starts the process without it.
            env = new ReadOnlyDictionary<string, string>(rawEnv.EnumerateObject()
                .Where(entry => entry.Value.ValueKind != JsonValueKind.Null)
                .ToDictionary(entry => entry.Name, entry => CommandString(entry.Value, label + ".env." + entry.Name), StringComparer.Ordinal));
        }

        IReadOnlyList<string>? inheritEnv = null;
        if (Get(command, "inheritEnv") is { } rawInherit)
        {
            if (rawInherit.ValueKind != JsonValueKind.Array)
            {
                throw Invalid(label + ".inheritEnv must be an array of variable names");
            }

            inheritEnv = rawInherit.EnumerateArray().Select((item, index) => InheritPattern(item, label + ".inheritEnv[" + Number(index) + "]")).ToList().AsReadOnly();
        }

        string? cwd = null;
        if (Get(command, "cwd") is { } rawCwd)
        {
            cwd = rawCwd.ValueKind == JsonValueKind.String ? rawCwd.GetString() : throw Invalid(label + ".cwd must be a string");
        }

        var startupTimeout = PositiveMs(Get(command, "startupTimeout"), label + ".startupTimeout");
        var shutdownTimeout = PositiveMs(Get(command, "shutdownTimeout"), label + ".shutdownTimeout");
        string? log = null;
        if (Get(command, "log") is { } rawLog)
        {
            log = rawLog.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(rawLog.GetString())
                ? rawLog.GetString()
                : throw Invalid(label + ".log must be a non-empty path");
        }

        bool? reuseExisting = null;
        if (Get(command, "reuseExisting") is { } rawReuse)
        {
            reuseExisting = rawReuse.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? rawReuse.GetBoolean()
                : throw Invalid(label + ".reuseExisting must be a boolean");
        }

        return new CommandConfig
        {
            Executable = executable.GetString()!,
            Args = args,
            Cwd = cwd,
            Env = env,
            InheritEnv = inheritEnv,
            StartupTimeout = startupTimeout,
            ShutdownTimeout = shutdownTimeout,
            Log = log,
            ReuseExisting = reuseExisting,
        };
    }

    /// <summary>A <c>log</c> must be a file inside the project root: a log outside it would let config write anywhere (upstream's <c>checkLog</c>).</summary>
    public static void CheckLog(CommandConfig command, string label, string projectRoot)
    {
        if (command.Log is { } log && !Paths.InsideProjectRoot(projectRoot, Path.GetFullPath(log, projectRoot)))
        {
            throw Invalid(label + ".log must be a file inside the project root, got " + JsonSerializer.Serialize(log));
        }
    }

    /// <summary>A value as the author wrote it, for messages that quote the offending input (upstream's <c>describeValue</c>).</summary>
    internal static string DescribeValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => JsonSerializer.Serialize(value.GetString()),
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        JsonValueKind.Array => "an array",
        _ => "an object",
    };

    private static JsonElement? Get(JsonElement value, string name) => value.TryGetProperty(name, out var property) ? property : null;

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static ConfigurationException Invalid(string message) => new("INVALID_CONFIG", message);

    /// <summary>Refuses the first key outside the ones a command takes, naming the nearest when it is a plausible typo and every key otherwise.</summary>
    private static void RejectUnknownKeys(string label, JsonElement command)
    {
        foreach (var property in command.EnumerateObject())
        {
            if (CommandKeys.Contains(property.Name, StringComparer.Ordinal))
            {
                continue;
            }

            var hint = Suggestions.DidYouMean(property.Name, CommandKeys);
            throw Invalid(label + " has unknown key \"" + property.Name + "\"" + (hint.Length == 0 ? "; expected one of " + string.Join(", ", CommandKeys) : hint));
        }
    }

    private static string CommandString(JsonElement value, string label) =>
        value.ValueKind == JsonValueKind.String ? value.GetString()! : throw Invalid(label + " must be a string, got " + DescribeValue(value));

    /// <summary>
    /// One <c>inheritEnv</c> entry: a variable name, or a prefix ending in <c>*</c>. A <c>*</c> anywhere else, an <c>=</c> or whitespace,
    /// and a bare <c>*</c> are <c>INVALID_CONFIG</c>; a bare <c>*</c> would pass every variable, model keys included.
    /// </summary>
    private static string InheritPattern(JsonElement value, string label)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw Invalid(label + " must be a string, got " + DescribeValue(value));
        }

        var text = value.GetString()!;
        if (text == "*")
        {
            throw Invalid(label + " cannot be \"*\": it would pass every variable to the app, model keys included; list the names or prefixes it needs");
        }

        if (!InheritName().IsMatch(text))
        {
            throw Invalid(label + " must be a variable name or a prefix ending in * (such as \"STRIPE_*\"), got " + JsonSerializer.Serialize(text));
        }

        return text;
    }

    private static TimeSpan? PositiveMs(JsonElement? value, string label)
    {
        if (value is not { } raw)
        {
            return null;
        }

        if (raw.ValueKind != JsonValueKind.Number || !raw.TryGetInt64(out var ms) || ms <= 0)
        {
            throw Invalid(label + " must be a positive safe integer of milliseconds, got " + DescribeValue(raw));
        }

        // A .NET timer cannot wait longer than int.MaxValue milliseconds.
        return ms > MaxTimerMs
            ? throw Invalid(label + " must be at most " + MaxTimerMs.ToString(CultureInfo.InvariantCulture) + " milliseconds, got " + ms.ToString(CultureInfo.InvariantCulture))
            : TimeSpan.FromMilliseconds(ms);
    }

    [GeneratedRegex(@"\A[^*=\s]+\*?\z", RegexOptions.CultureInvariant)]
    private static partial Regex InheritName();
}
