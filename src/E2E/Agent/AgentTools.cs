// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E;

internal static class AgentTools
{
    private static readonly JsonElement Target = Parse(
        """
        {
          "type": "object",
          "properties": {
            "role": { "type": "string", "description": "Accessible role, such as button or textbox." },
            "name": { "type": "string", "description": "Accessible name from the snapshot." },
            "testId": { "type": "string" },
            "ref": { "type": "string", "description": "ref from the current snapshot. Prefer role and name." }
          }
        }
        """);

    /// <summary>Screens one scroll call may move.</summary>
    public const int MaxScrollTimes = 20;

    /// <summary>Longest text <c>scroll_to</c> pages toward: a row label, never a paragraph.</summary>
    public const int MaxScrollToText = 200;

    /// <summary>Ceiling on a verdict summary (upstream's <c>MAX_VERDICT_SUMMARY_CHARS</c>).</summary>
    public const int MaxVerdictSummaryChars = 2_000;

    private const string Directions = """{ "type": "string", "enum": ["up", "down", "left", "right"] }""";

    /// <summary>Codes the model may pick when it ends an act. The runtime assigns every other code.</summary>
    public static readonly string[] ModelErrorCodes =
    [
        "ACTION_FAILED",
        "AUTOMATION_UNSUPPORTED",
        "ASSERTION_FAILED",
        "AUTHENTICATION_FAILED",
        "AUTH_CREDENTIAL_UNAVAILABLE",
        "AUTH_CREDENTIAL_INVALID",
        "SECRET_UNAVAILABLE",
        "ENVIRONMENT_UNAVAILABLE",
        "SEED_DATA_MISSING",
        "TEST_SETUP_FAILED",
        "APP_UNREACHABLE",
        "APP_NOT_OPEN",
        "POLICY_DENIED",
    ];

    /// <summary>The model codes a blocked verdict accepts: credentials, environment, test setup, or automation.</summary>
    public static readonly string[] BlockableCodes =
    [
        "AUTOMATION_UNSUPPORTED",
        "AUTH_CREDENTIAL_UNAVAILABLE",
        "AUTH_CREDENTIAL_INVALID",
        "SECRET_UNAVAILABLE",
        "ENVIRONMENT_UNAVAILABLE",
        "SEED_DATA_MISSING",
        "TEST_SETUP_FAILED",
        "APP_UNREACHABLE",
        "APP_NOT_OPEN",
        "POLICY_DENIED",
    ];

    private static readonly JsonElement Done = VerdictSchema(["passed", "failed", "blocked"], ModelErrorCodes);

    private static readonly JsonElement JudgeDone = VerdictSchema(["passed", "failed"], ["ASSERTION_FAILED", "ASSERTION_INCONCLUSIVE"]);

    private static readonly ModelTool Observe = Tool(
        "observe",
        "Look at the screen again. Action results already include the screen, so call this only after waiting for something in progress, never right after an action.",
        Parse("""{ "type": "object", "properties": {} }"""));

    private static readonly ModelTool ScrollTo = Tool(
        "scroll_to",
        "Scroll until a node is inside the viewport. With a target alone, one listed control above or below the fold. " +
        "With text, a node the screen does not list yet: the list named by the target (or the viewport) is paged screen by screen " +
        "until a node reading that text shows, as one action however far. Prefer either to blind scrolling.",
        Extend(
            Target,
            $$"""
            {
              "text": { "type": "string", "maxLength": {{MaxScrollToText}}, "description": "The visible text of the node to reach, or a distinctive part of it. With text, the target is the list to page." },
              "direction": {{Directions}}
            }
            """));

    private static readonly ModelTool Scroll = Tool(
        "scroll",
        "Scroll the viewport, or one scrollable control when a target is given, by a screen or a few. " +
        "The result shows the screen after the scroll. For a row far down a long list, prefer scroll_to with the row's text.",
        Extend(
            Target,
            $$"""
            {
              "direction": {{Directions}},
              "times": { "type": "integer", "minimum": 1, "maximum": {{MaxScrollTimes}}, "description": "How many screens to scroll in this one call, 1 to {{MaxScrollTimes}}; default 1." }
            }
            """,
            "direction"));

    private static readonly ModelTool Upload = Tool(
        "upload",
        "Attach one or more files to a file input node, or to a control that opens a file chooser when clicked (an upload button). " +
        "Paths are relative to the project root, e.g. \"fixtures/photo.png\"; use the paths the step gives you. " +
        "A file outside the project, a hidden one, or one that does not exist is refused.",
        Extend(Target, """{ "files": { "type": "array", "items": { "type": "string", "minLength": 1 }, "minItems": 1 } }""", "files"));

    private static readonly ModelTool Back = Tool(
        "back",
        "Go back one step in the browser history. The result shows the screen you return to.",
        Parse("""{ "type": "object", "properties": {} }"""));

    /// <summary>
    /// The pixel fallback of the grammar (upstream's <c>screenshot</c> tool, offered while pixels may reach the model): a masked viewport
    /// screenshot, and from then on one with every action result.
    /// </summary>
    internal const string ScreenshotName = "screenshot";

    private static readonly ModelTool Screenshot = Tool(
        ScreenshotName,
        "Attach a screenshot of the current viewport. Use it when the screen lists too little to act on (a canvas, a map, an image, a game, a system sheet) or contradicts what you expect. " +
        "From then on every action result carries a fresh screenshot too, so you can see what each action did.",
        Parse("""{ "type": "object", "properties": {} }"""));

    private static readonly JsonElement PointXY = Parse(
        """
        {
          "type": "object",
          "properties": {
            "x": { "type": "number", "description": "x in the latest screenshot, pixels from the left edge" },
            "y": { "type": "number", "description": "y in the latest screenshot, pixels from the top edge" }
          },
          "required": ["x", "y"]
        }
        """);

    /// <summary>
    /// Upstream's <c>POINT_TOOLS</c> (<c>agent/primitives.ts</c>): the point verbs that take only a point, one tool each. <c>Kind</c> is the
    /// engine pointer kind that takes the bare point, <c>Does</c> what the tool does up to the clause about a bare point, <c>ById</c> the
    /// node verb the last-resort advice names. The node hover of #861 is not offered here, so <c>hover_at</c> needs its pointer kind.
    /// </summary>
    private static readonly (string Tool, string Kind, string Does, string ById)[] PointTools =
    [
        (
            "tap_at",
            "tap",
            "Tap a point in the latest screenshot, given as pixel coordinates in that image (x from the left edge, y from the top edge). Aim for the center of the target. The bare point is tapped",
            "tap it by id"),
        (
            "hover_at",
            "hover",
            "Move the pointer to a point in the latest screenshot without clicking, for a hover target the screen does not list (a drawn control, a region of an image). The bare point is hovered",
            "hover it by id"),
    ];

    private static readonly ModelTool[] Verbs =
    [
        Tool("tap", "Activate a control.", Target),
        Tool("double_tap", "Double-click a control. Use it only when a tap does nothing, such as a label that opens an editor on a double-click.", Target),
        Tool("fill", "Replace the text in a field.", WithValue(Target, "value")),
        Tool("fill_secret", "Type a declared secret into an editable field. A password secret needs a password field. Pass the secret name, never the value.", WithValue(Target, "secret")),
        Tool("press", "Press a key such as Enter, Escape, or Tab. Omit role and name to press the focused control.", WithValue(Target, "key")),
        Tool("select", "Choose an option in a combobox.", WithValue(Target, "value")),
        Tool("check", "Check a checkbox.", Target),
        Tool("uncheck", "Uncheck a checkbox.", Target),
        Tool("clear", "Clear a field.", Target),
    ];

    private static readonly ModelTool LongPress = Tool(
        "long_press",
        "Press one node and hold: only for a control with a long-press menu or action. Anything else takes tap.",
        Target);

    private static readonly ModelTool RightClick = Tool(
        "right_click",
        "Right-click one node to open its context menu, and only for that. The menu items it opens, and every button, link, or field, take tap. " +
        "When nothing opens, the app has no context menu there: look for a menu button or another path instead.",
        Target);

    private static readonly ModelTool Hover = Tool(
        "hover",
        "Move the pointer over one node without clicking: opens what reacts to the pointer resting on it, such as a menu, a flyout, a tooltip, or a control revealed on mouse-over. " +
        "The result reports what appeared. In a nested menu, hover each level in turn and then tap the item while it is open.",
        Target);

    // The drop target is a second node, named like the first.
    private static readonly ModelTool Drag = Tool(
        "drag",
        "Drag one node and drop it on another: reorder a list, move a card to a column, drop an item into a zone. The result reports what moved.",
        Extend(Target, """{ "to": { "type": "object", "description": "The drop target, named like the node dragged.", "properties": { "role": { "type": "string" }, "name": { "type": "string" }, "testId": { "type": "string" }, "ref": { "type": "string" } } } }""", "to"));

    /// <summary>
    /// The pointer verbs the grammar adds when the engine declares their locator action kind (<see cref="IEngineSession.ActionKinds"/>), by
    /// upstream's kind name. Upstream's <c>VERBS_BY_ACTION</c>.
    /// </summary>
    internal static readonly IReadOnlyList<(string Kind, ExecutorVerb Verb)> PointerVerbs =
    [
        ("longPress", ExecutorVerb.LongPress),
        ("secondaryTap", ExecutorVerb.SecondaryTap),
        ("hover", ExecutorVerb.Hover),
        ("dragTo", ExecutorVerb.Drag),
    ];

    /// <summary>The tool of each pointer verb, with the locator action kind the engine declares for it.</summary>
    internal static readonly IReadOnlyDictionary<string, string> PointerToolKinds = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["long_press"] = "longPress",
        ["right_click"] = "secondaryTap",
        ["hover"] = "hover",
        ["drag"] = "dragTo",
    };

    /// <summary>The <c>modifiers</c> property of a tap-family tool: the keys held while the pointer clicks.</summary>
    internal static JsonObject ModifiersSchema() => new()
    {
        ["type"] = "array",
        ["items"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. KeyModifiers.Names.Select(name => (JsonNode)name)]) },
        ["description"] = "Keys held while the pointer clicks. A Shift-click extends a selection, a Control-click (Meta on macOS) toggles one item",
    };

    internal const string ReplaceDescription = "Select all and delete before typing, for a field that visibly holds text you must remove; default false. Leave it off for an empty field.";

    // With a keyboard, a fill with no target types into whatever has focus (upstream's type with typeText): how a field the tree does not
    // list gets its text after a tap_at gave it focus.
    private static readonly ModelTool FillFocused = Tool(
        "fill",
        "Type a plain-text value into one input node, or into whatever has focus when target is omitted. With target the value replaces the node's value. " +
        "After tap_at on a field the screen does not list, call fill with NO target (not the document or a container id): the value is inserted at the caret of the focused field; set replace to clear it first. " +
        "Several fields can be typed in one turn.",
        Extend(Target, $$"""{ "value": { "type": "string" }, "replace": { "type": "boolean", "description": "Without target: {{ReplaceDescription}}" } }""", "value"));

    // The tap family names the keys it holds for the click, on an engine that declares them.
    private static ModelTool HoldingModifiers(ModelTool verb) =>
        verb.Name is "tap" or "double_tap" or "right_click"
            ? new ModelTool
            {
                Name = verb.Name,
                Description = verb.Description + " Pass modifiers to hold keys for the click.",
                Parameters = Extend(Target, new JsonObject { ["modifiers"] = ModifiersSchema() }.ToJsonString()),
            }
            : verb;

    private static readonly ModelTool Navigate = Tool("navigate", "Open a path or URL.", Parse("""{ "type": "object", "properties": { "url": { "type": "string" } }, "required": ["url"] }"""));

    /// <summary>The verdict tool every loop offers, whatever its vocabulary, with the policy that accepts or refuses a verdict.</summary>
    internal static VerdictTool CreateVerdictTool() =>
        new(Tool(VerdictTool.Name, "End the step with a verdict. blocked requires a code naming what blocked you.", Done));

    /// <summary>
    /// The act tools an engine with these capabilities can honor, without the verdict tool. Scroll and back tools need their capability,
    /// and the pointer verbs (<c>long_press</c>, <c>right_click</c>, <c>hover</c>, <c>drag</c>) the locator action kind the engine declares for them.
    /// </summary>
    public static IReadOnlyList<ModelTool> CreateGrammarTools(EngineCapabilities capabilities, IReadOnlyList<ModelTool>? project = null, IReadOnlyCollection<string>? actionKinds = null, IReadOnlyCollection<string>? pointerKinds = null)
    {
        var tools = new List<ModelTool> { Observe };
        var pointer = new List<ModelTool>();
        foreach (var (name, kind) in PointerToolKinds)
        {
            if (actionKinds?.Contains(kind) == true)
            {
                pointer.Add(name switch { "long_press" => LongPress, "right_click" => RightClick, "hover" => Hover, _ => Drag });
            }
        }

        var keyboard = capabilities.HasFlag(EngineCapabilities.Keyboard);
        IEnumerable<ModelTool> grammar = keyboard ? Verbs.Select(verb => verb.Name == "fill" ? FillFocused : verb) : Verbs;
        tools.AddRange(capabilities.HasFlag(EngineCapabilities.TapModifiers) ? grammar.Concat(pointer).Select(HoldingModifiers) : [.. grammar, .. pointer]);
        if (capabilities.HasFlag(EngineCapabilities.Upload))
        {
            tools.Add(Upload);
        }

        if (capabilities.HasFlag(EngineCapabilities.Scroll))
        {
            tools.Add(ScrollTo);
            tools.Add(Scroll);
        }

        tools.Add(Navigate);
        if (capabilities.HasFlag(EngineCapabilities.History))
        {
            tools.Add(Back);
        }

        tools.Add(Screenshot);
        tools.AddRange(CreatePointTools(pointerKinds ?? [], keyboard));
        if (project is not null)
        {
            tools.AddRange(project);
        }

        return tools;
    }

    /// <summary>
    /// Upstream's <c>createPointTools</c>: the point-addressed fallback verbs, each taking a point in the latest screenshot for a target the
    /// tree does not list. <paramref name="pointerKinds"/> are the pointer kinds the engine takes at a bare point. A point tool whose node verb
    /// exists is offered even without the kind, and says its bare point cannot be taken. With a <paramref name="keyboard"/>, <c>type_at</c> and
    /// <c>press_at</c> on a point on nothing listed tap it to focus it and go through the keyboard.
    /// </summary>
    private static List<ModelTool> CreatePointTools(IReadOnlyCollection<string> pointerKinds, bool keyboard)
    {
        var tools = new List<ModelTool>();
        foreach (var (tool, kind, does, byId) in PointTools)
        {
            var bare = pointerKinds.Contains(kind);
            if (!bare && kind != "tap")
            {
                continue;
            }

            tools.Add(Tool(
                tool,
                does + (bare ? "." : ", which this engine cannot do: the point must land on a listed control.") + " Last resort: when the screen lists the target, " + byId + ".",
                PointXY));
        }

        if (pointerKinds.Contains("dragTo"))
        {
            tools.Add(Tool(
                "drag_at",
                "Drag from one point in the latest screenshot to another, for a drag the screen does not list: a shape on a canvas, a pin on a map, a card dropped on an empty spot. Both points are pixel coordinates in that image (x from the left edge, y from the top edge). Start on the center of what you drag. The bare points are dragged. Last resort: when the screen lists what you drag and where it goes, use drag by id.",
                Extend(
                    PointXY,
                    """
                    {
                      "to_x": { "type": "number", "description": "x of the drop point in the latest screenshot, pixels from the left edge" },
                      "to_y": { "type": "number", "description": "y of the drop point in the latest screenshot, pixels from the top edge" }
                    }
                    """,
                    "x",
                    "y",
                    "to_x",
                    "to_y")));
        }

        tools.Add(Tool(
            "type_at",
            keyboard
                ? "Type a plain-text value into the field at a point in the latest screenshot, for a field the screen does not list. A listed input under the point is filled by id, replacing its value; anything else is tapped to focus it and typed into through the keyboard, inserting at the caret unless replace is set. Last resort: when the screen lists the field, type by id."
                : "Type a plain-text value into the input at a point in the latest screenshot, replacing its value. Aim at the field itself; a point on nothing listed fails. Last resort: when the screen lists the field, type by id.",
            keyboard
                ? Extend(PointXY, $$"""{ "value": { "type": "string" }, "replace": { "type": "boolean", "description": "{{ReplaceDescription}}" } }""", "x", "y", "value")
                : Extend(PointXY, """{ "value": { "type": "string" } }""", "x", "y", "value")));
        tools.Add(Tool(
            "press_at",
            "Send one key (e.g. \"Enter\", \"Escape\", \"Tab\") to the control at a point in the latest screenshot, for a control the screen does not list." +
            (keyboard ? " A listed control under the point gets the key by id; anything else is tapped to focus it and the key goes through the keyboard." : "") +
            " Last resort: when the screen lists the control, press by id.",
            Extend(PointXY, """{ "key": { "type": "string", "minLength": 1, "maxLength": 64 } }""", "x", "y", "key")));
        tools.Add(Tool(
            "select_at",
            "Pick one option, by its visible label, from the select-like control at a point in the latest screenshot. Last resort: when the screen lists the select, use select by id.",
            Extend(PointXY, """{ "value": { "type": "string", "minLength": 1 } }""", "x", "y", "value")));
        return tools;
    }

    /// <summary>
    /// The tools the harness adds beside the grammar, each where it adds them: <c>report_finding</c> under <c>e2e explore</c>. A
    /// project tool named like one would be replaced there, so the name is refused. Upstream's <c>HARNESS_TOOL_NAMES</c>; the
    /// port's other harness tool, <c>done</c>, is reserved with the grammar's names.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> HarnessToolNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [ExploreExecutor.FindingToolName] = "e2e explore",
    };

    /// <summary>True when <paramref name="name"/> is a tool the agent offers itself, whatever the engine.</summary>
    public static bool IsReserved(string name) =>
        string.Equals(name, VerdictTool.Name, StringComparison.Ordinal) ||
        CreateGrammarTools(EngineCapabilities.Scroll | EngineCapabilities.History | EngineCapabilities.Upload, null, EngineActionKinds.Locator, ["tap", "hover", "dragTo"]).Any(tool => string.Equals(tool.Name, name, StringComparison.Ordinal));

    /// <summary>The model's view of a project tool: its name, description, and input schema.</summary>
    public static ModelTool Project(string name, AgentTool tool) =>
        Tool(name, tool.Description, JsonSerializer.SerializeToElement(tool.InputSchema));

    /// <summary>The extract tools, with <c>data</c> described by the JSON schema of the requested type.</summary>
    public static IReadOnlyList<ModelTool> ExtractFor(JsonNode schema)
    {
        var parameters = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject { ["data"] = schema.DeepClone() },
            ["required"] = new JsonArray("data"),
        };
        return
        [
            Tool("extract", "Return the data read from the screen.", JsonSerializer.SerializeToElement(parameters)),
            Tool("done", "Use this only when the data is not on screen.", JudgeDone),
        ];
    }

    private static ModelTool Tool(string name, string description, JsonElement parameters)
    {
        return new ModelTool { Name = name, Description = description, Parameters = parameters };
    }

    private static JsonElement VerdictSchema(string[] statuses, string[] codes)
    {
        return Parse(
            $$"""
            {
              "type": "object",
              "properties": {
                "status": { "type": "string", "enum": {{JsonSerializer.Serialize(statuses)}} },
                "summary": { "type": "string", "minLength": 1, "maxLength": {{MaxVerdictSummaryChars}}, "description": "One to three sentences for the next step: what you did, what the screen shows now, and any value it will need (a name or id you created, a message you saw). No page narration; under 400 characters." },
                "code": { "type": "string", "enum": {{JsonSerializer.Serialize(codes)}} }
              },
              "required": ["status", "summary"]
            }
            """);
    }

    private static JsonElement WithValue(JsonElement target, string property)
    {
        using var document = JsonDocument.Parse(target.GetRawText());
        var properties = new Dictionary<string, object?>();
        foreach (var item in document.RootElement.GetProperty("properties").EnumerateObject())
        {
            properties[item.Name] = JsonSerializer.Deserialize<object>(item.Value.GetRawText());
        }

        properties[property] = new Dictionary<string, string> { ["type"] = "string" };
        return JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = new[] { property },
        });
    }

    private static JsonElement Extend(JsonElement target, string properties, params string[] required)
    {
        var merged = new Dictionary<string, object?>();
        foreach (var item in target.GetProperty("properties").EnumerateObject())
        {
            merged[item.Name] = item.Value.Clone();
        }

        foreach (var item in Parse(properties).EnumerateObject())
        {
            merged[item.Name] = item.Value.Clone();
        }

        var schema = new Dictionary<string, object?> { ["type"] = "object", ["properties"] = merged };
        if (required.Length > 0)
        {
            schema["required"] = required;
        }

        return JsonSerializer.SerializeToElement(schema);
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

/// <summary>The conclusion tool: its definition, and the policy that accepts or refuses the verdict a call carries.</summary>
internal sealed class VerdictTool(ModelTool tool)
{
    /// <summary>The name the model calls to end a step.</summary>
    public const string Name = "done";

    /// <summary>The <c>done</c> tool; offer it beside the grammar tools.</summary>
    public ModelTool Tool { get; } = tool;

    // The model picks only from a closed set of codes, and the runtime assigns the rest.
    public ToolOutcome Execute(ModelToolCall call)
    {
        var status = Args.String(call.Arguments, "status");
        if (status is not ("passed" or "failed" or "blocked"))
        {
            return ToolOutcome.Fail("done status must be passed, failed, or blocked.");
        }

        // Upstream's schema (summary: z.string().min(1).max(2000)) turns an empty or oversized summary back to the model.
        var summary = Args.String(call.Arguments, "summary");
        if (string.IsNullOrWhiteSpace(summary) || summary.Length > AgentTools.MaxVerdictSummaryChars)
        {
            return ToolOutcome.Fail(
                "done summary must be a non-empty string of at most " + AgentTools.MaxVerdictSummaryChars.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " characters: what you did and what the screen shows now.");
        }

        var code = Args.String(call.Arguments, "code");
        if (string.Equals(status, "passed", StringComparison.Ordinal) || !AgentTools.ModelErrorCodes.Contains(code))
        {
            code = null;
        }

        if (string.Equals(status, "blocked", StringComparison.Ordinal) && !AgentTools.BlockableCodes.Contains(code))
        {
            return ToolOutcome.Fail(
                "a blocked verdict requires a code naming what blocked you (one of " + string.Join(", ", AgentTools.BlockableCodes) + "). " +
                "If the application itself misbehaved, use status failed instead.");
        }

        return ToolOutcome.Finish(status, summary, code);
    }
}

internal static class Args
{
    public static string? String(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }

    /// <summary>A finite number argument, or null when it is absent or not a number.</summary>
    public static double? Number(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDouble(out var number) && double.IsFinite(number))
            {
                return number;
            }
        }

        return null;
    }

    /// <summary>A boolean argument, or null when it is absent or not a boolean.</summary>
    public static bool? Bool(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return property.Value.GetBoolean();
            }
        }

        return null;
    }

    public static int? Int(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var number))
            {
                return number;
            }

            if (property.Value.ValueKind == JsonValueKind.String && int.TryParse(property.Value.GetString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out number))
            {
                return number;
            }
        }

        return null;
    }
}
