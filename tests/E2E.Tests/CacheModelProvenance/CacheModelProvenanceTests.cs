// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.Tests.AgentAct;
using E2E.Tests.AgentTraceCache;
using E2E.Tests.ReportJson;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.CacheModelProvenance;

/// <summary>
/// Upstream's <c>cache-model-provenance.test.ts</c>. The port names a model by <see cref="IAgentModel.Name"/>, which spells provider
/// and model id together, so an entry records one <c>recordedBy</c> string, and a malformed provider or model is a malformed string.
/// The tests run a session and read the step records and <c>report.json</c>, which they check against the report schema. The port
/// has no list reporter, so the "recorded with" line of the list is not checked, and no project run with <c>trace: 'on'</c>, so the
/// line of the trace page is not checked here: it is checked in <c>TheStepsOnATracePageTests</c>.
/// </summary>
public sealed class CacheModelProvenanceTests
{
    private const string Title = "model provenance";

    private const string Source = "fake-loop/model-A";

    private static Task<E2ESession> StartAsync(string directory, IAgentModel? model = null, IStepExecutor? executor = null, ReportDirectory? report = null) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            ResultsDirectory = report?.Results,
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

    /// <summary>Partial: the port has no list reporter, so the "recorded with" line of the list is not checked; the trace page's line is checked in <c>TheStepsOnATracePageTests</c>.</summary>
    [Theory]
    [InlineData("same")]
    [InlineData("different")]
    [InlineData("legacy")]
    [InlineData("null")]
    [InlineData("invalid-model")]
    [InlineData("blank-model")]
    [InlineData("oversized-model")]
    public async Task Partial_Reports_recording_attribution_without_changing_replay(string mode)
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
        using var report = new ReportDirectory();
        StepRecord step;
        await using (var replaying = await StartAsync(directory, new NamedModel(replay, mode == "same" ? Source : "fake-loop/model-B"), report: report))
        {
            step = await RunAsync(replaying);
        }

        Assert.Equal(0, replay.CallCount);
        Assert.Equal("self-finalized", step.Cache?.Mode);
        Assert.Equal(known ? Source : null, step.Cache?.RecordedBy);
        Assert.Null(step.Model);
        Assert.Equal(before, await File.ReadAllTextAsync(file));
        var wire = report.Step("agent.act");
        Assert.Equal("self-finalized", (string?)wire["cache"]!["mode"]);
        Assert.Equal(known ? "fake-loop" : null, (string?)wire["cache"]!["recordedBy"]?["provider"]);
        Assert.Equal(known ? "model-A" : null, (string?)wire["cache"]!["recordedBy"]?["model"]);
        Assert.Null(wire["model"]);
    }

    /// <summary>
    /// Partial: a step whose executor reports no provider or model is attributed to the executor's name upstream (<c>mixed-model</c>); the
    /// port's report says <c>unknown</c> for both, so the unconfigured case checks the model calls only.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Partial_Attributes_an_executor_to_its_configured_model_only(bool configured)
    {
        var directory = TwoTapCounter.NewDirectory();
        var calls = 0;
        var executor = new TestExecutor("mixed-model", async (context, token) =>
        {
            calls++;
            context.Budgets.RecordModelCall(configured ? new ExecutorModelCall { Provider = "fake-loop", ModelId = "text-model" } : null);
            var observation = await context.ObserveAsync();
            var target = TargetFor(observation.Text, "button \"Increment\"");
            await context.Actions.TapAsync(target, token);
            await context.Actions.TapAsync(target, token);
            context.Budgets.RecordModelCall(configured ? new ExecutorModelCall { Provider = "decision-provider", ModelId = "decision-model" } : null);
            return Passed("the counter shows 2");
        })
        {
            Version = "1",
            Model = configured ? new NamedModel(TwoTapCounter.Model(), "fake-loop/text-model") : null,
        };
        using var recordedReport = new ReportDirectory();
        await using (var recording = await StartAsync(directory, executor: executor, report: recordedReport))
        {
            await RunAsync(recording);
        }

        var file = Assert.Single(BillingSession.Entries(directory));
        var entry = JsonNode.Parse(await File.ReadAllTextAsync(file))!.AsObject();
        var expected = configured ? "fake-loop/text-model" : null;
        Assert.Equal(expected, (string?)entry["recordedBy"]);

        var recordedModel = recordedReport.Step("agent.act")["model"]!;
        Assert.Equal(2, (int)recordedModel["calls"]!);
        if (configured)
        {
            Assert.Equal(("decision-provider", "decision-model"), ((string)recordedModel["provider"]!, (string)recordedModel["model"]!));
        }

        using var replayedReport = new ReportDirectory();
        StepRecord replayed;
        await using (var replaying = await StartAsync(directory, executor: executor, report: replayedReport))
        {
            replayed = await RunAsync(replaying);
        }

        Assert.Equal(1, calls);
        Assert.Equal(expected, replayed.Cache?.RecordedBy);
        var replayedCache = replayedReport.Step("agent.act")["cache"]!;
        Assert.Equal(configured ? "fake-loop" : null, (string?)replayedCache["recordedBy"]?["provider"]);
        Assert.Equal(configured ? "text-model" : null, (string?)replayedCache["recordedBy"]?["model"]);
    }
}
