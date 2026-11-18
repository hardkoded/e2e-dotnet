// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using E2E.Decision.Sdk;
using E2E.Engine;

namespace E2E.Decision;

/// <summary>Upstream's <c>executor.ts</c>.</summary>
public static partial class Executor
{
    /// <summary>How a valid model reads, for every model error (upstream's <c>EXAMPLE</c>).</summary>
    private const string Example =
        "pass a decision model, e.g. DecisionExecutor(new DecisionExecutorOptions { Model = new OpenAIDecisionModel(...) })";

    /// <summary>The handle in the runner's secret-fill summary: <c>fill secret "&lt;json string&gt;"</c> at the start (upstream's <c>SECRET_FILL</c>).</summary>
    [GeneratedRegex(
        "^fill secret \"(?:[^\"\\\\]|\\\\.)*\"",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000
    )]
    internal static partial Regex SecretFill();

    /// <summary>Upstream's <c>decisionExecutor</c>. Builds a step executor that acts through a decision model and an optional text model.</summary>
    public static IStepExecutor DecisionExecutor(DecisionExecutorOptions options)
    {
        if (options is null)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                "DecisionExecutor() takes an options object; " + Example
            );
        }

        Model.CheckModel(options.Model, new ModelNeeds("DecisionExecutor", "choice", Example));
        var gates = new Gates(options.MinProbability, options.MinConfidence);
        if (!InUnit(gates.MinProbability) || !InUnit(gates.MinConfidence))
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                "DecisionExecutor({ minProbability, minConfidence }) must be between 0 and 1"
            );
        }

        Model.CheckProviderOptions(options.ProviderOptions, "DecisionExecutor");
        return new DecisionStepExecutor(options, gates);
    }

    /// <summary>
    /// Seeds history from a replayed prefix, flagging the uncertain action. The runner's summaries name a filled secret's handle
    /// (<c>fill secret "admin.password" into ...</c>, cut at 40 characters); secret names reach the model only as <c>secret</c> question
    /// criteria, so that handle becomes <c>&lt;secret&gt;</c>. Labels and values elsewhere in a summary stay as they are, even when they match
    /// a name.
    /// </summary>
    internal static List<HistoryEntry> SeedHistory(StepExecutorContext ctx)
    {
        if (ctx.ReplayedPrefix is not { } prefix)
        {
            return [];
        }

        string Redact(string summary) => SecretFill().Replace(summary, "fill secret <secret>");
        var seeded = prefix
            .ReplayedActions.Select(action => new HistoryEntry { Action = Redact(action), Replayed = true })
            .ToList();
        if (prefix.UncertainAction is { } uncertain)
        {
            seeded.Add(
                new HistoryEntry
                {
                    Action = Redact(uncertain),
                    Replayed = true,
                    Uncertain = true,
                }
            );
        }

        return seeded;
    }

    /// <summary>
    /// Three recorded actions in a row that failed or left the page unchanged. A failed action is no progress even when the page changed on
    /// its own, as a ticking timer does.
    /// </summary>
    internal static bool Stalled(IReadOnlyList<HistoryEntry> history)
    {
        if (history.Count < 3)
        {
            return false;
        }

        return history.Skip(history.Count - 3).All(entry => entry.Error is not null || entry.PageChanged == false);
    }

    /// <summary>An automation limitation says nothing about the application correctness.</summary>
    internal static StepVerdict Blocked(string summary) =>
        new(StepVerdictStatus.Blocked, summary, "AUTOMATION_UNSUPPORTED");

    /// <summary>Insufficient evidence must never become an assertion pass.</summary>
    internal static StepVerdict Inconclusive(string summary) =>
        new(StepVerdictStatus.Failed, summary, "ASSERTION_INCONCLUSIVE");

    private static bool InUnit(double value) => double.IsFinite(value) && value is >= 0 and <= 1;
}

/// <summary>The executor <see cref="Executor.DecisionExecutor"/> builds: it runs each step through a <see cref="DecisionStep"/>.</summary>
internal sealed class DecisionStepExecutor(DecisionExecutorOptions options, Gates gates) : IStepExecutor
{
    public string Name => "decision";

    public string? Version => "2";

    public ExecutorCache Cache => ExecutorCache.Inherit;

    public IAgentModel? Model => options.TextModel;

    public bool Vision => options.Vision;

    public async Task<StepVerdict> RunStepAsync(StepExecutorContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        // The step ends with the attempt, and with the runtime's own hard stop (the deadline, a cancelled test).
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            context.Attempt.CancellationToken,
            cancellationToken
        );
        return await new DecisionStep(context, options, gates, linked.Token).RunAsync().ConfigureAwait(false);
    }
}

/// <summary>One look at the app: the path, the action space when the tree had nodes, and the pixels the step may show (upstream's <c>View</c>).</summary>
internal sealed record View(string Path, ActionSpace? Space, Screenshot? Screenshot);

/// <summary>A look with nodes to act on (upstream's <c>Acting</c>).</summary>
internal sealed record Acting(string Path, ActionSpace Space, Screenshot? Screenshot);

/// <summary>An action the loop settled on, with how it reads in the turn and in history (upstream's <c>Prepared</c>).</summary>
/// <param name="Action">The action to perform.</param>
/// <param name="Label">The turn's call label, e.g. <c>type [3] textbox "New todo" = "Buy milk"</c>.</param>
/// <param name="Description">The history line, e.g. <c>type into New todo [n3]</c>.</param>
/// <param name="TargetAnswer">The answer that chose the target.</param>
/// <param name="Typed">What the action typed.</param>
internal sealed record Prepared(
    DecisionAction Action,
    string Label,
    string Description,
    Decision? TargetAnswer = null,
    string? Typed = null
);

/// <summary>What the loop settled an operation into (upstream's <c>Next</c>): an action, a verdict to finish with, or a turn to skip with its reason.</summary>
internal sealed record Next
{
    public Prepared? Prepared { get; init; }

    public StepVerdict? Verdict { get; init; }

    public string? Skip { get; init; }

    public string? Because { get; init; }
}

/// <summary>
/// One <c>agent.act</c> or <c>agent.assert</c> step: the loop, the budget, the history the model reads, and the turns and transcript the
/// runner keeps (upstream's <c>Step</c>).
/// </summary>
internal sealed class DecisionStep
{
    /// <summary>Error codes the runtime owns: rethrown untouched, never absorbed as history (upstream's <c>RUNTIME_CODES</c>).</summary>
    private static readonly IReadOnlySet<string> RuntimeCodes = StepExecutors.RuntimeCodes;

    private static readonly Dictionary<Control, string> ControlLabels = new()
    {
        [Control.ScrollUp] = "scroll viewport up",
        [Control.ScrollDown] = "scroll viewport down",
        [Control.Back] = "back one step in history",
        [Control.DismissKeyboard] = "dismiss the keyboard",
    };

    private static readonly JsonSerializerOptions TranscriptOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly StepExecutorContext _ctx;
    private readonly DecisionExecutorOptions _options;
    private readonly Gates _gates;
    private readonly List<StepTurn> _turns = [];
    private readonly List<string> _transcript = [];
    private readonly List<HistoryEntry> _history;
    private readonly IAgentModel? _language;
    private readonly bool _canType;
    private readonly bool _vision;

    /// <summary>Whether the step can locate a drawn control: the decision model scores and the text model names it.</summary>
    private readonly bool _locates;

    private readonly PointLocator _locator;
    private readonly CancellationToken _signal;
    private CancellationToken Signal => _signal;
    private int _calls;
    private string? _previousFingerprint;
    private string? _origin;

    /// <summary>The turn of the last action taken, settled by the next observation.</summary>
    private int? _pendingTurn;

    private int _rejectedTerminals;
    private bool _attached;

    public DecisionStep(StepExecutorContext ctx, DecisionExecutorOptions options, Gates gates, CancellationToken signal)
    {
        _signal = signal;
        _ctx = ctx;
        _options = options;
        _gates = gates;
        _history = Executor.SeedHistory(ctx);
        _language = ctx.Model;
        _canType = _language is not null && ctx.Target.Verbs.Contains(ExecutorVerb.Type);
        _vision = options.Vision;
        _locates = _language is not null && options.Model.SupportedQuestionTypes.Contains("score");
        _locator = new PointLocator(ctx, AskAsync, gates);
    }

    public async Task<StepVerdict> RunAsync()
    {
        try
        {
            return await (_ctx.Step.Kind == ExecutorStepKind.Assert ? JudgeAsync() : ActAsync()).ConfigureAwait(false);
        }
        finally
        {
            // A thrown step keeps its evidence too: the transcript is what explains a refusal or a bad answer.
            Attach();
        }
    }

    /// <summary>A fresh look: the tree, and masked pixels when allowed. <paramref name="typing"/> keeps a filled field's row for verdict views.</summary>
    private async Task<View> ViewAsync(bool typing)
    {
        var observation = await _ctx.ObserveAsync(new ExecutorObserveOptions { Tree = true, Pixels = _vision }, Signal)
            .ConfigureAwait(false);
        var path = observation.Path ?? "";
        Screenshot? screenshot =
            _vision && observation.Pixels is { } pixels
                ? new Screenshot(pixels.Data, pixels.MediaType, pixels.Width, pixels.Height, pixels.Scale)
                : null;
        var tree = observation.Tree;
        if (observation.TreeUnavailable || tree is null || EmptyTree(observation))
        {
            return new View(path, null, screenshot);
        }

        var seen = new SpaceObservation
        {
            Path = path,
            Viewport = observation.Viewport ?? new TraceViewport(0, 0),
            Tree = tree,
            KeyboardVisible = observation.KeyboardVisible,
            Pixels = screenshot is null ? null : observation.Pixels,
            Locates = screenshot is not null && _locates,
        };
        return new View(path, Elements.ActionSpace(_ctx, seen, typing), screenshot);
    }

    /// <summary>One decide call under the step budget, or null once it is spent.</summary>
    private async Task<IReadOnlyDictionary<string, Decision>?> AskAsync(DecisionRequest request)
    {
        if (!Spend())
        {
            return null;
        }

        _transcript.Add(TranscriptLine(request));
        var answers = await Decide
            .DecideAsync(_ctx, _options.Model, request, _options.ProviderOptions, Signal)
            .ConfigureAwait(false);
        _transcript.Add(JsonSerializer.Serialize(answers, TranscriptOptions));
        return answers;
    }

    /// <summary>One text-model call under the step budget, or null once it is spent.</summary>
    private async Task<T?> AskTextAsync<T>(Ask<T> spec, string operation, string page, FieldDescription? field = null)
        where T : class
    {
        if (_language is null)
        {
            throw Picks.Invalid("The decision model chose " + operation + " with no text model configured.");
        }

        if (!Spend())
        {
            return null;
        }

        return await Text.AskTextAsync(
                _ctx,
                _language,
                spec,
                new FieldInput
                {
                    Goal = _ctx.Step.Instruction,
                    Context = _ctx.AgentContext,
                    Params = Params.NonSecretParams(_ctx.Step.Params),
                    Field = field,
                    Page = page,
                    RecentActions = [.. _history.Skip(Math.Max(0, _history.Count - spec.History))],
                },
                Signal
            )
            .ConfigureAwait(false);
    }

    private bool Spend()
    {
        if (_calls >= _ctx.Budgets.MaxModelCalls)
        {
            return false;
        }

        _calls++;
        return true;
    }

    private string BudgetMessage() =>
        "The step did not conclude within its "
        + _ctx.Budgets.MaxModelCalls.ToString(CultureInfo.InvariantCulture)
        + " decision calls.";

    private void Attach()
    {
        if (_attached)
        {
            return;
        }

        _attached = true;
        _ctx.AttachTurns(_turns);
        _ctx.AttachTranscript(string.Join('\n', _transcript));
    }

    /// <summary>An assertion: one verdict on the current screen.</summary>
    private async Task<StepVerdict> JudgeAsync()
    {
        var (path, space, screenshot) = await ViewAsync(true).ConfigureAwait(false);
        // `vision: 'only'` judges the pixels alone, tree or no tree; without granted pixels the tree still decides.
        var pixelsOnly = _ctx.Step.Vision == VisionMode.Only && screenshot is not null;
        if (space is null && !pixelsOnly)
        {
            return Executor.Inconclusive("A complete semantic observation is required.");
        }

        var answers = await AskAsync(
                Questions.AssertionRequest(_ctx.Step.Instruction, path, pixelsOnly ? null : space, screenshot)
            )
            .ConfigureAwait(false);
        if (answers is null)
        {
            return Executor.Blocked(BudgetMessage());
        }

        var evidence = Picks.Describe(Picks.Need(answers.GetValueOrDefault("verdict"), "verdict"));
        var settled = Picks.VerdictOf(answers.GetValueOrDefault("verdict"), _gates);
        return settled switch
        {
            Verdict.Holds => new StepVerdict(
                StepVerdictStatus.Passed,
                "The screen shows the assertion holds (" + evidence + ")."
            ),
            Verdict.Fails => new StepVerdict(
                StepVerdictStatus.Failed,
                "The screen contradicts the assertion (" + evidence + ").",
                "ASSERTION_FAILED"
            ),
            _ => Executor.Inconclusive("The screen does not settle the assertion (" + evidence + ")."),
        };
    }

    /// <summary>The act loop: observe, decide, perform, until a terminal claim is confirmed or the step cannot go on.</summary>
    private async Task<StepVerdict> ActAsync()
    {
        while (true)
        {
            var view = await ViewAsync(_canType).ConfigureAwait(false);
            _origin ??= view.Path;
            if (view.Space is null)
            {
                return Executor.Blocked("A complete semantic observation is required.");
            }

            var acting = new Acting(view.Path, view.Space, view.Screenshot);
            Settle(acting);
            if (Executor.Stalled(_history))
            {
                return Executor.Blocked("Three actions in a row failed or changed nothing on screen.");
            }

            var decided = await AskAsync(Questions.OperationRequest(_ctx, acting.Space, _history, acting.Path))
                .ConfigureAwait(false);
            if (decided is null)
            {
                return Executor.Blocked(BudgetMessage());
            }

            var op = Picks.Need(decided.GetValueOrDefault("operation"), "operation");
            if (!Picks.Gated(op, _gates))
            {
                return Executor.Blocked("The next operation is uncertain (" + Picks.Describe(op) + ").");
            }

            if (op.Choice == "blocked")
            {
                return Executor.Blocked("The decision model cannot make progress.");
            }

            if (op.Choice is "done" or "failed")
            {
                var terminal = await TerminalCheckAsync(op.Choice).ConfigureAwait(false);
                if (terminal is { } verdict)
                {
                    return verdict;
                }

                continue;
            }

            var next = await PrepareAsync(op, acting).ConfigureAwait(false);
            if (next.Verdict is { } finished)
            {
                return finished;
            }

            if (next.Skip is not null)
            {
                _history.Add(new HistoryEntry { Action = next.Skip, Error = next.Because });
                continue;
            }

            await TakeAsync(next.Prepared!, op).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Settles the last action against a fresh look: whether the page changed goes on its turn and its history entry, and the locator learns
    /// whether a point tap landed.
    /// </summary>
    private void Settle(Acting acting)
    {
        var changed = acting.Space.Fingerprint != _previousFingerprint;
        _previousFingerprint = acting.Space.Fingerprint;
        if (_pendingTurn is { } pending)
        {
            _turns[pending] = _turns[pending] with
            {
                Outcome = _turns[pending].Outcome + (changed ? ", page changed" : ", page unchanged"),
            };
            _pendingTurn = null;
        }

        if (_history.Count > 0)
        {
            _history[^1] = _history[^1] with { PageChanged = changed };
        }

        _locator.Settle(acting.Screenshot);
    }

    /// <summary>
    /// Turns the chosen operation into an action: a control as is, a point through the locator, anything else through its target question
    /// and what the operation needs beside the target.
    /// </summary>
    private async Task<Next> PrepareAsync(Decision op, Acting view)
    {
        var space = view.Space;
        if (space.ControlNamed(op.Choice) is { } control)
        {
            return new Next
            {
                Prepared = new Prepared(
                    new DecisionAction { Control = control },
                    ControlLabels[control],
                    ControlLabels[control]
                ),
            };
        }

        var operation = space.OperationNamed(op.Choice)
            ?? throw Picks.Invalid("The decision model chose an unavailable operation.");
        if (operation == Operation.TapAt)
        {
            return await PrepareTapAtAsync(view).ConfigureAwait(false);
        }

        if (operation == Operation.TypeAt)
        {
            return await PrepareTypeAtAsync(view).ConfigureAwait(false);
        }

        if (!space.Targets.TryGetValue(operation, out var group))
        {
            throw Picks.Invalid("The decision model chose an unavailable operation.");
        }

        // The target depends on the operation: its own request, after the operation settled.
        var request = Questions.TargetRequest(_ctx, space, operation, _history, view.Path);
        var answers =
            request.Questions.Count == 0
                ? new Dictionary<string, Decision>()
                : await AskAsync(request).ConfigureAwait(false);
        if (answers is null)
        {
            return Finish(Executor.Blocked(BudgetMessage()));
        }

        var chosen = Picks.Pick(answers.GetValueOrDefault("target"), group, _gates, "target", WireNames.Of(operation));
        if (chosen.Blocked is not null)
        {
            return Finish(Executor.Blocked(chosen.Blocked));
        }

        var key = chosen.Key!;
        var target = chosen.Value!;
        var element = space.Element(key);
        var field = FieldOf(key, element);
        Next Prepare(DecisionAction action, string? typed = null) =>
            new()
            {
                Prepared = new Prepared(
                    action,
                    CallLabel(element, operation, target, typed),
                    target.Description,
                    chosen.Answer,
                    typed
                ),
            };

        switch (operation)
        {
            case Operation.Type:
            {
                var answer = await AskTextAsync(Text.TextAsk, WireNames.Of(operation), space.PageText, field)
                    .ConfigureAwait(false);
                if (answer is null)
                {
                    return Finish(Executor.Blocked(BudgetMessage()));
                }

                if (string.IsNullOrEmpty(answer.Text))
                {
                    return new Next { Skip = target.Description, Because = "no value for this field" };
                }

                return Prepare(
                    new DecisionAction
                    {
                        Operation = operation,
                        Target = target,
                        Text = answer.Text,
                    },
                    answer.Text
                );
            }

            case Operation.Upload:
            {
                var answer = await AskTextAsync(Text.Paths, WireNames.Of(operation), space.PageText, field)
                    .ConfigureAwait(false);
                if (answer is null)
                {
                    return Finish(Executor.Blocked(BudgetMessage()));
                }

                if (answer.Paths.Count == 0)
                {
                    return new Next { Skip = target.Description, Because = "no files for this input" };
                }

                return Prepare(
                    new DecisionAction
                    {
                        Operation = operation,
                        Target = target,
                        Paths = answer.Paths,
                    },
                    string.Join(", ", answer.Paths)
                );
            }

            case Operation.Drag:
            {
                var destination = Picks.Pick(
                    answers.GetValueOrDefault("destination"),
                    space.Destinations,
                    _gates,
                    "destination",
                    WireNames.Of(operation)
                );
                if (destination.Blocked is not null)
                {
                    return Finish(Executor.Blocked(destination.Blocked));
                }

                if (destination.Value!.Id == target.Id)
                {
                    return Finish(Executor.Blocked("The decision model chose to drag an element onto itself."));
                }

                return Prepare(
                    new DecisionAction
                    {
                        Operation = operation,
                        Target = target,
                        DestinationId = destination.Value.Id,
                    },
                    "onto " + destination.Value.Label
                );
            }

            case Operation.TypeSecret:
            {
                var secret = Picks.Pick(
                    answers.GetValueOrDefault("secret"),
                    _ctx.Step.Secrets.ToDictionary(entry => entry.Name, StringComparer.Ordinal),
                    _gates,
                    "secret",
                    WireNames.Of(operation)
                );
                if (secret.Blocked is not null)
                {
                    return Finish(Executor.Blocked(secret.Blocked));
                }

                return Prepare(
                    new DecisionAction
                    {
                        Operation = operation,
                        Target = target,
                        Name = secret.Value!.Name,
                    }
                );
            }

            default:
                return Prepare(new DecisionAction { Operation = operation, Target = target });
        }
    }

    /// <summary>A point tap: the text model names the drawn control, the locator finds it or recalls where it last landed.</summary>
    private async Task<Next> PrepareTapAtAsync(Acting view)
    {
        if (view.Screenshot is null)
        {
            throw Picks.Invalid("The decision model chose tap_at with no screenshot.");
        }

        var named = await AskTextAsync(Text.Target, "tap_at", view.Space.PageText).ConfigureAwait(false);
        if (named is null)
        {
            return Finish(Executor.Blocked(BudgetMessage()));
        }

        var found = await PointForAsync(view, view.Screenshot, named.Target, Aim.Control).ConfigureAwait(false);
        if (found.Verdict is not null)
        {
            return found.Verdict;
        }

        var point = found.Point!.Value;
        var label = "tap " + named.Target + " at (" + Whole(point.X) + ", " + Whole(point.Y) + ")";
        return new Next
        {
            Prepared = new Prepared(new DecisionAction { Operation = Operation.TapAt, Point = point }, label, label),
        };
    }

    /// <summary>A drawn field fill: the text model names the field and its value, the locator finds the field to tap and type into.</summary>
    private async Task<Next> PrepareTypeAtAsync(Acting view)
    {
        if (view.Screenshot is null)
        {
            throw Picks.Invalid("The decision model chose type_at with no screenshot.");
        }

        var named = await AskTextAsync(Text.DrawnField, "type_at", view.Space.PageText).ConfigureAwait(false);
        if (named is null)
        {
            return Finish(Executor.Blocked(BudgetMessage()));
        }

        if (string.IsNullOrEmpty(named.Text))
        {
            return new Next { Skip = named.Target, Because = "no value for this field" };
        }

        var found = await PointForAsync(view, view.Screenshot, named.Target, Aim.Field).ConfigureAwait(false);
        if (found.Verdict is not null)
        {
            return found.Verdict;
        }

        var point = found.Point!.Value;
        var label = "type into " + named.Target + " at (" + Whole(point.X) + ", " + Whole(point.Y) + ")";
        return new Next
        {
            Prepared = new Prepared(
                new DecisionAction
                {
                    Operation = Operation.TypeAt,
                    Point = point,
                    Text = named.Text,
                },
                label,
                label,
                null,
                named.Text
            ),
        };
    }

    /// <summary>Where a drawn control is: recalled from a tap that landed, or located afresh, and recorded for the next look to settle.</summary>
    private async Task<PointFound> PointForAsync(Acting acting, Screenshot screenshot, string wanted, Aim aim)
    {
        var path = acting.Path;
        var located = _locator.Known(path, wanted, aim) is { } known
            ? Located.At(known)
            : await _locator.LocateAsync(screenshot, path, wanted, aim).ConfigureAwait(false);
        if (located is null)
        {
            return new PointFound(null, Finish(Executor.Blocked(BudgetMessage())));
        }

        if (located.Uncertain is not null)
        {
            return new PointFound(
                null,
                Finish(Executor.Blocked("The tap point is uncertain (" + located.Uncertain + ")."))
            );
        }

        var point = located.Point!.Value;
        _locator.Tapped(path, wanted, aim, point, screenshot);
        return new PointFound(point, null);
    }

    /// <summary>
    /// Performs a prepared action and records the turn and the history entry. Action failures go back to the model as history; only the
    /// runtime's own stop codes propagate.
    /// </summary>
    private async Task TakeAsync(Prepared prepared, Decision op)
    {
        var targetPart = prepared.TargetAnswer is null
            ? ""
            : ", target p=" + prepared.TargetAnswer.Probability.ToString("F3", CultureInfo.InvariantCulture);
        _turns.Add(
            new StepTurn(
                _turns.Count + 1,
                [prepared.Label],
                "op p=" + op.Probability.ToString("F3", CultureInfo.InvariantCulture) + targetPart
            )
        );
        _pendingTurn = _turns.Count - 1;
        var entry = new HistoryEntry { Action = prepared.Description, Text = prepared.Typed };
        try
        {
            var note = await Dispatch.PerformAsync(_ctx, prepared.Action, Signal).ConfigureAwait(false);
            _history.Add(note is null ? entry : entry with { Note = note });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (E2EException error) when (RuntimeCodes.Contains(error.Code))
        {
            throw;
        }
        catch (Exception error)
        {
            var code = error is E2EException known ? known.Code : error.GetType().Name;
            _history.Add(entry with { Error = code });
        }
    }

    /// <summary>
    /// Verifies a done/failed claim on a fresh screen with the actions taken so far, but no model reasoning. Returns a verdict, or null to
    /// continue the loop. A verdict under the gates confirms nothing.
    /// </summary>
    private async Task<StepVerdict?> TerminalCheckAsync(string claim)
    {
        var (path, space, screenshot) = await ViewAsync(true).ConfigureAwait(false);
        if (space is null)
        {
            return RejectClaim(claim, "incomplete observation");
        }

        var answers = await AskAsync(Questions.CompletionRequest(_ctx, space, _history, path, screenshot, _origin))
            .ConfigureAwait(false);
        if (answers is null)
        {
            return Executor.Blocked(BudgetMessage());
        }

        var evidence = Picks.Describe(Picks.Need(answers.GetValueOrDefault("verdict"), "verdict"));
        var settled = Picks.VerdictOf(answers.GetValueOrDefault("verdict"), _gates);
        if (claim == "done" && settled == Verdict.Holds)
        {
            return new StepVerdict(StepVerdictStatus.Passed, "The screen shows the step is done (" + evidence + ").");
        }

        if (claim == "failed" && settled == Verdict.Fails)
        {
            return new StepVerdict(
                StepVerdictStatus.Failed,
                "The screen shows the step failed (" + evidence + ").",
                "ACTION_FAILED"
            );
        }

        return RejectClaim(
            claim,
            settled switch
            {
                Verdict.Holds => "holds",
                Verdict.Fails => "fails",
                Verdict.Inconclusive => "inconclusive",
                _ => "below gate",
            }
        );
    }

    /// <summary>
    /// Records a rejected terminal claim and continues the loop. The second rejected claim in a step ends it, decided by the check: visible
    /// counter-evidence is a product failure, anything else an automation limit.
    /// </summary>
    private StepVerdict? RejectClaim(string claim, string check)
    {
        _rejectedTerminals++;
        _history.Add(new HistoryEntry { Action = claim, Error = "check: " + check });
        if (_rejectedTerminals < 2)
        {
            return null;
        }

        if (check == "fails")
        {
            return new StepVerdict(
                StepVerdictStatus.Failed,
                "The screen contradicts the " + claim + " claim.",
                "ACTION_FAILED"
            );
        }

        return Executor.Blocked("The screen does not confirm the " + claim + " claim (check: " + check + ").");
    }

    private static Next Finish(StepVerdict verdict) => new() { Verdict = verdict };

    /// <summary>The field a text ask describes: the element's label, role, and value; a textbox named by its key when the table has no row.</summary>
    private static FieldDescription FieldOf(string key, Element? element) =>
        element is null
            ? new FieldDescription(key, "textbox")
            : new FieldDescription(element.Label, element.Role, element.Value);

    /// <summary>One turn call label, e.g. <c>type [3] textbox "New todo" = "Buy milk"</c>.</summary>
    private static string CallLabel(Element? element, Operation operation, Target target, string? text)
    {
        if (element is null)
        {
            return target.Description;
        }

        var value = text is null ? "" : " = " + JsonText.Quote(text);
        return WireNames.Of(operation)
            + " ["
            + element.Index
            + "] "
            + element.Role
            + " "
            + JsonText.Quote(element.Label)
            + value;
    }

    /// <summary>One transcript line: the request without its pixels, which are named by size.</summary>
    private static string TranscriptLine(DecisionRequest request)
    {
        var questions = new JsonObject();
        foreach (var (id, question) in request.Questions)
        {
            var record = new JsonObject { ["type"] = question.Type };
            if (question.Instructions is not null)
            {
                record["instructions"] = question.Instructions;
            }

            if (question.Choices is { } choices)
            {
                var criteria = new JsonObject();
                foreach (var choice in choices)
                {
                    criteria[choice.Value] = choice.Description;
                }

                record["criteria"] = criteria;
            }
            else if (question.Levels is { } levels)
            {
                record["criteria"] = new JsonArray([.. levels.Select(level => (JsonNode?)level)]);
            }

            questions[id] = record;
        }

        var line = new JsonObject { ["state"] = JsonNode.Parse(request.State.GetRawText()), ["questions"] = questions };
        if (request.Screenshot is { } screenshot)
        {
            line["screenshot"] =
                screenshot.Width.ToString(CultureInfo.InvariantCulture)
                + "x"
                + screenshot.Height.ToString(CultureInfo.InvariantCulture);
        }

        return line.ToJsonString(TranscriptOptions);
    }

    /// <summary>No nodes to build an action space from. A truncated observation still acts on what is there.</summary>
    private static bool EmptyTree(ExecutorObservation observation) =>
        observation.Tree is null || (observation.Tree.Count == 0 && observation.Text.Trim() == "");

    private static string Whole(double value) =>
        ((long)Overlay.RoundHalfUp(value)).ToString(CultureInfo.InvariantCulture);

    /// <summary>A located point, or the verdict that ends the step (the union upstream's <c>pointFor</c> returns).</summary>
    private sealed record PointFound(Point? Point, Next? Verdict);
}
