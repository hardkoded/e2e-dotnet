// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using E2E.Decision.Sdk;
using E2E.Engine;

namespace E2E.Decision.Tests;

/// <summary>Upstream's <c>tests/helpers.ts</c> pieces the decision tests share.</summary>
internal static class Helpers
{
    /// <summary>A decision model whose answers come from a function of the call.</summary>
    public sealed class ScriptedDecisionModel(Func<DecisionModelCall, DecisionModelResult> script) : IDecisionModel
    {
        public List<DecisionModelCall> Calls { get; } = [];

        public string Provider => "scripted";

        public string ModelId => "scripted-decision";

        public string SpecificationVersion { get; init; } = "v4";

        public IReadOnlyList<string> SupportedQuestionTypes { get; init; } = ["choice", "boolean", "score"];

        public Task<DecisionModelResult> DoDecideAsync(DecisionModelCall call, CancellationToken cancellationToken)
        {
            Calls.Add(call);
            return Task.FromResult(script(call));
        }
    }

    /// <summary>Upstream's <c>BUTTONS</c>: two buttons, the smallest screen with a target question.</summary>
    public static IReadOnlyList<ExecutorNode> Buttons { get; } =
    [
        new ExecutorNode
        {
            Id = "save",
            Role = "button",
            Name = "Save",
        },
        new ExecutorNode
        {
            Id = "cancel",
            Role = "button",
            Name = "Cancel",
        },
    ];

    /// <summary>Upstream's <c>FIELD</c>: a field and a button, the smallest screen that types.</summary>
    public static IReadOnlyList<ExecutorNode> Field { get; } =
    [
        new ExecutorNode
        {
            Id = "name",
            Role = "textbox",
            Name = "Name",
            Value = "",
        },
        new ExecutorNode
        {
            Id = "save",
            Role = "button",
            Name = "Save",
        },
    ];

    /// <summary>Upstream's <c>invalidConfig</c>: asserts the <c>ConfigurationException</c> a factory throws at config load.</summary>
    public static void AssertInvalidConfig(Action factory, string message)
    {
        var error = Assert.Throws<ConfigurationException>(factory);
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains(message, error.Message, StringComparison.Ordinal);
    }

    /// <summary>Upstream's <c>whitePixels</c>: a decodable white screenshot of the fixture viewport.</summary>
    public static ExecutorPixels WhitePixels() => new(PngHelpers.WhitePng(800, 600), "image/png", 800, 600, 1, 0);

    /// <summary>Upstream's <c>inTurn</c>: answers in order, then <paramref name="last"/> forever.</summary>
    public static Func<string> InTurn(IReadOnlyList<string> answers, string last)
    {
        var asked = 0;
        return () => asked < answers.Count ? answers[asked++] : last;
    }

    /// <summary>
    /// Upstream's <c>scriptedDecision</c>: a scripted decision model that records requests and answers every choice question through one
    /// resolver. Unspecified probabilities become a unanimous distribution for the choice, which core validation accepts.
    /// </summary>
    public static (ScriptedChoiceModel Model, List<EvalRequest> Requests) ScriptedDecision(
        Func<string, IReadOnlyList<string>, int, ScriptedAnswer> resolve,
        IReadOnlyList<string>? supported = null,
        Exception? throws = null
    )
    {
        var requests = new List<EvalRequest>();
        var model = new ScriptedChoiceModel(resolve, requests, supported ?? ["choice"], throws);
        return (model, requests);
    }

    /// <summary>Upstream's <c>scriptedText</c>: a scripted language model that returns queued texts as JSON.</summary>
    public static ScriptedTextModel ScriptedText(params string?[] texts) =>
        ScriptedOutputs([.. texts.Select(text => (object)new Dictionary<string, object?> { ["text"] = text })]);

    /// <summary>Upstream's <c>scriptedOutputs</c>: a scripted language model that answers each call with the next queued JSON value.</summary>
    public static ScriptedTextModel ScriptedOutputs(IEnumerable<object?> values) => new(values);

    /// <summary>Upstream's <c>context</c>: a step context over a scripted screen, whose actions, budgets, turns, and transcripts a test reads back.</summary>
    public static Fixture Context(ContextOptions? options = null) => new(options ?? new ContextOptions());

    /// <summary>A step context that ends with <paramref name="signal"/>.</summary>
    public static Fixture Context(CancellationToken signal) => new(new ContextOptions { Signal = signal });

    /// <summary>One scripted answer: the choice, its distribution, and the reported confidence (upstream's <c>ScriptedAnswer</c>).</summary>
    /// <param name="Choice">The chosen option, or the chosen level of a score question.</param>
    /// <param name="Probabilities">The distribution; null spreads all the mass on the choice.</param>
    /// <param name="Confidence">The reported confidence.</param>
    /// <param name="Bare">Omit the distribution from the answer entirely.</param>
    /// <param name="Score">For a score question: the probability-weighted level; defaults to the chosen level.</param>
    public sealed record ScriptedAnswer(
        string Choice,
        IReadOnlyDictionary<string, double>? Probabilities = null,
        double? Confidence = null,
        bool Bare = false,
        double? Score = null
    );

    /// <summary>One question of a recorded decide call.</summary>
    public sealed record QuestionRecord(
        string Type,
        string? Instructions,
        IReadOnlyList<DecisionChoice>? Choices,
        IReadOnlyList<string?>? Levels
    )
    {
        /// <summary>The keys of the choice criteria, in order.</summary>
        public IReadOnlyList<string> Keys => [.. (Choices ?? []).Select(choice => choice.Value)];
    }

    /// <summary>A recorded decide call: the state and the question map the executor sent (upstream's <c>EvalRequest</c>).</summary>
    public sealed class EvalRequest
    {
        /// <summary>The JSON part of the state.</summary>
        public required JsonElement State { get; init; }

        /// <summary>The file parts of the state, as the SDK hands them to the model.</summary>
        public required IReadOnlyList<DecisionFilePart> Files { get; init; }

        public required IReadOnlyDictionary<string, QuestionRecord> Questions { get; init; }

        /// <summary>Present only when the executor sent provider options.</summary>
        public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
    }

    /// <summary>The model <see cref="ScriptedDecision"/> builds.</summary>
    public sealed class ScriptedChoiceModel(
        Func<string, IReadOnlyList<string>, int, ScriptedAnswer> resolve,
        List<EvalRequest> requests,
        IReadOnlyList<string> supported,
        Exception? throws
    ) : IDecisionModel
    {
        public string Provider => "scripted";

        public string ModelId => "scripted-1";

        public string SpecificationVersion => "v4";

        public IReadOnlyList<string> SupportedQuestionTypes => supported;

        public Task<DecisionModelResult> DoDecideAsync(DecisionModelCall call, CancellationToken cancellationToken)
        {
            if (throws is not null)
            {
                return Task.FromException<DecisionModelResult>(throws);
            }

            var index = requests.Count;
            var questions = new Dictionary<string, QuestionRecord>();
            var answers = new Dictionary<string, DecisionAnswer>();
            var confidence = new Dictionary<string, double>();
            foreach (var (id, question) in call.Questions)
            {
                if (question.Type == "score")
                {
                    var levels = Enumerable
                        .Range(0, question.Levels!.Count)
                        .Select(level => level.ToString(System.Globalization.CultureInfo.InvariantCulture))
                        .ToList();
                    questions[id] = new QuestionRecord(
                        "score",
                        question.Instructions?.ToText(),
                        null,
                        question.Levels!.Select(level => level?.ToText()).ToList()
                    );
                    var answer = resolve(id, levels, index);
                    var score = answer.Score
                        ?? double.Parse(answer.Choice, System.Globalization.CultureInfo.InvariantCulture);
                    var low = (int)Math.Floor(score);
                    var high = Math.Min(levels.Count - 1, low + 1);
                    var weightHigh = score - low;
                    var probabilities = answer.Probabilities
                        ?? levels.ToDictionary(
                            level => level,
                            level =>
                                level == low.ToString(System.Globalization.CultureInfo.InvariantCulture)
                                    ? 1 - weightHigh
                                : level == high.ToString(System.Globalization.CultureInfo.InvariantCulture) ? weightHigh
                                : 0.0
                        );
                    answers[id] = new DecisionAnswer
                    {
                        Type = "score",
                        Score = score,
                        Probabilities = answer.Bare ? null : probabilities,
                    };
                    if (answer.Confidence is { } scoreConfidence)
                    {
                        confidence[id] = scoreConfidence;
                    }

                    continue;
                }

                if (question.Type != "choice")
                {
                    throw new InvalidOperationException("scripted model answers choice and score questions only");
                }

                questions[id] = new QuestionRecord("choice", question.Instructions?.ToText(), question.Choices, null);
                var keys = question.Choices!.Select(choice => choice.Value).ToList();
                var chosen = resolve(id, keys, index);
                answers[id] = new DecisionAnswer
                {
                    Type = "choice",
                    Choice = chosen.Choice,
                    Probabilities = chosen.Bare
                        ? null
                        : chosen.Probabilities
                            ?? keys.ToDictionary(key => key, key => key == chosen.Choice ? 1.0 : 0.0),
                };
                if (chosen.Confidence is { } choiceConfidence)
                {
                    confidence[id] = choiceConfidence;
                }
            }

            var json = call.State.OfType<DecisionJsonPart>().FirstOrDefault()?.Value ?? default;
            requests.Add(
                new EvalRequest
                {
                    State = json,
                    Files = [.. call.State.OfType<DecisionFilePart>()],
                    Questions = questions,
                    ProviderOptions = call.ProviderOptions,
                }
            );
            return Task.FromResult(
                new DecisionModelResult
                {
                    Answers = answers,
                    Usage = new DecisionUsage(10, 0),
                    Rounding = new DecisionRounding(6, 6),
                    ProviderMetadata = JsonSerializer.SerializeToElement(
                        new Dictionary<string, object> { ["scripted"] = new { confidence } }
                    ),
                    ResponseModelId = "scripted-1",
                }
            );
        }
    }

    /// <summary>The language model <see cref="ScriptedText"/> builds: it answers each call with the next queued JSON value.</summary>
    public sealed class ScriptedTextModel(IEnumerable<object?> values) : IAgentModel
    {
        private readonly Queue<object?> _queue = new(values);

        /// <summary>Every request the model received.</summary>
        public List<ModelRequest> Requests { get; } = [];

        /// <summary>The request texts, as one string, for a test that checks what the model was shown.</summary>
        public string Prompts =>
            string.Concat(
                Requests.Select(request =>
                    request.System + "\n" + string.Join('\n', request.Messages.Select(message => message.Content))
                )
            );

        /// <summary>Raised by the next call, when set.</summary>
        public Exception? Throws { get; set; }

        public string Name => "scripted-text/scripted-text-1";

        public Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (Throws is not null)
            {
                return Task.FromException<ModelResponse>(Throws);
            }

            // Exhaustion throws: queue an explicit null for the goal-supplies-no-value case.
            if (_queue.Count == 0)
            {
                throw new InvalidOperationException(
                    "scriptedText exhausted: the executor asked for more field values than queued."
                );
            }

            return Task.FromResult(
                new ModelResponse
                {
                    Content = JsonSerializer.Serialize(_queue.Dequeue()),
                    Usage = new ModelCallUsage { InputTokens = 5, OutputTokens = 1 },
                }
            );
        }
    }

    /// <summary>What <see cref="Fixture"/> builds the context from (upstream's <c>context</c> options).</summary>
    public sealed class ContextOptions
    {
        public ExecutorStepKind Kind { get; init; } = ExecutorStepKind.Act;

        public IReadOnlyList<ExecutorNode>? Tree { get; init; }

        /// <summary>Changes the observation a look returns (upstream's <c>observation</c> overrides).</summary>
        public Func<ExecutorObservation, ExecutorObservation>? Observation { get; init; }

        public int MaxModelCalls { get; init; } = 25;

        public IAgentModel? Model { get; init; }

        public string Ledger { get; init; } = "";

        public ReplayedPrefix? ReplayedPrefix { get; init; }

        public IReadOnlySet<ExecutorVerb>? Verbs { get; init; }

        public CancellationToken Signal { get; init; }

        public IReadOnlyDictionary<string, JsonElement>? Params { get; init; }

        public IReadOnlyList<ExecutorSecret>? Secrets { get; init; }

        public VisionMode? Vision { get; init; }
    }

    /// <summary>A step context over a scripted screen (upstream's <c>context</c> result).</summary>
    public sealed class Fixture
    {
        internal Fixture(ContextOptions options)
        {
            var tree = options.Tree
                ??
                [
                    new ExecutorNode
                    {
                        Id = "name",
                        Role = "textbox",
                        Name = "Name",
                        Value = "",
                    },
                ];
            ObserveWith = look =>
            {
                var observation = new ExecutorObservation("#name textbox")
                {
                    Revision = "1",
                    Viewport = new TraceViewport(800, 600),
                    Tree = tree,
                };
                return options.Observation?.Invoke(observation) ?? observation;
            };
            Ctx = new ScriptedContext(this, options);
        }

        public StepExecutorContext Ctx { get; }

        public void Deconstruct(out StepExecutorContext ctx, out List<ExecutorModelCall> usage)
        {
            ctx = Ctx;
            usage = Usage;
        }

        public RecordingActions Actions { get; } = new();

        /// <summary>What each model call recorded against the step budget.</summary>
        public List<ExecutorModelCall> Usage { get; } = [];

        public List<StepTurn> Turns { get; } = [];

        public List<string> Transcripts { get; } = [];

        /// <summary>The options of every look (upstream's <c>observe</c> mock calls).</summary>
        public List<ExecutorObserveOptions> Looks { get; } = [];

        /// <summary>What a look returns, by the look's number from 0 (upstream's <c>observe.mockImplementation</c>).</summary>
        public Func<int, ExecutorObservation> ObserveWith { get; set; }
    }

    /// <summary>The actions of a <see cref="Fixture"/>: every verb is recorded and resolves, unless <see cref="Fail"/> raises for it.</summary>
    public sealed class RecordingActions : IExecutorActions
    {
        /// <summary>Every call in order: the verb, and its arguments.</summary>
        public List<(string Verb, object?[] Args)> Calls { get; } = [];

        /// <summary>Raises for the n-th call of a verb, counting from 0, when it returns an exception.</summary>
        public Func<string, int, Exception?>? Fail { get; set; }

        /// <summary>What a point tap answers; the default is a tap that found nothing listed.</summary>
        public Func<Point, PointTapResult>? TapAtResult { get; set; }

        public IEnumerable<object?[]> Of(string verb) =>
            Calls.Where(call => call.Verb == verb).Select(call => call.Args);

        public int Count(string verb) => Calls.Count(call => call.Verb == verb);

        private Task Record(string verb, params object?[] args)
        {
            var nth = Count(verb);
            Calls.Add((verb, args));
            return Fail?.Invoke(verb, nth) is { } error ? Task.FromException(error) : Task.CompletedTask;
        }

        public Task TapAsync(
            ExecutorTarget target,
            ClickOptions? options,
            CancellationToken cancellationToken = default
        ) => Record("tap", target.Id);

        public Task DoubleTapAsync(
            ExecutorTarget target,
            ClickOptions? options,
            CancellationToken cancellationToken = default
        ) => Record("doubleTap", target.Id);

        public Task LongPressAsync(ExecutorTarget target, CancellationToken cancellationToken = default) =>
            Record("longPress", target.Id);

        public Task SecondaryTapAsync(
            ExecutorTarget target,
            ClickOptions? options,
            CancellationToken cancellationToken = default
        ) => Record("secondaryTap", target.Id);

        public Task HoverAsync(ExecutorTarget target, CancellationToken cancellationToken = default) =>
            Record("hover", target.Id);

        public Task DragAsync(
            ExecutorTarget source,
            ExecutorTarget destination,
            CancellationToken cancellationToken = default
        ) => Record("drag", source.Id, destination.Id);

        public Task TypeAsync(ExecutorTarget target, string value, CancellationToken cancellationToken = default) =>
            Record("type", target.Id, value);

        public Task TypeSecretAsync(
            ExecutorTarget target,
            string secretName,
            CancellationToken cancellationToken = default
        ) => Record("typeSecret", target.Id, secretName);

        public Task ClearAsync(ExecutorTarget target, CancellationToken cancellationToken = default) =>
            Record("clear", target.Id);

        public Task PressAsync(ExecutorTarget? target, string key, CancellationToken cancellationToken = default) =>
            Record("press", target?.Id, key);

        public Task TypeTextAsync(
            string value,
            TypeTextOptions? options = null,
            CancellationToken cancellationToken = default
        ) => Record("typeText", value);

        public Task PressKeyAsync(string key, CancellationToken cancellationToken = default) => Record("pressKey", key);

        public Task DismissKeyboardAsync(CancellationToken cancellationToken = default) => Record("dismissKeyboard");

        public Task SelectAsync(ExecutorTarget target, string value, CancellationToken cancellationToken = default) =>
            Record("select", target.Id, value);

        public Task CheckAsync(ExecutorTarget target, bool check, CancellationToken cancellationToken = default) =>
            Record("check", target.Id, check);

        public Task UploadAsync(
            ExecutorTarget target,
            IReadOnlyList<string> paths,
            CancellationToken cancellationToken = default
        ) => Record("upload", target.Id, paths);

        public Task ScrollToAsync(ExecutorTarget target, CancellationToken cancellationToken = default) =>
            Record("scrollTo", target.Id);

        public Task ScrollUntilAsync(
            string text,
            ScrollDirection direction,
            ExecutorTarget? list = null,
            CancellationToken cancellationToken = default
        ) => Record("scrollUntil", text, direction);

        public Task ScrollAsync(
            ExecutorTarget? list,
            ScrollDirection direction,
            int times = 1,
            CancellationToken cancellationToken = default
        ) => Record("scroll", direction, times);

        public Task NavigateAsync(string url, CancellationToken cancellationToken = default) => Record("navigate", url);

        public Task BackAsync(CancellationToken cancellationToken = default) => Record("back");

        public async Task<PointTapResult> TapAtAsync(Point point, CancellationToken cancellationToken = default)
        {
            await Record("tapAt", point);
            return TapAtResult?.Invoke(point) ?? new PointTapResult(point, null, "tap");
        }

        public async Task<PointTapResult> HoverAtAsync(Point point, CancellationToken cancellationToken = default)
        {
            await Record("hoverAt", point);
            return new PointTapResult(point, null, "hover");
        }

        public async Task<PointDragResult> DragAtAsync(
            Point from,
            Point to,
            CancellationToken cancellationToken = default
        )
        {
            await Record("dragAt", from, to);
            return new PointDragResult(from, to, null, null, "drag");
        }

        public async Task<PointHit> HitTestAsync(Point point, CancellationToken cancellationToken = default)
        {
            await Record("hitTest", point);
            return new PointHit(point, null, null, "hit");
        }
    }

    private sealed class ScriptedContext : StepExecutorContext
    {
        private readonly Fixture _fixture;
        private int _looks;

        [SetsRequiredMembers]
        public ScriptedContext(Fixture fixture, ContextOptions options)
        {
            _fixture = fixture;
            Step = new ExecutorStep
            {
                Kind = options.Kind,
                Index = 0,
                Instruction = "Do the thing",
                Params = options.Params,
                Secrets = options.Secrets ?? [new ExecutorSecret("password", "password")],
                Vision = options.Vision,
            };
            Attempt = new ExecutorAttempt
            {
                TestId = "test",
                AttemptId = "attempt",
                CancellationToken = options.Signal,
            };
            Target = new ExecutorTargetInfo
            {
                Name = "web",
                Platform = "web",
                Verbs = options.Verbs ?? new HashSet<ExecutorVerb>(Enum.GetValues<ExecutorVerb>()),
            };
            Model = options.Model;
            Ledger = options.Ledger;
            ReplayedPrefix = options.ReplayedPrefix;
            Actions = fixture.Actions;
            Budgets = new FixtureBudgets(fixture.Usage, options.MaxModelCalls);
        }

        public override Task<ExecutorObservation> ObserveAsync(
            ExecutorObserveOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            _fixture.Looks.Add(options ?? new ExecutorObserveOptions());
            return Task.FromResult(_fixture.ObserveWith(_looks++));
        }

        public override void AttachTranscript(string text) => _fixture.Transcripts.Add(text);

        public override void AttachTurns(IReadOnlyList<StepTurn> turns) => _fixture.Turns.AddRange(turns);

        public override Task<string> AttachScreenshotAsync(
            ExecutorPixels pixels,
            string label,
            CancellationToken cancellationToken = default
        ) => Task.FromResult("screenshot");
    }

    private sealed class FixtureBudgets(List<ExecutorModelCall> usage, int maxModelCalls) : IExecutorBudgets
    {
        public int MaxActions => 25;

        public int MaxModelCalls => maxModelCalls;

        public int ActionsUsed() => 0;

        public int RemainingMs() => 60_000;

        public void RecordModelCall(ExecutorModelCall? call = null) => usage.Add(call ?? new ExecutorModelCall());

        public Task<T> RunToolAsync<T>(string name, bool mutates, Func<Task<T>> body) => body();
    }
}
