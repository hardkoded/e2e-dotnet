// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json.Nodes;

namespace E2E;

/// <summary>What <see cref="ExploreExecutor.ExplorerAgent"/> builds the explorer from.</summary>
/// <param name="State">The record whose goal the explorer is told.</param>
/// <param name="AgentName">The name of the agent the exploration runs as, which a notice names.</param>
/// <param name="Entry">The agent as configured. Null is the built-in agent with no options.</param>
/// <param name="Resolved">The same entry resolved, whose models a replaced custom executor's own ones are read from.</param>
/// <param name="Notice">Told when a custom executor is replaced.</param>
internal sealed record ExplorerOptions(ExploreState State, string AgentName, AgentOptions? Entry, ResolvedAgent Resolved, Action<string> Notice);

/// <summary>
/// The explorer: the built-in agent with exploration guidance appended to the project's and one tool added to the project's
/// vocabulary, <c>report_finding</c>. Nothing else changes: budgets, loop guards, wind-down, secrets, origin policy, and the
/// transcript are the harness's, and each exploration step runs as one ordinary <c>ActAsync</c> step.
/// </summary>
public static class ExploreExecutor
{
    /// <summary>The name of the tool the explorer reports a defect with.</summary>
    public const string FindingToolName = "report_finding";

    /// <summary>
    /// Appended to the project's own guidance; the goal travels as the test title (<c>Test: &lt;goal&gt;</c>). Upstream's text, with the port's tool
    /// names: <c>done</c> ends a step where upstream calls <c>complete_step</c>, and <c>fill_secret</c> fills a password where
    /// upstream calls <c>type_secret</c>.
    /// </summary>
    private const string ExploreRules =
        "Exploration mode: this run has no scripted test. Each step is an exploration charter the planner wrote toward the goal on the Test: line.\n" +
        "- Think like a curious first-time user hunting for bugs: exercise the flow the charter names end to end, with realistic inputs and edge cases. Interact, do not just look: fill forms with obviously made-up test data and submit them, save and come back to check what was kept, sign in with made-up credentials when none are configured, and when a list has several items act on one that is not the first and check that the right one changed.\n" +
        "- On every screen, sanity-check beyond \"it renders\": totals equal the sum of their parts and quantities multiply; counts match the items listed; nothing is negative that cannot be; dates are plausible and in order, not an epoch default; copy has no template tokens, placeholders, or misspellings; every control does what its name says; what a screen claims happened (saved, added, removed) is true on the next screen; a password is never shown as you type it.\n" +
        "- Report every defect with " + FindingToolName + " the moment the evidence is on screen, one call per distinct defect: what you expected, what the screen shows, and the actions that reach it. Do not save findings for the conclusion: a defect that appears only in a summary is lost, since the report reads the tool, not the prose. Findings listed under \"reportedFindings\" in the step parameters are already recorded: never report them again and spend no actions re-confirming them.\n" +
        "- Accounts listed under \"credentials\" in the step parameters are yours to sign in with: type the username as text and fill the password with fill_secret by the name of its password secret. Never type a password as text, and never ask for one. If no fill_secret tool is offered, this engine cannot fill a password securely: skip signing in, note it in the step summary, and explore what is reachable without it.\n" +
        "- Report defects, not wishes. A defect is something the screen breaks or contradicts: a wrong value, a dead control, a navigation that lands wrong, a claim that turns out false, leaked template text, a misspelling. A missing feature, a design choice, a form the browser refuses to submit while a required field is empty, or something you merely expected is not a defect unless the screen or the goal promised it. A failed tool call, a refused action, or a limit of this harness is not a product defect either.\n" +
        "- Stay inside the application under test. Do not destroy data that existed before this run (deleting accounts, wiping lists) unless the goal asks for it; creating and editing your own data is fine.\n" +
        "- When the charter is covered, or nothing new is reachable within it, call done. \"passed\" means you carried the charter out, however many defects you reported on the way: reported findings never make a step fail. \"failed\" is only for a charter the application would not let you carry out at all: a flow that cannot be completed, a page that cannot be reached. Keep to the charter: a step is one flow, not the whole app. The summary is read by the planner of the next step: say what was covered, what was found, and what looks worth exploring next.";

    private const int MinTitleChars = 4;
    private const int MaxTitleChars = 200;
    private const int MaxDetailChars = 2_000;
    private const int MaxReproductionSteps = 20;
    private const int MaxReproductionChars = 500;

    /// <summary>
    /// The agent the exploration runs as: the project's own options, its guidance and tools included, with the exploration rules
    /// added. The goal reaches every prompt as the test's title (<c>Test: &lt;goal&gt;</c>), not through the context. A custom executor has no readable vocabulary and is replaced, with a notice, keeping the models it
    /// brought. The finding tool joins once the entry is resolved (<see cref="WithFindingTool"/>): its name is reserved, so a
    /// project tool can never take it.
    /// </summary>
    internal static AgentOptions ExplorerAgent(ExplorerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var entry = options.Entry ?? new AgentOptions();
        var model = entry.Model;
        var judge = entry.Judge;
        if (entry.Executor is { } executor)
        {
            options.Notice(
                "agents." + options.AgentName + " is a custom executor (\"" + executor.Name + "\"); explore runs the built-in agent instead"
                + (executor.Model is null ? "" : ", with the model that executor brought"));
            model = options.Resolved.Model;
            judge = options.Resolved.Judge;
        }

        return new AgentOptions
        {
            Model = model,
            Judge = judge,
            System = string.Join("\n\n", new[] { entry.System, ExploreRules }.Where(part => !string.IsNullOrWhiteSpace(part))),
            // The goal reaches every prompt as the test's title (`Test: <goal>`).
            Context = entry.Context,
            MaxSteps = entry.MaxSteps,
            MaxModelCalls = entry.MaxModelCalls,
            JudgmentTimeout = entry.JudgmentTimeout,
            ProviderOptions = entry.ProviderOptions,
            Tools = entry.Tools,
        };
    }

    /// <summary>The explorer with the finding tool beside the project's tools.</summary>
    internal static AgentOptions WithFindingTool(AgentOptions explorer, ExploreState state)
    {
        ArgumentNullException.ThrowIfNull(explorer);
        var tools = new Dictionary<string, AgentTool>(explorer.Tools ?? new Dictionary<string, AgentTool>(), StringComparer.Ordinal)
        {
            [FindingToolName] = CreateFindingTool(state),
        };
        return new AgentOptions
        {
            Model = explorer.Model,
            Judge = explorer.Judge,
            System = explorer.System,
            Context = explorer.Context,
            MaxSteps = explorer.MaxSteps,
            MaxModelCalls = explorer.MaxModelCalls,
            JudgmentTimeout = explorer.JudgmentTimeout,
            ProviderOptions = explorer.ProviderOptions,
            Tools = tools,
        };
    }

    /// <summary>
    /// The finding tool. Read-only: it observes the screen for the location and the evidence pixels, records the finding against
    /// the step in progress, keeps the pixels as a screenshot of the step, and answers the model with the finding's number so it
    /// is not reported twice.
    /// </summary>
    private static AgentTool CreateFindingTool(ExploreState state)
    {
        // Numbered as the calls arrive, before any await: a model may report two findings in one turn, so the finding's own index
        // is not known until its screenshot is saved.
        var reported = 0;
        return new AgentTool(
            "Report one product defect you have evidence of on the current screen: what you expected, what the screen shows, and how to reach it. Call it the moment the evidence is visible, once per distinct defect. Not for tool errors or refused actions.",
            FindingSchema(),
            mutates: false,
            async (input, context, cancellationToken) =>
            {
                var report = ReadFinding(input);
                var label = "finding-" + Interlocked.Increment(ref reported).ToString(CultureInfo.InvariantCulture);
                var observation = await TryAsync(() => context.ObserveAsync(new ExecutorObserveOptions { Pixels = true }, cancellationToken), cancellationToken).ConfigureAwait(false);

                // Evidence is worth keeping, never worth failing the finding for. It is saved first, so the finding is announced
                // complete, evidence included.
                var artifactId = observation?.Pixels is { } pixels
                    ? await TryAsync(() => context.AttachScreenshotAsync(pixels, label, cancellationToken), cancellationToken).ConfigureAwait(false)
                    : null;
                var finding = state.AddFinding(report with { Path = observation?.Path, ArtifactId = artifactId });
                return "Finding " + (finding.Index + 1).ToString(CultureInfo.InvariantCulture) + " recorded (" + (finding.Kind == ReportExploreFindingKind.Issue ? "issue" : "warning")
                    + ", severity " + finding.Severity.ToString(CultureInfo.InvariantCulture) + "): " + finding.Title + ". Do not report it again; continue the charter or conclude the step.";
            })
        {
            IsHarness = true,
        };
    }

    // Upstream's .catch(() => undefined): the screen and the file system may refuse, and the finding stands without them.
    // A cancel is not a refusal: it is never swallowed, and it surfaces as a cancel.
    private static async Task<T?> TryAsync<T>(Func<Task<T>> body, CancellationToken cancellationToken)
    {
        try
        {
            return await body().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is E2EException or IOException or UnauthorizedAccessException)
        {
            // A refusal that came with a cancel surfaces as the cancel.
            cancellationToken.ThrowIfCancellationRequested();
            return default;
        }
    }

    // A whole number written as 3.0 is 3, as upstream's literal check reads it. Anything else is 0, which the range check rejects.
    private static int ReadSeverity(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return 0;
        }

        // A value built in code holds its CLR type and converts to nothing else, so a whole int is read as one.
        if (value.TryGetValue<int>(out var whole))
        {
            return whole;
        }

        return value.TryGetValue<double>(out var number) && number is >= 1 and <= 5 && number == Math.Floor(number) ? (int)number : 0;
    }

    private static JsonObject FindingSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["title"] = new JsonObject
            {
                ["type"] = "string",
                ["minLength"] = MinTitleChars,
                ["maxLength"] = MaxTitleChars,
                ["description"] = "A specific headline for the defect under 80 characters, e.g. \"Checkout total shows $0.00 with two items in the cart\".",
            },
            ["kind"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray("issue", "warning"),
                ["description"] = "issue = a defect a user would hit: a broken flow, wrong data, a dead control, a navigation that lands wrong. warning = cosmetic or polish; blocks no task.",
            },
            ["severity"] = new JsonObject
            {
                ["type"] = "integer",
                ["enum"] = new JsonArray(1, 2, 3, 4, 5),
                ["description"] = "5: a core journey is impossible, data is lost, or money or security is wrong. 4: a core feature is broken but a workaround exists, or wrong money or quantity values are shown. 3: a secondary feature is broken, or wrong non-monetary content. 2: a cosmetic or layout defect that blocks nothing. 1: a trivial polish item (usually a warning).",
            },
            ["expected"] = new JsonObject
            {
                ["type"] = "string",
                ["minLength"] = 1,
                ["maxLength"] = MaxDetailChars,
                ["description"] = "What a user would expect here, in one sentence.",
            },
            ["actual"] = new JsonObject
            {
                ["type"] = "string",
                ["minLength"] = 1,
                ["maxLength"] = MaxDetailChars,
                ["description"] = "What the screen shows instead, in one sentence, quoting the text on screen.",
            },
            ["reproduction"] = new JsonObject
            {
                ["type"] = "array",
                ["minItems"] = 1,
                ["maxItems"] = MaxReproductionSteps,
                ["items"] = new JsonObject { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = MaxReproductionChars },
                ["description"] = "The actions that reach the defect from the start of the app, one short imperative per entry, e.g. \"Open the cart\".",
            },
        },
        ["required"] = new JsonArray("title", "kind", "severity", "expected", "actual", "reproduction"),
        ["additionalProperties"] = false,
    };

    // The AI SDK checks a tool's input against its schema before the tool runs; the port's loop does not, so the tool does.
    private static FindingInput ReadFinding(JsonObject input)
    {
        var issues = new List<string>();
        var title = Text(input, "title", MinTitleChars, MaxTitleChars, issues);
        var expected = Text(input, "expected", 1, MaxDetailChars, issues);
        var actual = Text(input, "actual", 1, MaxDetailChars, issues);
        var kind = (input["kind"] as JsonValue)?.TryGetValue<string>(out var kindText) == true ? kindText : null;
        if (kind is not ("issue" or "warning"))
        {
            issues.Add("kind: must be issue or warning");
        }

        var severity = ReadSeverity(input["severity"]);
        if (severity is < 1 or > 5)
        {
            issues.Add("severity: must be 1, 2, 3, 4, or 5");
        }

        var reproduction = new List<string>();
        if (input["reproduction"] is not JsonArray steps || steps.Count is < 1 or > MaxReproductionSteps)
        {
            issues.Add("reproduction: must be 1 to " + MaxReproductionSteps.ToString(CultureInfo.InvariantCulture) + " entries");
        }
        else
        {
            foreach (var step in steps)
            {
                var text = (step as JsonValue)?.TryGetValue<string>(out var entry) == true ? entry : null;
                if (text is null || text.Length is < 1 or > MaxReproductionChars)
                {
                    issues.Add("reproduction: every entry must be 1 to " + MaxReproductionChars.ToString(CultureInfo.InvariantCulture) + " characters");
                    break;
                }

                reproduction.Add(text);
            }
        }

        if (issues.Count > 0)
        {
            throw new TestException("INVALID_ARGUMENT", FindingToolName + " input is invalid: " + string.Join("; ", issues));
        }

        return new FindingInput(kind == "issue" ? ReportExploreFindingKind.Issue : ReportExploreFindingKind.Warning, severity, title, expected, actual, reproduction);
    }

    private static string Text(JsonObject input, string name, int min, int max, List<string> issues)
    {
        var text = (input[name] as JsonValue)?.TryGetValue<string>(out var value) == true ? value : null;
        if (text is null || text.Length < min || text.Length > max)
        {
            issues.Add(name + ": must be " + min.ToString(CultureInfo.InvariantCulture) + " to " + max.ToString(CultureInfo.InvariantCulture) + " characters");
            return "";
        }

        return text;
    }
}
