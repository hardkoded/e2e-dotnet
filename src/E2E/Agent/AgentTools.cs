// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>
/// The built-in agent's tool vocabulary (upstream's <c>agent/primitives.ts</c>). <see cref="CreateGrammarTools"/> and
/// <see cref="CreateVerdictTool"/> are public, for a tool loop or an executor that keeps the built-in actions and verdicts.
/// </summary>
public static class AgentTools
{
    private static readonly JsonElement Target = Parse(
        """
        {
          "type": "object",
          "properties": {
            "role": { "type": "string", "description": "Accessible role, such as button or textbox." },
            "name": { "type": "string", "description": "Accessible name from the snapshot." },
            "testId": { "type": "string" },
            "ref": { "type": "string", "description": "Node id from any screen in this conversation that is still present, e.g. \"n42\". Prefer it to role and name." }
          }
        }
        """);

    /// <summary>Screens one scroll call may move.</summary>
    internal const int MaxScrollTimes = 20;

    /// <summary>Presses one <c>press</c> call may repeat: a selection of a word, a caret moved a few steps (upstream's <c>MAX_PRESS_TIMES</c>).</summary>
    internal const int MaxPressTimes = 20;

    /// <summary>Longest text <c>scroll_to</c> pages toward: a row label, never a paragraph.</summary>
    internal const int MaxScrollToText = 200;

    /// <summary>Ceiling on a verdict summary (upstream's <c>MAX_VERDICT_SUMMARY_CHARS</c>).</summary>
    internal const int MaxVerdictSummaryChars = 2_000;

    private const string Directions = """{ "type": "string", "enum": ["up", "down", "left", "right"] }""";

    /// <summary>Codes the model may pick when it ends an act. The runtime assigns every other code.</summary>
    internal static readonly string[] ModelErrorCodes =
    [
        "ACTION_FAILED",
        "AUTOMATION_UNSUPPORTED",
        "ASSERTION_FAILED",
        "ASSERTION_INCONCLUSIVE",
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
    internal static readonly string[] BlockableCodes =
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

    private static readonly JsonElement Done = VerdictSchema(["passed", "failed", "blocked"], ModelErrorCodes, "Why the step failed or what blocked it. Only with status failed or blocked; a passed step carries none.");

    private static readonly JsonElement JudgeDone = VerdictSchema(["passed", "failed"], ["ASSERTION_FAILED", "ASSERTION_INCONCLUSIVE"]);

    private static readonly ModelTool Observe = Tool(
        "observe",
        "Look at the screen again. Action results already include the screen, so call this only after waiting for something in progress, never right after an action.",
        Parse("""{ "type": "object", "properties": {} }"""));

    private const string ScrollToTextDescription = "The visible text of the node to reach, or a distinctive part of it, e.g. \"Row 4322\". The list is paged screen by screen until a node reading it is in view; the node need not be listed yet.";

    // scroll_to reaches a node the screen lists (scrollTo) or one it does not list yet (scrollUntil, paging a list screen by screen). One tool,
    // each form offered only with its verb (upstream's createGrammarTools). With text, the target is the list to page.
    private static ModelTool ScrollToTool(bool byNode, bool byText)
    {
        var textProperty = $$"""{ "type": "string", "minLength": 1, "maxLength": {{MaxScrollToText}}, "description": {{JsonSerializer.Serialize(ScrollToTextDescription)}} }""";
        var directionProperty = $$"""{ "type": "string", "enum": ["up", "down", "left", "right"], "description": "Which way to page for text; down by default." }""";
        if (byNode && byText)
        {
            return Tool(
                "scroll_to",
                "Scroll until a node is inside the viewport. With target alone, one listed node the screen shows above or below the fold (a button past the fold, a row far down the page). With text, a node the screen does not list yet: the list named by target (or the viewport) is paged screen by screen until a node reading that text shows, as one action however far, for a row deep in a long or windowed list. Prefer either to blind scrolling.",
                Extend(Target, $$"""{ "text": {{textProperty}}, "direction": {{directionProperty}} }"""));
        }

        return byNode
            ? Tool(
                "scroll_to",
                "Scroll until one listed node is inside the viewport: for a target the screen lists but that sits above or below what is shown (a button past the fold, a row far down a long page). Prefer it to blind scrolling whenever the node has an id.",
                Target)
            : Tool(
                "scroll_to",
                "Scroll until a node reading the given text is inside the viewport: the list named by target (or the viewport) is paged screen by screen until it shows, as one action however far, for a row deep in a long or windowed list the screen does not list yet. Prefer it to blind scrolling whenever you know what the target reads.",
                Extend(Target, $$"""{ "text": {{textProperty}}, "direction": {{directionProperty}} }""", "text"));
    }

    private static ModelTool ScrollTool(bool withTarget)
    {
        var times = $$"""{ "type": "integer", "minimum": 1, "maximum": {{MaxScrollTimes}}, "description": "How many screens to scroll in this one call, 1 to {{MaxScrollTimes}}; default 1. Use more to move far down a long list or feed." }""";
        return withTarget
            ? Tool(
                "scroll",
                "Scroll the viewport, or one scrollable node when target is given, by a screen or a few. The result reports the rows that came into or left the tree. For a row far down a long or windowed list, prefer scroll_to with the row's text: it pages until the row shows, as one action.",
                Extend(Target, $$"""{ "direction": {{Directions}}, "times": {{times}} }""", "direction"))
            : Tool(
                "scroll",
                "Scroll the viewport by a screen or a few. The result reports the rows that came into or left the tree. For a row far down a long list, prefer scroll_to with the row's text: it pages until the row shows, as one action.",
                Parse($$"""{ "type": "object", "properties": { "direction": {{Directions}}, "times": {{times}} }, "required": ["direction"] }"""));
    }

    private static readonly ModelTool Upload = Tool(
        "upload",
        "Attach one or more files to a file input node, or to a control that opens a file chooser when clicked (an upload button). " +
        "Use the project-relative paths the step gives you; when it names none, attach a sample: " +
        string.Join(", ", SampleFiles.SampleNames.Select(name => SampleFiles.SamplePrefix + name)) + ". " +
        "Any other file outside the project, a hidden one, or one that does not exist is refused.",
        Extend(Target, """{ "files": { "type": "array", "items": { "type": "string", "minLength": 1 }, "minItems": 1 } }""", "files"));

    private static readonly ModelTool Back = Tool(
        "back",
        "Go back one step in the browser history. The result shows the screen you return to.",
        Parse("""{ "type": "object", "properties": {} }"""));

    /// <summary>Upstream's <c>resize_viewport</c> tool (<c>agent/primitives.ts</c>), offered where the engine can resize its viewport.</summary>
    internal const string ResizeViewportName = "resize_viewport";

    internal const string ResizeViewportDescription = "Resize the viewport to check how the page lays out at another screen size, e.g. 1440x900 desktop, 768x1024 tablet, 390x844 phone. It stays at that size until resized again. The result shows the screen at the new size; take a screenshot to check the layout itself (overflow, overlap, clipped or hidden content).";

    private static readonly ModelTool ResizeViewport = Tool(
        ResizeViewportName,
        ResizeViewportDescription,
        Parse(
            $$"""
            {
              "type": "object",
              "properties": {
                "width": { "type": "integer", "minimum": {{Geometry.ViewportLimits.Min}}, "maximum": {{Geometry.ViewportLimits.Max}}, "description": "Viewport width in CSS pixels." },
                "height": { "type": "integer", "minimum": {{Geometry.ViewportLimits.Min}}, "maximum": {{Geometry.ViewportLimits.Max}}, "description": "Viewport height in CSS pixels." }
              },
              "required": ["width", "height"]
            }
            """));

    /// <summary>Upstream's <c>switch_tab</c> tool (<c>agent/primitives.ts</c>), offered where the engine can show another tab.</summary>
    internal const string SwitchTabName = "switch_tab";

    internal const string SwitchTabDescription = "Show another open tab. When more than one tab is open, the screen header says which tab you are on and how many there are, numbered from 1 in the order they opened. A tab the app opens stays in the background until you switch to it.";

    /// <summary>Upstream's <c>close_tab</c> tool (<c>agent/primitives.ts</c>), offered where the engine can close a tab.</summary>
    internal const string CloseTabName = "close_tab";

    internal const string CloseTabDescription = "Close an open tab, numbered as switch_tab counts them. Closing the tab on screen shows the tab that opened it, else the tab shown before it. The last open tab cannot be closed.";

    private static readonly JsonElement TabSchema = Parse(
        """
        {
          "type": "object",
          "properties": {
            "tab": { "type": "integer", "minimum": 1, "description": "The tab number, from 1 in the order the tabs opened." }
          },
          "required": ["tab"]
        }
        """);

    private static readonly ModelTool SwitchTab = Tool(SwitchTabName, SwitchTabDescription, TabSchema);

    private static readonly ModelTool CloseTab = Tool(CloseTabName, CloseTabDescription, TabSchema);

    /// <summary>Upstream's <c>dismiss_keyboard</c> tool (<c>agent/primitives.ts</c>), offered where the engine can hide its keyboard.</summary>
    internal const string DismissKeyboardName = "dismiss_keyboard";

    internal const string DismissKeyboardDescription = "Hide the on-screen keyboard when it covers what you need to reach.";

    private static readonly ModelTool DismissKeyboard = Tool(
        DismissKeyboardName,
        DismissKeyboardDescription,
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
    /// Upstream's <c>POINT_TOOLS</c> (<c>agent/primitives.ts</c>): the point verbs that take only a point, one tool each. <c>Bare</c> is the
    /// verb the engine's pointer kind unlocks, <c>Node</c> the node verb the point falls back to, <c>Does</c> what the tool does up to the
    /// clause about a bare point, <c>ById</c> the node verb the last-resort advice names. A point tool is offered with either verb.
    /// </summary>
    private static readonly (string Tool, ExecutorVerb Bare, ExecutorVerb Node, string Does, string ById)[] PointTools =
    [
        (
            "tap_at",
            ExecutorVerb.TapAt,
            ExecutorVerb.Tap,
            "Tap a point in the latest screenshot, given as pixel coordinates in that image (x from the left edge, y from the top edge). Aim for the center of the target. The bare point is tapped",
            "tap it by id"),
        (
            "hover_at",
            ExecutorVerb.HoverAt,
            ExecutorVerb.Hover,
            "Move the pointer to a point in the latest screenshot without clicking, for a hover target the screen does not list (a drawn control, a region of an image). The bare point is hovered",
            "hover it by id"),
    ];

    private static readonly ModelTool[] Verbs =
    [
        Tool("tap", "Activate a control.", Target),
        Tool("double_tap", "Double-click a control. Use it only when a tap does nothing, such as a label that opens an editor on a double-click.", Target),
        Tool("fill", "Replace the text in a field.", WithValue(Target, "value")),
        Tool("fill_secret", "Type a declared secret into an editable field. A password secret needs a password field. Pass the secret name, never the value.", WithValue(Target, "secret")),
        Tool(
            "press",
            "Press a key such as Enter, Escape, or Tab. Omit role and name to press the focused control. " +
            "Shift with an arrow key extends the selection in a field one step per press, and the screen then shows the selected text as selection=\"...\".",
            Extend(
                WithValue(Target, "key"),
                $$"""{ "times": { "type": "integer", "minimum": 1, "maximum": {{MaxPressTimes}}, "description": "Press the key this many times in this one call, 1 to {{MaxPressTimes}}; default 1. Use more to extend a selection or move a caret several steps at once." } }""",
                "key")),
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

    /// <summary>
    /// The verbs of <see cref="ExecutorTargetInfo.Verbs"/> that offer each tool, any one of them (the <c>verbs.has(...)</c> at each tool's site in
    /// upstream's <c>createGrammarTools</c> and <c>createPointTools</c>): a tool whose verbs the engine's declarations do not unlock is absent
    /// from the toolset. <c>fill</c> and <c>press</c> are also offered by the keyboard alone, without a target. The port's <c>clear</c> and
    /// <c>uncheck</c> have no upstream tool; they follow their own action kind and the kinds of <c>check</c>.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, ExecutorVerb[]> ToolVerbs = new Dictionary<string, ExecutorVerb[]>(StringComparer.Ordinal)
    {
        ["tap"] = [ExecutorVerb.Tap],
        ["double_tap"] = [ExecutorVerb.DoubleTap],
        ["long_press"] = [ExecutorVerb.LongPress],
        ["right_click"] = [ExecutorVerb.SecondaryTap],
        ["hover"] = [ExecutorVerb.Hover],
        ["drag"] = [ExecutorVerb.Drag],
        ["fill"] = [ExecutorVerb.Type, ExecutorVerb.TypeText],
        ["fill_secret"] = [ExecutorVerb.TypeSecret],
        ["press"] = [ExecutorVerb.Press, ExecutorVerb.PressKey],
        ["select"] = [ExecutorVerb.Select],
        ["check"] = [ExecutorVerb.Check],
        ["uncheck"] = [ExecutorVerb.Check],
        ["clear"] = [ExecutorVerb.Clear],
        ["tap_at"] = [ExecutorVerb.TapAt, ExecutorVerb.Tap],
        ["hover_at"] = [ExecutorVerb.HoverAt, ExecutorVerb.Hover],
        ["drag_at"] = [ExecutorVerb.DragAt, ExecutorVerb.Drag],
        ["type_at"] = [ExecutorVerb.Type, ExecutorVerb.TypeText],
        ["press_at"] = [ExecutorVerb.Press, ExecutorVerb.PressKey],
        ["select_at"] = [ExecutorVerb.Select],
    };

    /// <summary>True when the verbs the engine's declarations unlock offer the tool; a tool outside <see cref="ToolVerbs"/> is not shaped by them.</summary>
    internal static bool OffersVerb(string tool, IReadOnlySet<ExecutorVerb> verbs) =>
        !ToolVerbs.TryGetValue(tool, out var offering) || offering.Any(verbs.Contains);

    // A node verb as the engine's declarations shape it. With a keyboard, a fill with no target types into whatever has focus (upstream's type
    // with typeText), and an engine that declares no fill takes that form alone: no target. The same for press.
    private static ModelTool NodeTool(ModelTool verb, IReadOnlySet<ExecutorVerb> verbs)
    {
        var typeText = verbs.Contains(ExecutorVerb.TypeText);
        return verb.Name switch
        {
            "fill" when typeText && !verbs.Contains(ExecutorVerb.Type) => WithoutTarget(
                FillFocused,
                "Type a plain-text value into whatever has focus. After tap_at on a field the screen does not list, call fill: the value is inserted at the caret of the focused field; set replace to clear it first. Several fields can be typed in one turn."),
            "fill" when typeText => FillFocused,
            "press" when verbs.Contains(ExecutorVerb.PressKey) && !verbs.Contains(ExecutorVerb.Press) => WithoutTarget(
                verb,
                "Press a key such as Enter, Escape, or Tab on whatever has focus. Shift with an arrow key extends the selection in a field one step per press, and the screen then shows the selected text as selection=\"...\"."),
            _ => verb,
        };
    }

    private static ModelTool WithoutTarget(ModelTool tool, string description)
    {
        var parameters = JsonNode.Parse(tool.Parameters.GetRawText())!.AsObject();
        var properties = parameters["properties"]!.AsObject();
        foreach (var name in Target.GetProperty("properties").EnumerateObject().Select(property => property.Name))
        {
            properties.Remove(name);
        }

        return new ModelTool { Name = tool.Name, Description = description, Parameters = JsonSerializer.SerializeToElement(parameters) };
    }

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
                Parameters = Closed(Extend(Target, new JsonObject { ["modifiers"] = ModifiersSchema() }.ToJsonString())),
            }
            : verb;

    private static readonly ModelTool Navigate = Tool("navigate", "Open a path or URL.", Parse("""{ "type": "object", "properties": { "url": { "type": "string" } }, "required": ["url"] }"""));

    /// <summary>
    /// Upstream's <c>VERDICT_RULES</c>: the guidance for <see cref="VerdictTool"/>. The tool loop appends it to the instructions of every
    /// executor it builds, so an executor outside the loop gives it to its own model beside the tool. The port's tool is <c>done</c>
    /// where upstream's is <c>complete_step</c>, and takes <c>code</c> where upstream's takes <c>errorCode</c>.
    /// </summary>
    public const string VerdictRules =
        "Finish with done when the step's goal is achieved, or you are certain it cannot be. " +
        "done status is passed, failed, or blocked. " +
        "blocked means credentials, the environment, or test setup prevented a verdict, and requires a code.";

    /// <summary>
    /// Upstream's <c>createVerdictTool</c>: the <c>done</c> tool with the closed blocked-code policy, and the verdict it collected. Make a
    /// fresh one for each step: the first accepted verdict is final.
    /// </summary>
    /// <param name="kind">
    /// The step's kind. On <see cref="ExecutorStepKind.Act"/>, <c>ASSERTION_INCONCLUSIVE</c> is dropped from a failed verdict the same
    /// way, since only an assertion can be inconclusive. Omit it to keep every code a failed verdict sends.
    /// </param>
    public static VerdictTool CreateVerdictTool(ExecutorStepKind? kind = null) =>
        new(Tool(VerdictTool.Name, "End the step with a verdict. blocked requires a code naming what blocked you.", Done), kind);

    /// <summary>
    /// Upstream's <c>createGrammarTools</c>: the built-in agent's action tools for the verbs the step's engine supports, each answering what
    /// changed on screen. Offer them from <see cref="ToolLoopExecutorOptions.Tools"/> beside tools of your own, and begin the executor's
    /// <see cref="ToolLoopExecutorOptions.System"/> with <see cref="DefaultAgent.BaseRules"/>. Every call runs through the step's own actions, so it is
    /// authorized, counted, recorded, and replayed from the cache. The context must be one the runtime opened for a step, as the built-in
    /// agent's is. The screen presenter and the failure causes stay internal, as upstream's do.
    /// </summary>
    public static IReadOnlyList<ToolLoopTool> CreateGrammarTools(StepExecutorContext context, GrammarToolsOptions? options = null) =>
        GrammarTools(context, null, options);

    // Upstream's grammarTools: createGrammarTools for the package's own callers, which also offer the project's tools.
    internal static IReadOnlyList<ToolLoopTool> GrammarTools(StepExecutorContext context, IReadOnlyDictionary<string, AgentTool>? tools, GrammarToolsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var host = context.Host
            ?? throw new InvalidOperationException("The grammar tools drive the context of a step the runtime opened; they cannot run on a context built by hand.");
        var asserting = (options?.Kind ?? context.Step.Kind) == ExecutorStepKind.Assert;
        // An assertion offers the project tools that read the app, and none that change it (upstream's projectTools).
        var project = tools is null
            ? new Dictionary<string, AgentTool>()
            : tools.Where(pair => pair.Value.AppliesTo(context.Target.Platform) && (!asserting || !pair.Value.Mutates)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var offered = project.Select(pair => Project(pair.Key, pair.Value)).ToList();
        var guard = options?.Guard;
        var act = new ActTools(host.Scope, host.Feed, DefaultAgent.PresenterFor(context));
        return GrammarToolDefinitions(host.Scope.EngineCapabilities, offered, host.Scope.ActionKinds, host.Scope.Session.PointerActionKinds, host.Scope.Session.CanDismissKeyboard)
            .Where(definition => !asserting || AssertionOffers(definition.Name, project, context.Step.Vision))
            .Select(definition => new ToolLoopTool(
                definition,
                async (call, token) =>
                {
                    // Upstream's schemaTool closes the schema of every grammar tool; a project tool's schema is the author's own.
                    if (!project.ContainsKey(definition.Name) && SchemaTool(definition, call) is { } refusal)
                    {
                        return new ToolLoopResult(refusal, Failed: true, ErrorText: true);
                    }

                    ToolOutcome? outcome = null;
                    async Task<string> Body()
                    {
                        outcome = await act.ExecuteAsync(call, context.Actions, host.Accounting, project, token, context).ConfigureAwait(false);
                        return outcome.Content;
                    }

                    var content = guard is null ? await Body().ConfigureAwait(false) : await guard(Body).ConfigureAwait(false);

                    // A guard that answered in the body's place (the step is concluding) leaves no outcome: a failed call. Text the guard
                    // changed on a body that ran keeps the body's result and its screenshot.
                    return outcome is null ? new ToolLoopResult(content, Failed: true, ErrorText: true) : new ToolLoopResult(content, !outcome.Succeeded, outcome.Pixels, outcome.ErrorText);
                }))
            .ToList();
    }

    // An assertion only looks: the read-only tools, the project tools that read, and no screenshot when it asked for the tree alone.
    private static bool AssertionOffers(string name, IReadOnlyDictionary<string, AgentTool> project, VisionMode? vision) =>
        project.ContainsKey(name) || (ActionNames.ReadOnlyGrammar.ContainsKey(name) && (name != "screenshot" || vision != VisionMode.Off));

    /// <summary>
    /// Upstream's <c>schemaTool</c>: the schema of a grammar tool and of the verdict tool is closed, so a field it does not declare fails validation and the call never runs.
    /// Returns the refusal the model reads in the call's place (the AI SDK's <c>Invalid input for tool</c> error over a zod <c>unrecognized_keys</c>
    /// issue), or null when the call declares nothing but what the tool does. Only undeclared fields are checked: the tools read their own values.
    /// </summary>
    internal static string? SchemaTool(ModelTool definition, ModelToolCall call)
    {
        if (call.Arguments.ValueKind != JsonValueKind.Object
            || !definition.Parameters.TryGetProperty("properties", out var declared)
            || declared.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var keys = call.Arguments.EnumerateObject().Select(property => property.Name).Where(name => !declared.TryGetProperty(name, out _)).ToList();
        if (keys.Count == 0)
        {
            return null;
        }

        var issue = JsonSerializer.Serialize(new object[]
        {
            new Dictionary<string, object>
            {
                ["code"] = "unrecognized_keys",
                ["keys"] = keys,
                ["path"] = Array.Empty<string>(),
                ["message"] = "Unrecognized key" + (keys.Count > 1 ? "s" : "") + ": " + string.Join(", ", keys.Select(key => "\"" + key + "\"")),
            },
        });
        return "failed: Invalid input for tool " + definition.Name + ": Type validation failed: Value: " + call.Arguments.GetRawText() + ".\nError message: " + issue;
    }

    /// <summary>
    /// The act tools an engine with these capabilities can honor, without the verdict tool. Scroll and back tools need their capability,
    /// and the pointer verbs (<c>long_press</c>, <c>right_click</c>, <c>hover</c>, <c>drag</c>) the locator action kind the engine declares for them.
    /// </summary>
    internal static IReadOnlyList<ModelTool> GrammarToolDefinitions(EngineCapabilities capabilities, IReadOnlyList<ModelTool>? project = null, IReadOnlyCollection<string>? actionKinds = null, IReadOnlyCollection<string>? pointerKinds = null, bool dismissKeyboard = false) =>
        GrammarToolDefinitions(ActContext.VerbsOf(capabilities, actionKinds, pointerKinds ?? [], dismissKeyboard), capabilities.HasFlag(EngineCapabilities.TapModifiers), actionKinds, project);

    /// <summary>The same over the verbs the engine's declarations unlock (upstream's <c>verbs</c> of <c>createGrammarTools</c>): each tool is shaped by them.</summary>
    internal static IReadOnlyList<ModelTool> GrammarToolDefinitions(IReadOnlySet<ExecutorVerb> verbs, bool tapModifiers = false, IReadOnlyCollection<string>? actionKinds = null, IReadOnlyList<ModelTool>? project = null)
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

        var keyboard = verbs.Contains(ExecutorVerb.TypeText) || verbs.Contains(ExecutorVerb.PressKey);
        var grammar = Verbs.Where(verb => OffersVerb(verb.Name, verbs)).Select(verb => NodeTool(verb, verbs));
        tools.AddRange(tapModifiers ? grammar.Concat(pointer).Select(HoldingModifiers) : [.. grammar, .. pointer]);
        if (verbs.Contains(ExecutorVerb.Upload))
        {
            tools.Add(Upload);
        }

        if (verbs.Contains(ExecutorVerb.ScrollTo) || verbs.Contains(ExecutorVerb.ScrollUntil))
        {
            tools.Add(ScrollToTool(verbs.Contains(ExecutorVerb.ScrollTo), verbs.Contains(ExecutorVerb.ScrollUntil)));
        }

        // Node-targeted scrolling rides a tap; without it only the viewport scrolls.
        if (verbs.Contains(ExecutorVerb.Scroll))
        {
            tools.Add(ScrollTool(verbs.Contains(ExecutorVerb.Tap)));
        }

        if (verbs.Contains(ExecutorVerb.Navigate))
        {
            tools.Add(Navigate);
        }

        if (verbs.Contains(ExecutorVerb.Back))
        {
            tools.Add(Back);
        }

        if (verbs.Contains(ExecutorVerb.Resize))
        {
            tools.Add(ResizeViewport);
        }

        if (verbs.Contains(ExecutorVerb.SwitchTab))
        {
            tools.Add(SwitchTab);
        }

        if (verbs.Contains(ExecutorVerb.CloseTab))
        {
            tools.Add(CloseTab);
        }

        // Upstream offers dismiss_keyboard with the dismissKeyboard verb, which an engine with a keyboard that can be hidden lists.
        if (verbs.Contains(ExecutorVerb.DismissKeyboard))
        {
            tools.Add(DismissKeyboard);
        }

        tools.Add(Screenshot);
        tools.AddRange(CreatePointTools(verbs, keyboard));
        if (project is not null)
        {
            tools.AddRange(project);
        }

        return tools;
    }

    /// <summary>
    /// Upstream's <c>createPointTools</c>: the point-addressed fallback verbs, each taking a point in the latest screenshot for a target the
    /// tree does not list. <paramref name="verbs"/> are the verbs the engine's declarations unlock (<see cref="ActContext.VerbsOf"/>): a point
    /// verb is offered with its own pointer kind or with its node verb, and without the pointer kind says its bare point cannot be taken.
    /// <c>type_at</c> and <c>press_at</c> are offered with the keyboard too, <c>select_at</c> with <c>select</c>. With a
    /// <paramref name="keyboard"/>, <c>type_at</c> and <c>press_at</c> on a point on nothing listed tap it to focus it and go through the keyboard.
    /// </summary>
    private static List<ModelTool> CreatePointTools(IReadOnlySet<ExecutorVerb> verbs, bool keyboard)
    {
        var tools = new List<ModelTool>();
        foreach (var (tool, bareVerb, nodeVerb, does, byId) in PointTools)
        {
            var bare = verbs.Contains(bareVerb);
            if (!bare && !verbs.Contains(nodeVerb))
            {
                continue;
            }

            tools.Add(Tool(
                tool,
                does + (bare ? "." : ", which this engine cannot do: the point must land on a listed control.") + " Last resort: when the screen lists the target, " + byId + ".",
                PointXY));
        }

        var bareDrag = verbs.Contains(ExecutorVerb.DragAt);
        if (bareDrag || verbs.Contains(ExecutorVerb.Drag))
        {
            tools.Add(Tool(
                "drag_at",
                "Drag from one point in the latest screenshot to another, for a drag the screen does not list: a shape on a canvas, a pin on a map, a card dropped on an empty spot. Both points are pixel coordinates in that image (x from the left edge, y from the top edge). Start on the center of what you drag. The bare points are dragged" +
                (bareDrag ? "." : ", which this engine cannot do: the points must land on two different listed controls.") +
                " Last resort: when the screen lists what you drag and where it goes, use drag by id.",
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

        if (verbs.Contains(ExecutorVerb.Type) || keyboard)
        {
            tools.Add(Tool(
                "type_at",
                keyboard
                    ? "Type a plain-text value into the field at a point in the latest screenshot, for a field the screen does not list. A listed input under the point is filled by id, replacing its value; anything else is tapped to focus it and typed into through the keyboard, inserting at the caret unless replace is set. Last resort: when the screen lists the field, type by id."
                    : "Type a plain-text value into the input at a point in the latest screenshot, replacing its value. Aim at the field itself; a point on nothing listed fails. Last resort: when the screen lists the field, type by id.",
                keyboard
                    ? Extend(PointXY, $$"""{ "value": { "type": "string" }, "replace": { "type": "boolean", "description": "{{ReplaceDescription}}" } }""", "x", "y", "value")
                    : Extend(PointXY, """{ "value": { "type": "string" } }""", "x", "y", "value")));
        }

        if (verbs.Contains(ExecutorVerb.Press) || verbs.Contains(ExecutorVerb.PressKey))
        {
            tools.Add(Tool(
                "press_at",
                "Send one key (e.g. \"Enter\", \"Escape\", \"Tab\") to the control at a point in the latest screenshot, for a control the screen does not list." +
                (keyboard ? " A listed control under the point gets the key by id; anything else is tapped to focus it and the key goes through the keyboard." : "") +
                " Last resort: when the screen lists the control, press by id.",
                Extend(PointXY, """{ "key": { "type": "string", "minLength": 1, "maxLength": 64 } }""", "x", "y", "key")));
        }

        if (verbs.Contains(ExecutorVerb.Select))
        {
            tools.Add(Tool(
                "select_at",
                "Pick one option, by its visible label, from the select-like control at a point in the latest screenshot. Last resort: when the screen lists the select, use select by id.",
                Extend(PointXY, """{ "value": { "type": "string", "minLength": 1 } }""", "x", "y", "value")));
        }

        return tools;
    }

    /// <summary>
    /// The tools the harness adds beside the grammar, each where it adds them: <c>report_finding</c> under <c>e2e explore</c>, and
    /// <c>locate</c>, <c>test_lines</c>, <c>start_recording</c>, and <c>stop_recording</c> under an e2e mcp session (the session
    /// catalog, <c>SessionCatalog.Create</c>; <c>run_code</c> is reserved, and the port has no tool of that name). A project tool named like one would be
    /// replaced there, so the name is refused. Upstream's <c>HARNESS_TOOL_NAMES</c>; the port's other harness tool, <c>done</c>,
    /// is reserved with the grammar's names.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> HarnessToolNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["complete_step"] = "every agent step",
        [ExploreExecutor.FindingToolName] = "e2e explore",
        ["locate"] = "an e2e mcp session",
        ["run_code"] = "an e2e mcp session",
        ["test_lines"] = "an e2e mcp session",
        ["start_recording"] = "an e2e mcp session",
        ["stop_recording"] = "an e2e mcp session",
    };

    /// <summary>True when <paramref name="name"/> is a tool the agent offers itself, whatever the engine.</summary>
    internal static bool IsReserved(string name) =>
        string.Equals(name, VerdictTool.Name, StringComparison.Ordinal) ||
        GrammarToolDefinitions(EngineCapabilities.Scroll | EngineCapabilities.History | EngineCapabilities.Resize | EngineCapabilities.SwitchTab | EngineCapabilities.CloseTab | EngineCapabilities.Upload | EngineCapabilities.Keyboard, null, EngineActionKinds.Locator, ["tap", "hover", "dragTo"], dismissKeyboard: true).Any(tool => string.Equals(tool.Name, name, StringComparison.Ordinal));

    /// <summary>The model's view of a project tool: its name, description, and input schema.</summary>
    internal static ModelTool Project(string name, AgentTool tool) =>
        Open(name, tool.Description, JsonSerializer.SerializeToElement(tool.InputSchema));

    /// <summary>The extract tools, with <c>data</c> described by the JSON schema of the requested type.</summary>
    internal static IReadOnlyList<ModelTool> ExtractFor(JsonNode schema)
    {
        var parameters = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject { ["data"] = schema.DeepClone() },
            ["required"] = new JsonArray("data"),
        };
        return
        [
            Open("extract", "Return the data read from the screen.", JsonSerializer.SerializeToElement(parameters)),
            Open("done", "Use this only when the data is not on screen.", JudgeDone),
        ];
    }

    // Upstream's schemaTool: the schema of every grammar tool and of the verdict tool is closed, so a provider receives
    // additionalProperties: false and refuses a field the tool does not declare.
    private static ModelTool Tool(string name, string description, JsonElement parameters) => Open(name, description, Closed(parameters));

    // A tool whose schema is not the grammar's to close: a project tool's is the author's own, and the extract tools' are upstream's own.
    private static ModelTool Open(string name, string description, JsonElement parameters)
    {
        return new ModelTool { Name = name, Description = description, Parameters = parameters };
    }

    private static JsonElement Closed(JsonElement schema)
    {
        if (JsonNode.Parse(schema.GetRawText()) is not JsonObject root || root.ContainsKey("additionalProperties"))
        {
            return schema;
        }

        root["additionalProperties"] = false;
        return JsonSerializer.SerializeToElement(root);
    }

    private static JsonElement VerdictSchema(string[] statuses, string[] codes, string? codeDescription = null)
    {
        var codeProperty = codeDescription is null
            ? $$"""{ "type": "string", "enum": {{JsonSerializer.Serialize(codes)}} }"""
            : $$"""{ "type": "string", "enum": {{JsonSerializer.Serialize(codes)}}, "description": {{JsonSerializer.Serialize(codeDescription)}} }""";
        return Parse(
            $$"""
            {
              "type": "object",
              "properties": {
                "status": { "type": "string", "enum": {{JsonSerializer.Serialize(statuses)}} },
                "summary": { "type": "string", "minLength": 1, "maxLength": {{MaxVerdictSummaryChars}}, "description": "One to three sentences for the next step: what you did, what the screen shows now, and any value it will need (a name or id you created, a message you saw). No page narration; under 400 characters." },
                "code": {{codeProperty}}
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

/// <summary>What <see cref="AgentTools.CreateGrammarTools"/> runs a tool body under (upstream's <c>GrammarToolOptions</c>).</summary>
public sealed class GrammarToolsOptions
{
    /// <summary>
    /// Wraps every tool body. By default the body runs as it is, so a hard stop of the step (budget, timeout, cancel) propagates out of the
    /// tool, where the tool loop ends the step. Pass <c>body =&gt; helpers.GuardAsync(body)</c> to run each under the loop's policy instead,
    /// or your own wrapper. Upstream's is generic over the body's result; the port's bodies answer the text the model reads.
    /// </summary>
    public Func<Func<Task<string>>, Task<string>>? Guard { get; init; }

    /// <summary>
    /// The kind of step to offer tools for, when it is not the context's own: an assertion offers only the tools that read the app (upstream's
    /// <c>grammarTools</c> filters on <c>context.step.kind</c>; a session that lists its tools once for steps of both kinds builds the contexts with
    /// <c>Object.create</c>, which the port has no counterpart of). .NET-only.
    /// </summary>
    internal ExecutorStepKind? Kind { get; init; }
}

/// <summary>
/// Upstream's <c>VerdictTool</c>: the conclusion tool, the policy that accepts or refuses the verdict a call carries, and the verdict it
/// collected. Made by <see cref="AgentTools.CreateVerdictTool"/>; the tool loop adds one to every vocabulary, so an executor of its own that
/// drives a model outside the loop is the only caller of <see cref="Execute"/>.
/// </summary>
public sealed class VerdictTool
{
    private readonly Lock _gate = new();
    private StepVerdict? _verdict;

    private readonly ExecutorStepKind? _kind;

    internal VerdictTool(ModelTool tool, ExecutorStepKind? kind = null)
    {
        Tool = tool;
        _kind = kind;
    }

    /// <summary>The name the model calls to end a step.</summary>
    public const string Name = "done";

    /// <summary>The <c>done</c> tool; offer it beside the grammar tools.</summary>
    public ModelTool Tool { get; }

    /// <summary>The verdict once the model called the tool with an accepted one. Upstream's <c>verdict()</c>.</summary>
    public StepVerdict? Verdict => _verdict;

    /// <summary>True once a verdict landed. Upstream's <c>concluded()</c>.</summary>
    public bool Concluded => _verdict is not null;

    /// <summary>
    /// Runs one call of the tool and answers what the model reads. The first accepted verdict is final. A code sent beside <c>passed</c> is
    /// dropped and the answer says so, never refused.
    /// </summary>
    public ToolLoopResult Execute(ModelToolCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        var outcome = Decide(call);
        lock (_gate)
        {
            if (_verdict is not null)
            {
                return new ToolLoopResult("The step already concluded.");
            }

            if (!outcome.Done)
            {
                return new ToolLoopResult(outcome.Content, Failed: true, ErrorText: outcome.ErrorText);
            }

            _verdict = new StepVerdict(
            outcome.Status switch
            {
                "passed" => StepVerdictStatus.Passed,
                "blocked" => StepVerdictStatus.Blocked,
                _ => StepVerdictStatus.Failed,
            },
            outcome.Summary ?? "",
            outcome.Code);
        }

        return new ToolLoopResult(outcome.Content);
    }

    // The model picks only from a closed set of codes, and the runtime assigns the rest. Stateless: the loop reads the outcome itself.
    internal ToolOutcome Decide(ModelToolCall call)
    {
        // The verdict tool's schema is closed too (upstream's schemaTool): a field it does not declare refuses the call.
        if (AgentTools.SchemaTool(Tool, call) is { } refusal)
        {
            return new ToolOutcome { Content = refusal, Succeeded = false, ErrorText = true };
        }

        var status = Args.String(call.Arguments, "status");
        if (status is not ("passed" or "failed" or "blocked"))
        {
            return ToolOutcome.Reject("done status must be passed, failed, or blocked.");
        }

        // Upstream's schema (summary: z.string().min(1).max(2000)) turns an empty or oversized summary back to the model.
        var summary = Args.String(call.Arguments, "summary");
        if (string.IsNullOrWhiteSpace(summary) || summary.Length > AgentTools.MaxVerdictSummaryChars)
        {
            return ToolOutcome.Reject(
                "done summary must be a non-empty string of at most " + AgentTools.MaxVerdictSummaryChars.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " characters: what you did and what the screen shows now.");
        }

        var code = Args.String(call.Arguments, "code");
        // Upstream's schema makes errorCode an enum, so the note names only a code from the closed set; any other code is ignored without being echoed.
        var assertionOnly = _kind == ExecutorStepKind.Act && string.Equals(code, "ASSERTION_INCONCLUSIVE", StringComparison.Ordinal) && !string.Equals(status, "passed", StringComparison.Ordinal);
        var dropped = string.Equals(status, "passed", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(code)
            ? (AgentTools.ModelErrorCodes.Contains(code) ? code : "")
            : assertionOnly ? code : null;
        if (string.Equals(status, "passed", StringComparison.Ordinal) || assertionOnly || !AgentTools.ModelErrorCodes.Contains(code))
        {
            code = null;
        }

        if (string.Equals(status, "blocked", StringComparison.Ordinal) && !AgentTools.BlockableCodes.Contains(code))
        {
            return ToolOutcome.Reject(
                "a blocked verdict requires a code naming what blocked you (one of " + string.Join(", ", AgentTools.BlockableCodes) + "). " +
                "If the application itself misbehaved, use status failed instead.");
        }

        return ToolOutcome.Finish(status, summary, code, dropped, assertionOnly);
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
