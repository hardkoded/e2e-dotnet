// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.WinApp;

/// <summary>
/// Agent-side tools for Windows targets. Everything a model plans through beyond the grammar verbs the engine already unlocks
/// (tap, type, scroll, screenshot) lives here: bringing another app forward, and answering a dialog. Each tool is scoped to the
/// <c>windows</c> platform, so a mixed suite never offers it on a browser. The mobile engine's <c>MobileTools</c>, for Windows.
/// </summary>
public static class WinAppTools
{
    /// <summary>
    /// Builds the tool pack for one or more Windows engines, keyed by tool name, for <c>AgentOptions.Tools</c>. A worker runs one
    /// attempt at a time, so at execution the pack dispatches to the session whose attempt is running. Mutating tools are recorded
    /// as replay gaps by the trace cache.
    /// </summary>
    public static IReadOnlyDictionary<string, AgentTool> Create(params WinAppEngine[] engines)
    {
        ArgumentNullException.ThrowIfNull(engines);
        if (engines.Length == 0)
        {
            throw new ArgumentException("winAppTools needs at least one engine", nameof(engines));
        }

        var platforms = engines.Select(engine => engine.Platform).Distinct().ToList();

        WindowSession Active()
        {
            var running = engines.SelectMany(engine => engine.ActiveSessions).ToList();
            return running.Count == 1
                ? running[0]
                : throw new EngineException(
                    EngineErrorCodes.InvalidState,
                    running.Count == 0
                        ? "no Windows app attempt is running; window tools act inside a test attempt only"
                        : "several Windows app attempts are running in one worker; tools cannot pick a window",
                    retryable: false);
        }

        var tools = new (string Name, AgentTool Tool)[]
        {
            ("open_app", new AgentTool(
                "Open an app: the app under test, or a running app by its process name or window title (e.g. \"Notepad\"), bringing it to the foreground. Set relaunch to restart the app under test fresh. Takes an app, not a URL.",
                Schema(new JsonObject { ["app"] = Primitive("string", minLength: 1), ["relaunch"] = Primitive("boolean") }, "app"),
                true,
                async (input, token) =>
                {
                    var app = RequireString(input, "app");
                    await Active().OpenAppAsync(app, new OpenAppOptions { Relaunch = OptionalBoolean(input, "relaunch") }, token).ConfigureAwait(false);
                    return $"Opened {app}.";
                },
                platforms)),
            ("alert", new AgentTool(
                "Accept or dismiss the open dialog. Use it when the dialog's buttons are not listed on screen; a listed button tapped by id is the same tap and replays from the trace cache, this tool does not.",
                Schema(new JsonObject { ["action"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("accept", "dismiss") } }, "action"),
                true,
                async (input, token) =>
                {
                    var action = RequireString(input, "action");
                    if (action is not ("accept" or "dismiss"))
                    {
                        throw new TestException("INVALID_ARGUMENT", "alert takes an action of accept or dismiss");
                    }

                    await Active().Window.AlertAsync(action == "accept" ? AlertAction.Accept : AlertAction.Dismiss, token).ConfigureAwait(false);
                    return $"Alert {action}ed.";
                },
                platforms)),
        };
        return tools.ToDictionary(pair => pair.Name, pair => pair.Tool);
    }

    private static JsonObject Schema(JsonObject properties, params string[] required) => new()
    {
        ["type"] = "object",
        ["properties"] = properties,
        ["required"] = new JsonArray([.. required.Select(name => (JsonNode?)JsonValue.Create(name))]),
        ["additionalProperties"] = false,
    };

    private static JsonObject Primitive(string type, int? minLength = null)
    {
        var schema = new JsonObject { ["type"] = type };
        if (minLength is { } length)
        {
            schema["minLength"] = length;
        }

        return schema;
    }

    private static string RequireString(JsonObject input, string name) =>
        input[name] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0
            ? text
            : throw new TestException("INVALID_ARGUMENT", $"{name} must be a non-empty string");

    private static bool OptionalBoolean(JsonObject input, string name) =>
        input[name] is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;
}
