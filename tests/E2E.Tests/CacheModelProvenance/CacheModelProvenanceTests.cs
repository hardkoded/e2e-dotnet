// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.Tests.AgentAct;
using E2E.Tests.AgentTraceCache;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.CacheModelProvenance;

/// <summary>
/// Upstream's <c>cache-model-provenance.test.ts</c>. The port names a model by <see cref="IAgentModel.Name"/>, which spells provider
/// and model id together, so an entry records one <c>recordedBy</c> string, and a malformed provider or model is a malformed string.
/// The tests run a session and read the step records: the port has no list reporter, no report schema, and no project run with
/// <c>trace: 'on'</c>, so the "recorded with" line of the list and of the trace page is not checked here. The page line is checked in
/// <c>TheStepsOnATracePageTests</c>.
/// </summary>
public sealed class CacheModelProvenanceTests
{
    private const string Title = "model provenance";

    private const string Source = "fake-loop/model-A";

    private static Task<E2ESession> StartAsync(string directory, IAgentModel? model = null, IStepExecutor? executor = null) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(CounterPage.World()),
            Model = model,
            AgentExecutor = executor,
            BaseUrl = "https://counter.test",
            Cache = new FileStepCache(directory),
            CacheEnabled = true,
            CacheMode = CacheMode.ReadWrite,
            TestTitle = Title,
            ActionTimeout = TimeSpan.FromMilliseconds(300),
            ReplayTimeout = TimeSpan.FromMilliseconds(300),
        });

    private static async Task<StepRecord> RunAsync(E2ESession session)
    {
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync(TwoTapCounter.Instruction);
        await Expect.That(session.Screen.GetByRole("status", "Counter")).ToHaveTextAsync("2");
        session.Complete();
        return Assert.Single(session.Steps, step => step.Api == "agent.act");
    }

    [Theory]
    [InlineData("same")]
    [InlineData("different")]
    [InlineData("legacy")]
    [InlineData("null")]
    [InlineData("invalid-model")]
    [InlineData("blank-model")]
    [InlineData("oversized-model")]
    public async Task Reports_recording_attribution_without_changing_replay(string mode)
    {
        var directory = TwoTapCounter.NewDirectory();
        await using (var recording = await StartAsync(directory, new NamedModel(TwoTapCounter.Model(), Source)))
        {
            await RunAsync(recording);
        }

        var file = Assert.Single(BillingSession.Entries(directory));
        var entry = JsonNode.Parse(await File.ReadAllTextAsync(file))!.AsObject();
        Assert.Equal(Source, (string?)entry["recordedBy"]);
        var known = mode is "same" or "different";
        if (!known)
        {
            entry.Remove("recordedBy");
            if (mode != "legacy")
            {
                entry["recordedBy"] = mode switch
                {
                    "null" => null,
                    "invalid-model" => (JsonNode)1,
                    "blank-model" => " ",
                    _ => new string('x', 301),
                };
            }

            await File.WriteAllTextAsync(file, entry.ToJsonString());
        }

        var before = await File.ReadAllTextAsync(file);
        var replay = TwoTapCounter.Model();
        StepRecord step;
        await using (var replaying = await StartAsync(directory, new NamedModel(replay, mode == "same" ? Source : "fake-loop/model-B")))
        {
            step = await RunAsync(replaying);
        }

        Assert.Equal(0, replay.CallCount);
        Assert.Equal("self-finalized", step.Cache?.Mode);
        Assert.Equal(known ? Source : null, step.Cache?.RecordedBy);
        Assert.Null(step.Model);
        Assert.Equal(before, await File.ReadAllTextAsync(file));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Attributes_an_executor_to_its_configured_model_only(bool configured)
    {
        var directory = TwoTapCounter.NewDirectory();
        var calls = 0;
        var executor = new TestExecutor("mixed-model", async (context, token) =>
        {
            calls++;
            var observation = await context.ObserveAsync();
            var target = TargetFor(observation.Text, "button \"Increment\"");
            await context.Actions.TapAsync(target, token);
            await context.Actions.TapAsync(target, token);
            return Passed("the counter shows 2");
        })
        {
            Version = "1",
            Model = configured ? new NamedModel(TwoTapCounter.Model(), "fake-loop/text-model") : null,
        };
        await using (var recording = await StartAsync(directory, executor: executor))
        {
            await RunAsync(recording);
        }

        var file = Assert.Single(BillingSession.Entries(directory));
        var entry = JsonNode.Parse(await File.ReadAllTextAsync(file))!.AsObject();
        var expected = configured ? "fake-loop/text-model" : null;
        Assert.Equal(expected, (string?)entry["recordedBy"]);

        StepRecord replayed;
        await using (var replaying = await StartAsync(directory, executor: executor))
        {
            replayed = await RunAsync(replaying);
        }

        Assert.Equal(1, calls);
        Assert.Equal(expected, replayed.Cache?.RecordedBy);
    }
}
