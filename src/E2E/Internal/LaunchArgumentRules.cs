// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace E2E.Internal;

/// <summary>
/// Launch arguments after the checks: a list of strings, or an object of argument name to value (upstream's <c>ResolvedLaunchArguments</c>,
/// <c>readonly string[] | LaunchArgumentValues</c>). Exactly one of <see cref="List"/> and <see cref="Values"/> is set. .NET-only holder: C# has no union type.
/// </summary>
public sealed class ResolvedLaunchArguments
{
    internal ResolvedLaunchArguments(
        IReadOnlyList<string>? list,
        IReadOnlyDictionary<string, LaunchArgumentValue>? values
    )
    {
        List = list;
        Values = values;
    }

    /// <summary>The arguments as a list of strings, or null for the object form.</summary>
    public IReadOnlyList<string>? List { get; }

    /// <summary>The arguments as an object of argument name to value, in declaration order, or null for the list form.</summary>
    public IReadOnlyDictionary<string, LaunchArgumentValue>? Values { get; }

    /// <summary>The list form of <paramref name="list"/>.</summary>
    public static ResolvedLaunchArguments Of(IReadOnlyList<string> list) => new(list, null);

    /// <summary>The object form of <paramref name="values"/>.</summary>
    public static ResolvedLaunchArguments Of(IReadOnlyDictionary<string, LaunchArgumentValue> values) =>
        new(null, values);
}

/// <summary>
/// The checks and spellings of launch arguments (upstream's <c>core/internal/launch-arguments.ts</c>: <c>checkLaunchArguments</c>,
/// <c>engineLaunchArguments</c>, <c>appleUserDefaults</c>). .NET-only holder name: C# has no free functions.
/// </summary>
public static partial class LaunchArgumentRules
{
    private const double MaxSafeInteger = 9007199254740991;

    private const string Shape =
        "an array of strings or an object of argument name to string, number, boolean, or array of strings";

    /// <summary>
    /// Checks launch arguments as declared in code: a list of strings, or an object of argument name (non-empty, no leading dash, no whitespace
    /// or control characters) to string, finite number below 2^53 in magnitude, boolean, or list of strings, none holding a NUL character. A
    /// null value is the entry left out. <paramref name="code"/> picks the error: a target's config fails with <c>INVALID_CONFIG</c>, a test's own
    /// launch with <c>INVALID_ARGUMENT</c>. Declaring both <paramref name="list"/> and <paramref name="values"/> is refused: .NET-only, since upstream's
    /// one property holds one form.
    /// </summary>
    public static ResolvedLaunchArguments? CheckLaunchArguments(
        string label,
        IReadOnlyList<string>? list,
        IReadOnlyDictionary<string, LaunchArgumentValue?>? values,
        string code = "INVALID_CONFIG"
    )
    {
        ArgumentNullException.ThrowIfNull(label);
        if (list is not null && values is not null)
        {
            throw Fail(
                code,
                label
                    + " is declared as both a list and an object of values: declare one of LaunchArguments and LaunchArgumentValues"
            );
        }

        if (list is not null)
        {
            for (var index = 0; index < list.Count; index++)
            {
                if (list[index] is null)
                {
                    throw Fail(code, $"{label} must be {Shape}; item {index} is null");
                }

                if (list[index].Contains('\0', StringComparison.Ordinal))
                {
                    throw Fail(code, $"{label} item {index} holds a NUL character");
                }
            }

            return ResolvedLaunchArguments.Of(list);
        }

        if (values is null)
        {
            return null;
        }

        var entries = new Dictionary<string, LaunchArgumentValue>(StringComparer.Ordinal);
        foreach (var (name, declared) in values)
        {
            if (declared is not { } value)
            {
                continue;
            }

            CheckName(code, label, name);
            switch (value.Kind)
            {
                case LaunchArgumentValueKind.Number:
                    CheckNumber(code, label, name, value.Number!.Value);
                    break;
                case LaunchArgumentValueKind.List:
                    for (var index = 0; index < value.Items!.Count; index++)
                    {
                        if (value.Items[index] is null)
                        {
                            throw Fail(
                                code,
                                $"{label}.{name} must be a string, number, boolean, or array of strings; item {index} is null"
                            );
                        }
                    }

                    break;
            }

            CheckNul(code, label, name, value);
            entries[name] = value;
        }

        return ResolvedLaunchArguments.Of(new ReadOnlyDictionary<string, LaunchArgumentValue>(entries));
    }

    /// <summary>
    /// Checks <c>app.launchArguments</c> as an <c>e2e.config.json</c> declared it: an array of strings or an object of argument name to value,
    /// with the checks of <see cref="CheckLaunchArguments(string, IReadOnlyList{string}?, IReadOnlyDictionary{string, LaunchArgumentValue?}?, string)"/>.
    /// Upstream's <c>checkLaunchArguments</c> reads its <c>unknown</c>; the JSON reader is where the port's input is untyped.
    /// </summary>
    public static ResolvedLaunchArguments? CheckLaunchArguments(
        string label,
        JsonElement? declared,
        string code = "INVALID_CONFIG"
    )
    {
        ArgumentNullException.ThrowIfNull(label);
        if (declared is not { } element)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var argument in element.EnumerateArray())
            {
                if (argument.ValueKind != JsonValueKind.String)
                {
                    throw Fail(code, $"{label} must be {Shape}; item {list.Count} is {Describe(argument)}");
                }

                list.Add(argument.GetString()!);
            }

            return CheckLaunchArguments(label, list, null, code);
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Fail(code, $"{label} must be {Shape}, got {Describe(element)}");
        }

        var entries = new Dictionary<string, LaunchArgumentValue>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            var name = property.Name;
            var value = property.Value;
            CheckName(code, label, name);
            LaunchArgumentValue converted;
            switch (value.ValueKind)
            {
                case JsonValueKind.Array:
                    var items = new List<string>();
                    foreach (var item in value.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.String)
                        {
                            throw Fail(
                                code,
                                $"{label}.{name} must be a string, number, boolean, or array of strings; item {items.Count} is {Describe(item)}"
                            );
                        }

                        items.Add(item.GetString()!);
                    }

                    converted = items;
                    break;
                case JsonValueKind.String:
                    converted = value.GetString()!;
                    break;
                case JsonValueKind.True or JsonValueKind.False:
                    converted = value.GetBoolean();
                    break;
                case JsonValueKind.Number:
                    var number = value.GetDouble();
                    CheckNumber(code, label, name, number);
                    converted = number;
                    break;
                default:
                    throw Fail(
                        code,
                        $"{label}.{name} must be a string, number, boolean, or array of strings, got {Describe(value)}"
                    );
            }

            CheckNul(code, label, name, converted);
            entries[name] = converted;
        }

        return ResolvedLaunchArguments.Of(new ReadOnlyDictionary<string, LaunchArgumentValue>(entries));
    }

    /// <summary>The values with the entries a null value left out dropped, or null for none declared. The form an engine receives.</summary>
    public static IReadOnlyDictionary<string, LaunchArgumentValue>? WithoutNulls(
        IReadOnlyDictionary<string, LaunchArgumentValue?>? values
    ) =>
        values is null
            ? null
            : new ReadOnlyDictionary<string, LaunchArgumentValue>(
                values
                    .Where(pair => pair.Value is not null)
                    .ToDictionary(pair => pair.Key, pair => pair.Value!.Value, StringComparer.Ordinal)
            );

    /// <summary>The two fields an engine reads launch arguments from (upstream's <c>engineLaunchArguments</c>): the list, or the object, never both.</summary>
    public static (
        IReadOnlyList<string>? LaunchArguments,
        IReadOnlyDictionary<string, LaunchArgumentValue>? LaunchArgumentValues
    ) EngineLaunchArguments(ResolvedLaunchArguments? launchArguments) =>
        launchArguments is null ? (null, null) : (launchArguments.List, launchArguments.Values);

    /// <summary>
    /// Launch arguments as Apple process arguments. A list stays as it is. An object becomes <c>-name value</c> for each entry, as
    /// <c>NSUserDefaults</c> reads it: <c>YES</c> or <c>NO</c> for a boolean, an old-style plist array for a list, and a quoted plist string for a
    /// value the parser would otherwise misread, such as <c>(draft)</c> or <c>-1</c>. Upstream's <c>appleUserDefaults</c>.
    /// </summary>
    public static IReadOnlyList<string> AppleUserDefaults(ResolvedLaunchArguments launchArguments)
    {
        ArgumentNullException.ThrowIfNull(launchArguments);
        if (launchArguments.List is { } list)
        {
            return list;
        }

        var arguments = new List<string>();
        foreach (var (name, value) in launchArguments.Values!)
        {
            arguments.Add("-" + name);
            arguments.Add(AppleUserDefault(value));
        }

        return arguments;
    }

    private static string AppleUserDefault(LaunchArgumentValue value) =>
        value.Kind switch
        {
            LaunchArgumentValueKind.Boolean => value.Flag == true ? "YES" : "NO",
            LaunchArgumentValueKind.List => "(" + string.Join(", ", value.Items!.Select(PlistString)) + ")",
            _ => PlistString(value.ToString()),
        };

    /// <summary>An old-style plist string: bare when its characters allow and it does not open a <c>//</c> comment, else quoted with <c>\</c> and <c>"</c> escaped.</summary>
    private static string PlistString(string item) =>
        PlistUnquoted().IsMatch(item) && !item.StartsWith("//", StringComparison.Ordinal)
            ? item
            : "\""
                + item.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)
                + "\"";

    /// <summary>What an old-style plist string may hold unquoted. <c>NSUserDefaults</c> reads a bare value that starts with <c>-</c> as the next argument's name.</summary>
    [GeneratedRegex(@"^[A-Za-z0-9_$/:.][A-Za-z0-9_$/:.-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex PlistUnquoted();

    private static void CheckName(string code, string label, string name)
    {
        if (name.Length == 0)
        {
            throw Fail(code, $"{label} has an empty argument name");
        }

        if (name.Any(character => char.IsWhiteSpace(character) || char.IsControl(character) || character == '﻿'))
        {
            throw Fail(
                code,
                $"{label} argument name {JsonQuote.Of(name)} must have no whitespace or control characters"
            );
        }

        if (name[0] == '-')
        {
            throw Fail(
                code,
                $"{label} argument name {JsonQuote.Of(name)} starts with a dash; remove the dash, the engine adds it"
            );
        }
    }

    private static void CheckNumber(string code, string label, string name, double number)
    {
        if (double.IsFinite(number) && Math.Abs(number) <= MaxSafeInteger)
        {
            return;
        }

        var described =
            double.IsNaN(number) ? "NaN"
            : double.IsFinite(number) ? "a number beyond 2^53 - 1"
            : number > 0 ? "Infinity"
            : "-Infinity";
        throw Fail(code, $"{label}.{name} must be a string, number, boolean, or array of strings, got {described}");
    }

    private static void CheckNul(string code, string label, string name, LaunchArgumentValue value)
    {
        var holds = value.Kind switch
        {
            LaunchArgumentValueKind.String => value.Text!.Contains('\0', StringComparison.Ordinal),
            LaunchArgumentValueKind.List => value.Items!.Any(item => item.Contains('\0', StringComparison.Ordinal)),
            _ => false,
        };
        if (holds)
        {
            throw Fail(code, $"{label}.{name} holds a NUL character");
        }
    }

    private static string Describe(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => "null",
            JsonValueKind.Array => "an array",
            JsonValueKind.Object => "an object",
            JsonValueKind.String => "a string",
            JsonValueKind.Number => "a number",
            _ => "a boolean",
        };

    private static Exception Fail(string code, string message) =>
        code == "INVALID_CONFIG" ? new ConfigurationException(code, message) : new TestException(code, message);
}
