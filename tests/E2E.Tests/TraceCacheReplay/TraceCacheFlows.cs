// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Tests.TraceCacheReplay;

/// <summary>
/// Upstream's <c>tests/helpers/trace-cache.ts</c>: the flows a scripted model records and replays through the real browser engine,
/// the model that acts them out, and the readers of what a run reported and what the store holds. Upstream drives them through the
/// runner from a generated test file; the port runs each flow as one session over a shared cache directory.
/// </summary>
internal static class TraceCacheFlows
{
    public enum Variant
    {
        Record,
        Replay,
    }

    /// <summary>What a flow's body reads of its run: the unique value its parameters carry.</summary>
    public sealed class FlowRun
    {
        public string Value { get; set; } = "";
    }

    /// <summary>One flow: where it opens, what its test does after <c>app.open</c>, the model's part of every act step, and what each step records.</summary>
    public sealed record Flow(
        string Title,
        Func<Variant, string?> Open,
        Func<E2ESession, FlowRun, Task> Body,
        IReadOnlyDictionary<string, Func<string, FlowRun, ModelResponse>> Script,
        IReadOnlyList<int> Actions);

    /// <summary>One flow's session: the error it ended with and the agent steps it made, in order.</summary>
    public sealed record FlowOutcome(string Title, Exception? Error, IReadOnlyList<StepRecord> ActSteps);

    private static ModelResponse Turn(params (string Name, object Arguments)[] calls) => new()
    {
        ToolCalls = [.. calls.Select((call, index) => new ModelToolCall
        {
            Id = "call_" + call.Name + "_" + index.ToString(CultureInfo.InvariantCulture),
            Name = call.Name,
            Arguments = ModelResponses.Call(call.Name, call.Arguments).ToolCalls[0].Arguments,
        })],
    };

    private static (string, object) Tap(string reference) => ("tap", new { @ref = reference });

    private static (string, object) Type(string reference, string value) => ("fill", new { @ref = reference, value });

    /// <summary>The reference of the last listed node whose line matches <paramref name="pattern"/>.</summary>
    private static string NodeIdFor(string prompt, string pattern) => NodeIdsFor(prompt, pattern).LastOrDefault() ?? throw new InvalidOperationException("No line " + pattern + " in the screen:\n" + prompt);

    /// <summary>Node references of every line matching the pattern, in document order.</summary>
    private static List<string> NodeIdsFor(string prompt, string pattern) =>
        [.. Regex.Matches(prompt, @"^\s*(?:added |changed )?#(e\d+) " + pattern + @"[^\n]*$", RegexOptions.Multiline).Select(match => match.Groups[1].Value)];

    /// <summary>The "Reserve now" button in the row whose label is <paramref name="offer"/>.</summary>
    private static string ReserveButtonFor(string prompt, string offer)
    {
        var lines = prompt.Split('\n');
        var row = Array.FindIndex(lines, line => line.Contains("Offer " + offer, StringComparison.Ordinal));
        if (row < 0)
        {
            throw new InvalidOperationException("No row for offer " + offer + " in the screen:\n" + prompt);
        }

        var button = lines.Skip(row + 1).First(line => line.Contains("button \"Reserve now\"", StringComparison.Ordinal));
        return Regex.Match(button, @"#(e\d+)").Groups[1].Value;
    }

    private static ModelResponse IncrementTwice(string prompt, FlowRun run)
    {
        var id = NodeIdFor(prompt, "button \"Increment\"");
        return Turn(Tap(id), Tap(id));
    }

    private static ModelResponse IncrementOnce(string prompt, FlowRun run) => Turn(Tap(NodeIdFor(prompt, "button \"Increment\"")));

    private static string Stamp() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

    private static Task ExpectTextAsync(E2ESession session, string status, string text) =>
        Expect.That(session.Screen.GetByRole("status", status)).ToHaveTextAsync(text);

    /// <summary>
    /// The flows the suites record on a first run and replay on a second. Every flow acts in one batched turn and concludes on the
    /// next. The replay variant opens the same screens under the shapes a second visit takes: a fresh record id, reordered rows, a
    /// banner pushed in above the form.
    /// </summary>
    public static readonly IReadOnlyList<Flow> Flows =
    [
        new(
            "increments the counter",
            _ => null,
            async (session, _) =>
            {
                await session.Agent.ActAsync("increment the counter twice");
                await ExpectTextAsync(session, "Counter", "2");
            },
            new Dictionary<string, Func<string, FlowRun, ModelResponse>> { ["increment the counter twice"] = IncrementTwice },
            [2]),
        new(
            "creates a company",
            _ => "/companies/new",
            async (session, run) =>
            {
                var name = "E2E " + Stamp() + " Company";
                run.Value = name;
                await session.Agent.ActAsync("create a company named {name}", new ActOptions { Params = new Dictionary<string, object?> { ["name"] = Values.Unique(name) } });
                await Expect.That(session.Screen.GetByRole("heading", name)).ToBeVisibleAsync();
            },
            new Dictionary<string, Func<string, FlowRun, ModelResponse>>
            {
                ["create a company named"] = (prompt, run) => Turn(Type(NodeIdFor(prompt, "textbox \"Company name\""), run.Value), Tap(NodeIdFor(prompt, "button \"(?:Create|Save)\""))),
            },
            [2]),
        new(
            "searches",
            _ => "/search",
            async (session, run) =>
            {
                var q = "E2E " + Stamp() + " widget";
                run.Value = q;
                await session.Agent.ActAsync("search for {q}", new ActOptions { Params = new Dictionary<string, object?> { ["q"] = Values.Unique(q) } });
                await Expect.That(session.Screen.GetByRole("heading", "Results for " + q)).ToBeVisibleAsync();
            },
            new Dictionary<string, Func<string, FlowRun, ModelResponse>>
            {
                ["search for"] = (prompt, run) => Turn(Type(NodeIdFor(prompt, "textbox \"Query\""), run.Value), Tap(NodeIdFor(prompt, "button \"Search\""))),
            },
            [2]),
        new(
            "archives a record",
            variant => variant == Variant.Replay ? "/records/9f8e7d6c5b4a" : "/records/1a2b3c4d5e6f",
            async (session, _) =>
            {
                await session.Agent.ActAsync("archive this record");
                await ExpectTextAsync(session, "Record state", "archived");
            },
            new Dictionary<string, Func<string, FlowRun, ModelResponse>>
            {
                ["archive this record"] = (prompt, _) => Turn(Tap(NodeIdFor(prompt, "button \"(?:Archive|Retire)\""))),
            },
            [1]),
        new(
            "refreshes the feed",
            _ => "/feed",
            async (session, _) =>
            {
                await session.Agent.ActAsync("refresh the feed");
                await ExpectTextAsync(session, "Marker", "refreshed");
            },
            new Dictionary<string, Func<string, FlowRun, ModelResponse>>
            {
                ["refresh the feed"] = (prompt, _) => Turn(Tap(NodeIdFor(prompt, "button \"Refresh feed\""))),
            },
            [1]),
        new(
            "fills the twins form",
            // The replay run renders the b variant (FlowsApp.Variant) under the recorded URL.
            _ => "/twins-form",
            async (session, _) =>
            {
                await session.Agent.ActAsync("fill in the twins form");
                await ExpectTextAsync(session, "Summary", "nickname=ada motto=carpe diem first=quill second=ember picked=2");
            },
            new Dictionary<string, Func<string, FlowRun, ModelResponse>>
            {
                ["fill in the twins form"] = (prompt, _) =>
                {
                    var anonymous = Regex.Matches(prompt, @"^\s*#(e\d+) textbox\s*$", RegexOptions.Multiline).Select(match => match.Groups[1].Value).ToList();
                    var adds = NodeIdsFor(prompt, "button \"Add\"");
                    return Turn(
                        Type(NodeIdFor(prompt, "textbox \"Nickname\""), "ada"),
                        Type(NodeIdFor(prompt, "textbox \"Motto\""), "carpe diem"),
                        Type(anonymous[0], "quill"),
                        Type(anonymous[1], "ember"),
                        Tap(adds[1]),
                        Tap(NodeIdFor(prompt, "button \"Submit\"")));
                },
            },
            [6]),
        new(
            "reserves offer B",
            // The replay run renders the b variant, the offers reversed, under the recorded URL.
            _ => "/repeats",
            async (session, _) =>
            {
                await session.Agent.ActAsync("reserve offer B");
                await ExpectTextAsync(session, "Picked", "B");
            },
            new Dictionary<string, Func<string, FlowRun, ModelResponse>>
            {
                ["reserve offer B"] = (prompt, _) => Turn(Tap(ReserveButtonFor(prompt, "B"))),
            },
            [1]),
        new(
            "clears a field",
            _ => "/clear-field",
            async (session, _) =>
            {
                await session.Agent.ActAsync("type a draft, then clear it");
                await ExpectTextAsync(session, "Draft state", "cleared");
            },
            new Dictionary<string, Func<string, FlowRun, ModelResponse>>
            {
                ["type a draft, then clear it"] = (prompt, _) =>
                {
                    var draft = NodeIdFor(prompt, "textbox \"Draft\"");
                    return Turn(Type(draft, "draft"), Type(draft, ""));
                },
            },
            [2]),
        new(
            "mixes deterministic and agent steps",
            _ => null,
            async (session, _) =>
            {
                await session.Agent.ActAsync("increment the counter once");
                await ExpectTextAsync(session, "Counter", "1");
                await session.Screen.GetByRole("button", "Increment").TapAsync();
                await ExpectTextAsync(session, "Counter", "2");
                await session.Agent.ActAsync("increment the counter one more time");
                await ExpectTextAsync(session, "Counter", "3");
            },
            new Dictionary<string, Func<string, FlowRun, ModelResponse>>
            {
                ["increment the counter once"] = IncrementOnce,
                ["increment the counter one more time"] = IncrementOnce,
            },
            [1, 1]),
    ];

    /// <summary>The flow with this title, failing when the table has none.</summary>
    public static Flow FlowByTitle(string title) => Flows.FirstOrDefault(candidate => candidate.Title == title) ?? throw new InvalidOperationException("no flow titled \"" + title + "\"");

    /// <summary>
    /// The scripted model behind a flow: it acts once on the screen of its first turn, concludes on the next, and fails the step
    /// loudly on an instruction no flow scripted. <paramref name="calls"/> counts every model call of the run.
    /// </summary>
    public static ScriptedModel Model(Flow flow, FlowRun run, Counter calls) => new(request =>
    {
        calls.Increment();
        var prompt = request.Messages[0].Content!;
        var key = flow.Script.Keys.Where(candidate => prompt.Contains(candidate, StringComparison.Ordinal)).OrderByDescending(candidate => candidate.Length).FirstOrDefault()
            ?? throw new InvalidOperationException("unscripted instruction in:\n" + prompt);
        return request.Messages.Any(message => message.Role == "tool")
            ? ModelResponses.Done("passed", "done: " + key)
            : flow.Script[key](prompt, run);
    });

    /// <summary>A model-call counter shared by the sessions of one run.</summary>
    public sealed class Counter
    {
        private int _value;

        public int Value => Volatile.Read(ref _value);

        public void Increment() => Interlocked.Increment(ref _value);
    }

    /// <summary>Runs every flow once as its own session over the cache directory, the app opened under <paramref name="variant"/>.</summary>
    public static async Task<List<FlowOutcome>> RunAsync(FlowsApp app, string directory, IEnumerable<Flow> flows, Variant variant, Counter calls)
    {
        var outcomes = new List<FlowOutcome>();
        foreach (var flow in flows)
        {
            var run = new FlowRun();
            await using var session = await E2ESession.StartAsync(new E2ESessionOptions
            {
                Engine = new WebEngine(headless: true),
                Model = Model(flow, run, calls),
                BaseUrl = app.Url,
                AppIdentity = "fixture-app",
                Cache = new FileStepCache(directory),
                CacheEnabled = true,
                CacheMode = CacheMode.ReadWrite,
                TestTitle = flow.Title,
                AssertionTimeout = TimeSpan.FromSeconds(5),
                ActionTimeout = TimeSpan.FromSeconds(10),
                StepTimeout = TimeSpan.FromSeconds(30),
                TestTimeout = TimeSpan.FromSeconds(60),
            });
            Exception? error = null;
            try
            {
                var open = flow.Open(variant);
                if (open is null)
                {
                    await session.App.OpenAsync();
                }
                else
                {
                    await session.App.OpenAsync(open);
                }

                await flow.Body(session, run);
            }
#pragma warning disable CA1031 // A failed flow is reported by the test, which names it.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                error = ex;
            }

            session.Complete(error);
            outcomes.Add(new FlowOutcome(flow.Title, error, [.. session.StepRecords.Where(step => step.Api == "agent.act")]));
        }

        return outcomes;
    }

    /// <summary>The entries under the store, parsed, in file order.</summary>
    public static List<(string File, CacheEntry Entry)> ReadEntries(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var cache = new FileStepCache(directory);
        return [.. Directory.GetFiles(directory, "*.json").Order(StringComparer.Ordinal).Select(file => (file, cache.Read(Path.GetFileNameWithoutExtension(file)).Entry ?? throw new InvalidOperationException("Unreadable cache entry: " + file)))];
    }

    /// <summary>The entries recorded for one test, by its title, in file order.</summary>
    public static List<CacheEntry> EntriesFor(string directory, string title) => [.. ReadEntries(directory).Select(item => item.Entry).Where(entry => entry.Test == title)];

    /// <summary>A step the cache served whole: no model turn.</summary>
    public static void ExpectReplayed(StepRecord step, int actions)
    {
        Assert.Equal(StepStatus.Passed, step.Status);
        Assert.Equal(("self-finalized", actions, actions), (step.Cache?.Mode, step.Cache?.ReplayedActions, step.Cache?.TotalActions));
        Assert.Equal(0, step.Metrics?.ModelCalls);
    }
}
