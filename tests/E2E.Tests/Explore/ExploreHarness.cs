// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.Agent;
using E2E.Tests.AgentAct;

namespace E2E.Tests.Explore;

/// <summary>What a scripted explorer step sees: how many model turns it has had, and what the last tool answered.</summary>
internal sealed record LoopCall(int Turn, ModelRequest Request)
{
    public IReadOnlyList<string> ToolNames => [.. Request.Tools.Select(tool => tool.Name)];

    /// <summary>The first message of the step: the goal, the screen, and the params.</summary>
    public string Prompt => Request.Messages[0].Content ?? "";

    public string? LastToolResult => Request.Messages.LastOrDefault(message => message.Role == "tool")?.Content;
}

/// <summary>
/// One scripted model serving both surfaces, as upstream's <c>installExploreModel</c> does: the planner's structured call and the
/// explorer's tool loop. A request that offers <c>tap</c> is a turn of the explorer's loop. Anything else is the planner's call.
/// </summary>
internal sealed class ExploreModel
{
    public ExploreModel(Func<string, PlanAnswer> plan, Func<LoopCall, ModelResponse> loop)
    {
        Model = new ScriptedModel(request =>
        {
            if (request.Tools.Any(tool => tool.Name == "tap"))
            {
                return loop(new LoopCall(request.Messages.Count(message => message.Role == "assistant") + 1, request));
            }

            var answer = plan(request.Messages[0].Content ?? "");
            return ModelResponses.Call("extract", new { data = new { decision = answer.Decision == PlanKind.Step ? "step" : "finish", title = answer.Title, instruction = answer.Instruction, summary = answer.Summary } });
        });
    }

    public ScriptedModel Model { get; }

    /// <summary>The planner's calls.</summary>
    public IEnumerable<ModelRequest> PlannerCalls => Model.Requests.Where(request => request.Tools.All(tool => tool.Name != "tap"));

    /// <summary>The explorer's turns.</summary>
    public IEnumerable<ModelRequest> LoopCalls => Model.Requests.Where(request => request.Tools.Any(tool => tool.Name == "tap"));

    public static PlanAnswer Step(string title, string instruction) => new(PlanKind.Step, title, instruction, "");

    public static PlanAnswer Finish(string summary) => new(PlanKind.Finish, "", "", summary);

    public static ModelResponse Done(string status, string summary, string? code = null) => ModelResponses.Done(status, summary, code);

    public static ModelResponse ReportFinding(object input) => ModelResponses.Call(ExploreExecutor.FindingToolName, input);

    /// <summary>The finding the scripted explorer reports on the home page.</summary>
    public static object CounterFinding(string? title = null, string kind = "issue", int severity = 3) => new
    {
        title = title ?? "Counter starts at 0 with no label of what it counts",
        kind,
        severity,
        expected = "The counter says what it counts",
        actual = "output \"Counter\" reads 0",
        reproduction = new[] { "Open the home page", "Look at the counter next to Increment" },
    };
}

internal static class ExploreHarness
{
    public const string Url = "https://app.test";

    public static string TempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "e2e-explore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>The config of a project with one web target, and the extra top-level keys <paramref name="more"/> names.</summary>
    public static E2EConfig Config(string root, string? more = null) =>
        E2EConfig.Parse("{ \"targets\": [{ \"name\": \"web\", \"platform\": \"web\", \"app\": { \"url\": \"" + Url + "\" } }]" + (more is null ? "" : ", " + more) + " }", root);

    public static IEngine Engine(DocumentWorld? world = null) => new ScreenshotEngine(world ?? CounterPage.World(), _ => ScreenshotEngine.Png());

    /// <summary>The options of a run against the counter page whose default agent is <paramref name="model"/>.</summary>
    public static ExploreOptions Options(
        string root,
        IAgentModel? model,
        List<string> notices,
        string goal = "Explore the home page and find bugs",
        int maxSteps = 2,
        AgentOptions? agent = null,
        IReadOnlyDictionary<string, AgentOptions>? agents = null,
        E2EConfig? config = null,
        IEngine? engine = null,
        IReadOnlyDictionary<string, UserCredential>? credentials = null,
        string? agentName = null,
        string? target = null,
        RecordingMode? trace = null,
        RecordingMode? video = null,
        bool aiTrace = false) =>
        new()
        {
            Cwd = root,
            RawConfig = config ?? Config(root, "\"actionTimeout\": 10000"),
            Agents = agents ?? new Dictionary<string, AgentOptions> { ["default"] = agent ?? new AgentOptions { Model = model } },
            Engine = engine ?? Engine(),
            Goal = goal,
            MaxSteps = maxSteps,
            Timeout = TimeSpan.FromMinutes(3),
            Notice = notices.Add,
            Credentials = credentials,
            Agent = agentName,
            Target = target,
            Trace = trace,
            Video = video,
            AiTrace = aiTrace,
        };

    /// <summary>Every file the run kept under the project's results directory.</summary>
    public static IEnumerable<string> Files(string root) =>
        Directory.Exists(Path.Combine(root, ".e2e")) ? Directory.EnumerateFiles(Path.Combine(root, ".e2e"), "*", SearchOption.AllDirectories) : [];

    /// <summary>The system prompts and messages every request carried, as one text.</summary>
    public static string Everything(IEnumerable<ModelRequest> requests) =>
        string.Join('\n', requests.Select(request => request.System + "\n" + string.Join('\n', request.Messages.Select(message => message.Content))));
}
