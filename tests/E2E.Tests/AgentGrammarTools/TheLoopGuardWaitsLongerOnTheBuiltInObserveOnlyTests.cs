// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using E2E.Tests.AgentFallbackHandoff;
using E2E.Tests.EngineContract;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.AgentGrammarTools;

/// <summary>
/// The <c>describe('the loop guard waits longer on the built-in observe only')</c> of upstream's <c>agent-grammar-tools.test.ts</c>
/// (tester-army/e2e#1993). Upstream hands the step a fake context with an <c>observe</c> of its own. The port runs the step on a document
/// engine whose looks the test scripts (a page the model changes between turns, or <see cref="FallbackEngine"/> for a screen that loses its tree).
/// </summary>
public sealed class TheLoopGuardWaitsLongerOnTheBuiltInObserveOnlyTests
{
    private const string WaitNotice = "the screen was looked at several times in a row with no change";
    private const string TickNotice = "looked at many times in a row while it kept changing";

    // What the model was sent at one call. The request's message list is the loop's own and keeps growing, so a test that reads the history
    // after the step reads the end of it: it takes what it needs while the call is made (upstream's <c>loopCalls</c>).
    private sealed record LoopCall(int UserMessages, string LastPrompt, string LastToolResult, bool OnlyConclusion);

    private static LoopCall Snapshot(ModelRequest request) =>
        new(
            request.Messages.Count(message => message.Role == "user"),
            request.Messages.LastOrDefault(message => message.Role == "user")?.Content ?? "",
            request.Messages.LastOrDefault(message => message.Role == "tool")?.Content ?? "",
            request.Tools.Count == 1 && request.Tools[0].Name == "complete_step"
        );

    private static ScriptedModel Model(List<LoopCall> calls, Func<LoopCall, ModelResponse> next) =>
        new(request =>
        {
            var call = Snapshot(request);
            calls.Add(call);
            return next(call);
        });

    // A model that observes until the loop forces a verdict, and how many times it observed.
    private static (ScriptedModel Model, List<LoopCall> Calls, Func<int> Observed) ObserveUntilForced(
        Action? beforeObserve = null
    )
    {
        var observed = 0;
        var calls = new List<LoopCall>();
        var model = Model(calls, call =>
        {
            if (call.OnlyConclusion)
            {
                return ModelResponses.Done("failed", "still saving");
            }

            observed++;
            beforeObserve?.Invoke();
            return ModelResponses.Call("observe", new { });
        });
        return (model, calls, () => observed);
    }

    private static DocumentWorld SavingWorld() =>
        new DocumentWorld().Map("/", page => page.Button("Saving").Disabled = true);

    private static Task<Attempt> StepAsync(
        IAgentModel model,
        DocumentWorld? world = null,
        IStepExecutor? executor = null,
        Func<IEngine, IEngine>? wrapEngine = null
    ) =>
        RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("save the product");
            },
            world ?? SavingWorld(),
            model,
            executor: executor,
            wrapEngine: wrapEngine
        );

    // A notice joins the history as a new user message, so count the turns that added one.
    private static int NoticesAdded(List<LoopCall> calls, string notice) =>
        calls
            .Where(
                (call, index) =>
                    index > 0
                    && call.UserMessages > calls[index - 1].UserMessages
                    && call.LastPrompt.Contains(notice, StringComparison.Ordinal)
            )
            .Count();

    private static ModelTool Definition(string name) =>
        new()
        {
            Name = name,
            Description = name,
            Parameters = JsonDocument.Parse("""{ "type": "object", "properties": {} }""").RootElement,
        };

    [Fact]
    public async Task Marks_observe_and_the_plain_screenshot_as_waiting_tools_and_not_the_screenshot_that_saves_a_file()
    {
        var names = new List<string[]>();
        var executor = ToolLoop.CreateToolLoopExecutor(
            new ToolLoopExecutorOptions
            {
                Name = "listing",
                Version = "1",
                BuildPrompt = context => Task.FromResult(context.Step.Instruction),
                Tools = (context, _) =>
                {
                    string[] Names(IReadOnlyList<ToolLoopTool> tools) =>
                        [.. E2E.LoopGuards.WaitingToolNames(tools).Order(StringComparer.Ordinal)];
                    var plain = AgentTools.GrammarTools(context, null, new GrammarToolsOptions());
                    names.Add(Names(plain));
                    names.Add(Names(AgentTools.CreateGrammarTools(context)));
                    names.Add(
                        Names(
                            AgentTools.GrammarTools(
                                context,
                                null,
                                new GrammarToolsOptions { SaveScreenshot = (file, _) => Task.FromResult(file) }
                            )
                        )
                    );
                    return plain;
                },
            }
        );
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "done"));

        await StepAsync(model, executor: executor, wrapEngine: engine => new FakeEngine(engine));

        Assert.Equal(["observe", "screenshot"], names[0]);
        Assert.Equal(["observe", "screenshot"], names[1]);
        Assert.Equal(["observe"], names[2]);
    }

    [Fact]
    public async Task Lets_the_built_in_act_loop_observe_ten_times_while_a_save_stays_in_progress()
    {
        var (model, calls, observed) = ObserveUntilForced();

        var attempt = await StepAsync(model);

        Assert.IsType<AgentException>(attempt.Error);
        Assert.Equal(10, observed());
        var warned = calls.FirstOrDefault(call => call.LastPrompt.Contains(WaitNotice, StringComparison.Ordinal));
        Assert.NotNull(warned);
        Assert.Contains(
            "Observe again only while the screen still shows the work in progress",
            warned.LastPrompt,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "once it is done, do what the step still asks for, then call complete_step",
            warned.LastPrompt,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("going in circles", warned.LastPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gives_each_notice_of_a_wait_once_however_the_wait_alternates_between_ticking_and_standing_still()
    {
        DocumentElement? status = null;
        var world = new DocumentWorld().Map("/", page => status = page.Status("Saving 0s"));
        var turn = 0;
        var calls = new List<LoopCall>();
        var model = Model(calls, call =>
        {
            if (call.OnlyConclusion)
            {
                return ModelResponses.Done("failed", "forced");
            }

            // Observes 1-12 tick, 13-18 stand still on the last tick, 19 on tick again until the guard ends the wait at twenty.
            turn++;
            var second = turn <= 12 || turn >= 19 ? turn : 12;
            status!.Name = "Saving " + second + "s";
            return ModelResponses.Call("observe", new { });
        });

        await StepAsync(model, world);

        Assert.Equal(1, NoticesAdded(calls, TickNotice));
        Assert.Equal(1, NoticesAdded(calls, WaitNotice));
    }

    [Fact]
    public async Task Keeps_a_recovered_listing_as_progress_after_compaction_elides_it_from_the_history_the_model_reads()
    {
        // Opening look, then: no tree, no tree, the listing back (large) on the third observe, no tree after it. The model says which observe
        // is next before it calls it, so the script does not depend on how many times the engine is asked per look.
        var observe = 0;
        var big = new[] { FallbackFixtures.Save }
            .Concat(
                Enumerable
                    .Range(0, 900)
                    .Select(index => new SemanticNode
                    {
                        Ref = "row" + index,
                        Role = "text",
                        Name = $"row {index} {new string('x', 40)}",
                    })
            )
            .ToArray();
        var engine = new FallbackEngine
        {
            Observe = (_, _, _) =>
                Task.FromResult(
                    observe == 0 ? FallbackFixtures.TreeOf(FallbackFixtures.Save)
                    : observe == 3 ? FallbackFixtures.TreeOf(big)
                    : new Observation
                    {
                        Route = "/",
                        Roots = [],
                        Viewport = FallbackFixtures.Viewport,
                        TreeUnavailable = true,
                        Pixels = FallbackFixtures.Screenshot(1),
                    }
                ),
        };
        var (model, _, observed) = ObserveUntilForced(() => observe++);

        await FallbackRun.ActAsync(new FallbackSetup { Engine = engine, Model = model });

        // The third observe saw the listing come back, so ten unchanged looks are counted from the fourth: thirteen in all.
        Assert.Equal(13, observed());
    }

    [Fact]
    public async Task Warns_each_wait_in_a_step_not_only_the_first()
    {
        DocumentElement? save = null;
        DocumentElement? send = null;
        var world = new DocumentWorld().Map("/", page =>
        {
            save = page.Button("Saving");
            save.Disabled = true;
            send = page.Button("Send");
            send.Hidden = true;
        });
        var looks = 1;
        var sent = false;
        var calls = new List<LoopCall>();
        var model = Model(calls, call =>
        {
            if (call.OnlyConclusion)
            {
                return ModelResponses.Done("failed", "forced");
            }

            // The opening look and six observes show the first save in progress; then it is done, and the second save never ends.
            if (looks == 7)
            {
                save!.Name = "Saved";
                save.Disabled = false;
                send!.Hidden = false;
            }

            if (!sent && call.LastToolResult.Contains("\"Send\"", StringComparison.Ordinal))
            {
                sent = true;
                send!.Name = "Sending";
                send.Disabled = true;
                return ModelResponses.Tap("button", "Send");
            }

            looks++;
            return ModelResponses.Call("observe", new { });
        });

        await StepAsync(model, world);

        // One warning per wait: the second wait, after the tap, is warned again.
        Assert.True(sent);
        Assert.Equal(2, NoticesAdded(calls, WaitNotice));
    }

    [Fact]
    public async Task Lets_the_act_loop_go_on_to_the_steps_next_action_when_the_tenth_observe_shows_the_save_finished()
    {
        DocumentElement? save = null;
        var world = new DocumentWorld().Map("/", page =>
        {
            save = page.Button("Saving");
            save.Disabled = true;
            page.Link("Product list", "/list");
        });
        world.Map("/list", page => page.Heading("Products"));
        var tools = new List<string>();
        var calls = new List<LoopCall>();
        var model = Model(calls, call =>
        {
            if (call.OnlyConclusion)
            {
                return ModelResponses.Done("failed", "forced");
            }

            // The loop let the tap through: that is all this test asks, whatever the fake engine does with it.
            if (tools.Count > 0 && tools[^1] == "tap")
            {
                return ModelResponses.Done("passed", "saved, then opened the list");
            }

            // The opening look and nine observes show the save in progress; the tenth observe shows it done.
            if (tools.Count == 9)
            {
                save!.Name = "Saved";
                save.Disabled = false;
            }

            if (tools.Count >= 10)
            {
                tools.Add("tap");
                return ModelResponses.Tap("link", "Product list");
            }

            tools.Add("observe");
            return ModelResponses.Call("observe", new { });
        });

        var attempt = await StepAsync(model, world);

        Assert.Null(attempt.Error);
        Assert.Equal([.. Enumerable.Repeat("observe", 10), "tap"], tools);
    }

    [Fact]
    public async Task Keeps_the_ordinary_limit_for_a_callers_own_tool_named_observe_which_may_write()
    {
        var writes = new List<string>();
        var executor = ToolLoop.CreateToolLoopExecutor(
            new ToolLoopExecutorOptions
            {
                Name = "custom",
                System = _ => "Record observations.",
                BuildPrompt = _ => Task.FromResult("go"),
                Tools = (_, _) => [new ToolLoopTool(Definition("observe"), (_, _) =>
                        {
                            writes.Add("row");
                            return Task.FromResult(new ToolLoopResult("recorded"));
                        })],
            }
        );
        var (model, calls, observed) = ObserveUntilForced();

        var attempt = await StepAsync(model, executor: executor);

        Assert.IsType<AgentException>(attempt.Error);
        Assert.Equal(5, observed());
        Assert.Equal(5, writes.Count);
    }

    [Fact]
    public async Task Counts_only_the_calls_an_executor_keeps_in_the_history_it_sends_not_the_ones_its_prepareMessages_summarized_away()
    {
        var rows = 5;
        // The executor carries the five rows read in earlier steps into its prompt, then summarizes them away before the model reads it.
        var carried = Enumerable
            .Range(0, 5)
            .SelectMany(index =>
                new ModelMessage[]
                {
                    new()
                    {
                        Role = "assistant",
                        ToolCalls =
                        [
                            new ModelToolCall
                            {
                                Id = "old" + index,
                                Name = "read_row",
                                Arguments = JsonDocument.Parse("{}").RootElement,
                            },
                        ],
                    },
                    new()
                    {
                        Role = "tool",
                        ToolCallId = "old" + index,
                        Name = "read_row",
                        Content = "row " + (index + 1),
                    },
                }
            )
            .ToList();
        var executor = ToolLoop.CreateToolLoopExecutor(
            new ToolLoopExecutorOptions
            {
                Name = "custom",
                System = _ => "Read the next row.",
                BuildMessages = _ =>
                    Task.FromResult<IReadOnlyList<ModelMessage>>([
                        new ModelMessage { Role = "user", Content = "Rows so far:" },
                        .. carried,
                        new ModelMessage { Role = "user", Content = "Read the next row." },
                    ]),
                Tools = (_, _) =>
                    [
                        new ToolLoopTool(Definition("read_row"), (_, _) =>
                            Task.FromResult(new ToolLoopResult("row " + ++rows))
                        ),
                    ],
                PrepareMessages = (messages, _) =>
                    new PreparedMessages([
                        .. messages.Where(message => message.Role == "user" || !carried.Contains(message)),
                    ]),
            }
        );
        var model = new ScriptedModel(request =>
            Snapshot(request).OnlyConclusion || rows >= 6
                ? ModelResponses.Done("passed", "read " + rows + " rows")
                : ModelResponses.Call("read_row", new { })
        );

        var attempt = await StepAsync(model, executor: executor);

        Assert.Null(attempt.Error);
        // The step read its row: the summarized calls did not force a verdict before its first turn.
        Assert.Equal(6, rows);
    }

    [Fact]
    public async Task Keeps_the_ordinary_limit_when_the_caller_replaces_the_built_in_handler_after_the_tools_are_built()
    {
        var writes = new List<string>();
        var offered = new List<ToolLoopTool>();
        var executor = ToolLoop.CreateToolLoopExecutor(
            new ToolLoopExecutorOptions
            {
                Name = "custom",
                System = _ => "Record observations.",
                Tools = (context, _) =>
                {
                    offered.Clear();
                    offered.Add(
                        AgentTools.CreateGrammarTools(context).First(tool => tool.Definition.Name == "observe")
                    );
                    return offered;
                },
                BuildPrompt = _ =>
                {
                    offered[0] = offered[0] with
                    {
                        ExecuteAsync = (_, _) =>
                        {
                            writes.Add("row");
                            return Task.FromResult(new ToolLoopResult("recorded"));
                        },
                    };
                    return Task.FromResult("go");
                },
            }
        );
        var (model, calls, observed) = ObserveUntilForced();

        var attempt = await StepAsync(model, executor: executor);

        Assert.IsType<AgentException>(attempt.Error);
        Assert.Equal(5, observed());
        Assert.Equal(5, writes.Count);
    }

    [Fact]
    public async Task Keeps_the_ordinary_limit_when_a_caller_reuses_the_built_in_observe_with_a_handler_of_its_own()
    {
        var writes = new List<string>();
        var executor = ToolLoop.CreateToolLoopExecutor(
            new ToolLoopExecutorOptions
            {
                Name = "custom",
                System = _ => "Record observations.",
                BuildPrompt = _ => Task.FromResult("go"),
                Tools = (context, _) =>
                {
                    var builtIn = AgentTools
                        .CreateGrammarTools(context)
                        .First(tool => tool.Definition.Name == "observe");
                    return
                    [
                        builtIn with
                        {
                            ExecuteAsync = (_, _) =>
                            {
                                writes.Add("row");
                                return Task.FromResult(new ToolLoopResult("recorded"));
                            },
                        },
                    ];
                },
            }
        );
        var (model, calls, observed) = ObserveUntilForced();

        var attempt = await StepAsync(model, executor: executor);

        Assert.IsType<AgentException>(attempt.Error);
        Assert.Equal(5, observed());
        Assert.Equal(5, writes.Count);
    }
}
