// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace E2E;

/// <summary>Whether the planner wants another step or the run to end.</summary>
[JsonConverter(typeof(PlanKindConverter))]
internal enum PlanKind
{
    Step,

    Finish,
}

internal sealed class PlanKindConverter() : JsonStringEnumConverter<PlanKind>(JsonNamingPolicy.CamelCase);

/// <summary>
/// What the planner answers. A flat object with every field required rather than a union: OpenAI's strict structured output
/// rejects a schema whose <c>required</c> omits a property, and every provider renders a flat object. Fields the decision does
/// not use are empty strings. The shape is lenient on purpose: a model that puts the whole charter into <c>title</c> and leaves
/// <c>instruction</c> empty (seen live) has still planned a step, and <see cref="ExplorePlan.RepairPlan"/> derives the missing
/// field rather than spending the repair round on it. The schema <c>ExtractAsync</c> sends carries no field descriptions, so
/// <see cref="ExplorePlan.PlanInstruction"/> says what each field holds. Upstream's <c>PLAN_SCHEMA</c>.
/// </summary>
internal sealed record PlanAnswer(
    PlanKind Decision,
    string Title,
    string Instruction,
    string Summary) : IJsonOnDeserialized
{
    /// <summary>The answer is read from the model, so the rules the schema cannot state are checked as it is read: a failure gets the repair round.</summary>
    void IJsonOnDeserialized.OnDeserialized()
    {
        var issues = PlanSchema.Issues(this);
        if (issues.Count > 0)
        {
            throw new PlanSchemaException(issues);
        }
    }
}

/// <summary>One rule an answer broke, with the field it names.</summary>
internal sealed record PlanIssue(string Path, string Message);

/// <summary>An answer that broke a rule of <see cref="PlanSchema"/>. A <see cref="JsonException"/>, so the extract that read it repairs once.</summary>
internal sealed class PlanSchemaException(IReadOnlyList<PlanIssue> issues)
    : JsonException(string.Join("; ", issues.Select(issue => issue.Path + ": " + issue.Message)))
{
    public IReadOnlyList<PlanIssue> Issues { get; } = issues;
}

/// <summary>The outcome of reading one answer against <see cref="PlanSchema"/>.</summary>
internal sealed record PlanParse(bool Success, PlanAnswer? Value, IReadOnlyList<PlanIssue> Issues);

/// <summary>The rules a planner answer must satisfy, beyond its shape. Upstream's <c>PLAN_SCHEMA</c> refinements.</summary>
internal static class PlanSchema
{
    private const int MinSummaryChars = 10;

    /// <summary>
    /// The most the grammar accepts in one field. Well above the report's ceilings, which the state clips to when the step opens:
    /// a model that pours a whole charter into one field (seen live, past 2000 characters) has still planned a step, and a
    /// rejection would cost a repair round for nothing. Validation enforces it, not the provider: <c>ExtractAsync</c> sends a
    /// schema's shape without its value rules, so a runaway field runs to the judgment's output ceiling and costs the repair
    /// round; when the repair runs away too, the planner falls back to its built-in charter.
    /// </summary>
    public const int MaxFieldChars = 8_000;

    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    /// <summary>The rules <paramref name="answer"/> breaks: a field past its ceiling, a step with nothing to do, a finish with no assessment.</summary>
    public static IReadOnlyList<PlanIssue> Issues(PlanAnswer answer)
    {
        var issues = new List<PlanIssue>();
        foreach (var (path, text) in new[] { ("title", answer.Title), ("instruction", answer.Instruction), ("summary", answer.Summary) })
        {
            if (text.Length > MaxFieldChars)
            {
                issues.Add(new PlanIssue(path, "at most " + MaxFieldChars.ToString(CultureInfo.InvariantCulture) + " characters"));
            }
        }

        if (issues.Count > 0)
        {
            return issues;
        }

        if (answer.Decision == PlanKind.Step)
        {
            if ((answer.Title + answer.Instruction).Trim().Length == 0)
            {
                issues.Add(new PlanIssue("instruction", "a step needs a concrete instruction"));
            }
        }
        else if (answer.Summary.Trim().Length < MinSummaryChars)
        {
            issues.Add(new PlanIssue("summary", "finishing needs an overall assessment"));
        }

        return issues;
    }

    /// <summary>Reads an answer as <c>ExtractAsync</c> does and reports the rules it breaks instead of throwing.</summary>
    public static PlanParse SafeParse(string json)
    {
        try
        {
            return new PlanParse(true, JsonSerializer.Deserialize<PlanAnswer>(json, ReadOptions), []);
        }
        catch (PlanSchemaException ex)
        {
            return new PlanParse(false, null, ex.Issues);
        }
        catch (JsonException ex)
        {
            return new PlanParse(false, null, [new PlanIssue(ex.Path ?? "$", ex.Message)]);
        }
    }
}

/// <summary>The planner's decision, after <see cref="ExplorePlan.RepairPlan"/>.</summary>
internal abstract record PlanDecision
{
    private PlanDecision()
    {
    }

    /// <summary>The next charter.</summary>
    public sealed record Step(string Title, string Instruction) : PlanDecision;

    /// <summary>The run ends with this assessment.</summary>
    public sealed record Finish(string Summary) : PlanDecision;
}

/// <summary>One configured credential the explorer can sign in with, by name. The password stays out of every prompt.</summary>
/// <param name="Name">The credential's name, which its password secret is named after.</param>
/// <param name="Username">The username to type.</param>
internal sealed record PlanAccount(string Name, string Username);

/// <summary>What the planner is asked.</summary>
/// <param name="MustFinish">The run must end now. The planner is asked for the closing assessment only.</param>
/// <param name="Remaining">The wall clock left.</param>
/// <param name="Timeout">The planner's own deadline.</param>
/// <param name="Accounts">The accounts the explorer can sign in with.</param>
/// <param name="Reason">Why the run must end, in the planner's prompt.</param>
/// <param name="Agent">The agent the call runs as. Null is <c>default</c>.</param>
internal sealed record PlanRequest(
    bool MustFinish,
    TimeSpan Remaining,
    TimeSpan Timeout,
    IReadOnlyList<PlanAccount>? Accounts = null,
    string? Reason = null,
    string? Agent = null);

/// <summary>
/// The planner: between exploration steps, one structured model call decides the next charter or finishes the run. It runs as an
/// <c>ExtractAsync</c> step, so it reads the current screen the same way every judgment does, under the same budgets and repair
/// loop.
/// </summary>
internal static class ExplorePlan
{
    /// <summary>A title derived from an instruction stops at the first clause or here.</summary>
    private const int DerivedTitleChars = 80;

    /// <summary>
    /// What the closing assessment is for. The findings and the steps print above it, so it carries what neither list can: the
    /// verdict, the gaps, and what to script next.
    /// </summary>
    private static readonly string[] AssessmentRules =
    [
        "The assessment is two to four plain sentences for the person reading the terminal, where the steps and the findings",
        "are already listed above it. Do not list or count the findings again. Say: your verdict on the goal in one sentence;",
        "what the goal names that was not reached or not fully exercised, and why; and which one or two flows are most worth",
        "turning into scripted tests. Name screens and controls as the app names them; no headings, bullets, or markdown.",
    ];

    /// <summary>Asks the model for the next charter or the closing assessment.</summary>
    public static async Task<PlanDecision> PlanNextAsync(IExploreAgent agent, ExploreState state, PlanRequest request, CancellationToken cancellationToken = default)
    {
        var answer = await agent.ExtractAsync<PlanAnswer>(
            PlanInstruction(state, request),
            new ExtractOptions { Timeout = request.Timeout, Agent = request.Agent },
            cancellationToken).ConfigureAwait(false);
        var decision = RepairPlan(answer);

        // An answer that was only padding has no charter left: it counts as an answer outside the grammar.
        return decision is PlanDecision.Step { Instruction.Length: 0 }
            ? throw new AgentException("MODEL_OUTPUT_INVALID", "the planner's step has no instruction once padding is removed")
            : decision;
    }

    /// <summary>
    /// The decision, after the repairs live providers' answers have needed. Every repair the planner makes is here:
    /// <list type="bullet">
    /// <item>Digit runs a provider pads a field with (seen live: a title followed by hundreds of <c>1234567890</c>) are removed. They
    /// carry no meaning and would otherwise become the charter's tail. The threshold sits past any number a charter can mean: phone
    /// numbers, order ids, card numbers, and amounts all stop short of twenty digits.</item>
    /// <item>A step with only one of <c>title</c> and <c>instruction</c> filled has the other derived: a charter with no title takes
    /// its first clause, a title with no charter is the charter.</item>
    /// </list>
    /// Length is not repaired here: the grammar accepts long fields, and the state clips them to the report's ceilings when the step opens.
    /// </summary>
    public static PlanDecision RepairPlan(PlanAnswer answer)
    {
        if (answer.Decision == PlanKind.Finish)
        {
            return new PlanDecision.Finish(Unpad(answer.Summary));
        }

        var title = Unpad(answer.Title);
        var instruction = Unpad(answer.Instruction);
        var charter = instruction.Length == 0 ? title : instruction;

        // A title that stood in for the charter is a heading only while it is short.
        var longest = instruction.Length == 0 ? DerivedTitleChars : ExploreState.MaxTitleChars;
        var heading = title.Length > 0 && title.Length <= longest ? title : DeriveTitle(charter);
        return new PlanDecision.Step(heading, charter);
    }

    /// <summary>The planner's instruction: the goal, the record so far, the budget, and how to decide.</summary>
    public static string PlanInstruction(ExploreState state, PlanRequest request)
    {
        var steps = state.Steps;
        var findings = state.Findings;
        var minutesLeft = Math.Max(0, Math.Round(request.Remaining.TotalMinutes, MidpointRounding.AwayFromZero));
        var stepLines = steps.Count == 0
            ? "(none yet: this is the first step)"
            : string.Join('\n', steps.Select(step => step.Index.ToString(CultureInfo.InvariantCulture) + ". [" + StatusMark(step.Status) + "] " + step.Title + (step.Summary is null ? "" : " — " + step.Summary)));
        var findingLines = findings.Count == 0
            ? "(none yet)"
            : string.Join('\n', findings.Select(finding => "- [" + KindName(finding.Kind) + ", severity " + finding.Severity.ToString(CultureInfo.InvariantCulture) + "] " + finding.Title));
        var header = new List<string>
        {
            request.MustFinish
                ? "Finish an exploratory test run with your overall assessment."
                : "Plan the next step of an exploratory test run, or finish it.",
            "",
            "Goal: " + state.Goal,
            "Budget: " + steps.Count.ToString(CultureInfo.InvariantCulture) + " of " + state.Budgets.MaxSteps.ToString(CultureInfo.InvariantCulture)
                + " steps used, about " + minutesLeft.ToString(CultureInfo.InvariantCulture) + " minute(s) left.",
            "",
            "Steps so far:",
            stepLines,
            "",
            "Findings so far:",
            findingLines,
        };
        if (request.Accounts is { Count: > 0 } accounts)
        {
            header.Add("");
            header.Add("Accounts the agent can sign in with (it fills the password itself, by name):");
            header.AddRange(accounts.Select(account => "- " + account.Name + " (username: " + account.Username + ")"));
        }

        header.Add("");
        if (request.MustFinish)
        {
            header.Add("The run must end now: " + (request.Reason ?? "its budget is spent") + ".");
            header.Add("Respond with {\"decision\": \"finish\", \"title\": \"\", \"instruction\": \"\", \"summary\": \"...\"}: the closing assessment.");
            header.AddRange(AssessmentRules);
            return string.Join('\n', header);
        }

        header.AddRange(
        [
            "Decide from the goal, the steps so far, and the current screen:",
            "- \"step\": the next exploration charter. \"title\" names the area or flow in a few words. \"instruction\" is a concrete,",
            "  self-contained charter for an agent that sees only the screen and that text: which flow to exercise, what inputs",
            "  to try, what to check. One flow or screen per step, sized for five to fifteen actions; split anything bigger.",
            "  Ask the agent to interact, not only to look: submit forms with made-up test data, save and revisit,",
            "  sign in with the accounts listed above (or made-up credentials when none are configured), act on a non-first item of a list. Prefer breadth: touch the main",
            "  flows the goal names before drilling deeper into one. Do not re-test an area a passed step already covered, and",
            "  never plan a step to re-confirm a finding already recorded above. When a step ended at its limit, continue where",
            "  it stopped or move on. When a step summary mentions something odd that is not among the findings, spend the next",
            "  step confirming it.",
            "- \"finish\": the goal is covered, or nothing new is reachable. \"summary\" is the closing assessment.",
        ]);
        header.AddRange(AssessmentRules.Select(line => "  " + line));
        header.Add("Respond with every field present: {\"decision\": \"step\", \"title\": \"...\", \"instruction\": \"...\", \"summary\": \"\"} or {\"decision\": \"finish\", \"title\": \"\", \"instruction\": \"\", \"summary\": \"...\"}.");
        return string.Join('\n', header);
    }

    private static string Unpad(string text) => Regex.Replace(text, @"\s*[0-9]{20,}\s*", " ").Trim();

    private static string DeriveTitle(string charter)
    {
        var end = charter.IndexOfAny(['.', ':', ';', '\n']);
        var clause = (end < 0 ? charter : charter[..end]).Trim();
        var shortClause = clause.Length == 0 ? charter : clause;
        return shortClause.Length <= DerivedTitleChars ? shortClause : shortClause[..(DerivedTitleChars - 1)] + "…";
    }

    private static string StatusMark(ReportExploreStepStatus status) => status switch
    {
        ReportExploreStepStatus.Passed => "passed",
        ReportExploreStepStatus.Failed => "FAILED",
        ReportExploreStepStatus.Blocked => "blocked",
        _ => "ended at its limit",
    };

    private static string KindName(ReportExploreFindingKind kind) => kind == ReportExploreFindingKind.Issue ? "issue" : "warning";
}
