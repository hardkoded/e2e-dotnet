// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.Internal;
using E2E.Tests.AgentAct;
using E2E.Tests.AgentTraceCache;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.CacheModelProvenance;

/// <summary>
/// Upstream's <c>tests/integration/cache-model-provenance.test.ts</c>, driven through a session over the document engine with the
/// scripted two-tap model. Not ported: the <c>ListReporter</c> line <c>replayed · recorded with ...</c> the first test also checks,
/// since the port has no list reporter, and its <c>assertValidReport</c> calls, since the port has no <c>report.json</c>. A model
/// names itself in <see cref="IAgentModel.Name"/> alone, so a recording carries no provider and the label is the model id.
/// </summary>
public sealed class CacheModelProvenanceTests
{
    private const string Title = "model provenance";

    private static Task<E2ESession> StartAsync(string directory, string results, IAgentModel model, bool strict) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(CounterPage.World()),
            Model = model,
            BaseUrl = "https://counter.test",
            Cache = new FileStepCache(directory),
            CacheEnabled = true,
            CacheMode = CacheMode.ReadWrite,
            CacheStrict = strict,
            TestTitle = Title,
            ActionTimeout = TimeSpan.FromMilliseconds(300),
            ReplayTimeout = TimeSpan.FromMilliseconds(300),
            ProjectRoot = results,
            ResultsDirectory = results,
            Trace = new ResolvedRecording(RecordingMode.On, RecordingSource.Target),
        });

    private static NamedModel CounterModel(string name) => new(name, TwoTapCounter.Model());

    private static string NewResults() => Path.Combine(Path.GetTempPath(), "e2e-provenance", Guid.NewGuid().ToString("n"));

    /// <summary>Records the two-tap step with a model named <c>model-A</c> and returns the entry file.</summary>
    private static async Task<string> RecordAsync(string directory, string results)
    {
        await using (var session = await StartAsync(directory, results, CounterModel("model-A"), strict: false))
        {
            await TwoTapCounter.RunAsync(session);
            session.Complete();
        }

        return Directory.GetFiles(directory).Single();
    }

    private static async Task RewriteAsync(string file, Action<JsonObject> change)
    {
        var document = JsonNode.Parse(await File.ReadAllTextAsync(file))!.AsObject();
        change(document);
        await File.WriteAllTextAsync(file, document.ToJsonString());
    }

    [Theory]
    [InlineData("same")]
    [InlineData("different")]
    [InlineData("legacy")]
    [InlineData("null")]
    [InlineData("invalid-model")]
    [InlineData("missing-model")]
    [InlineData("invalid-provider")]
    [InlineData("blank-model")]
    [InlineData("oversized-provider")]
    [InlineData("oversized-model")]
    public async Task Reports_recording_attribution_without_changing_replay(string mode)
    {
        var directory = TwoTapCounter.NewDirectory();
        var results = NewResults();
        var file = await RecordAsync(directory, results);
        Assert.Equal("model-A", TwoTapCounter.OnlyEntry(directory).RecordedBy?.Label);
        var known = mode is "same" or "different";
        if (!known)
        {
            await RewriteAsync(file, document =>
            {
                document.Remove("recordedBy");
                JsonNode? invalid = mode switch
                {
                    "legacy" => null,
                    "null" => null,
                    "invalid-model" => new JsonObject { ["provider"] = "test", ["model"] = 1 },
                    "missing-model" => new JsonObject { ["provider"] = "test" },
                    "invalid-provider" => new JsonObject { ["provider"] = 1, ["model"] = "A" },
                    "blank-model" => new JsonObject { ["provider"] = "test", ["model"] = " " },
                    "oversized-provider" => new JsonObject { ["provider"] = new string('x', 301), ["model"] = "A" },
                    _ => new JsonObject { ["provider"] = "test", ["model"] = new string('x', 301) },
                };
                if (mode != "legacy")
                {
                    document["recordedBy"] = invalid;
                }
            });
        }

        var before = await File.ReadAllTextAsync(file);
        var replayer = CounterModel(mode == "same" ? "model-A" : "model-B");
        ActResult replayed;
        await using (var session = await StartAsync(directory, results, replayer, strict: true))
        {
            replayed = await TwoTapCounter.RunAsync(session);
            session.Complete();
        }

        Assert.Equal(0, replayer.CallCount);
        Assert.Equal("self-finalized", replayed.Cache?.Mode);
        Assert.Equal(known ? "model-A" : null, replayed.Cache?.RecordedBy?.Label);
        Assert.Equal(0, replayed.ModelCalls);
        Assert.Equal(before, await File.ReadAllTextAsync(file));
        var page = await File.ReadAllTextAsync(Path.Combine(results, ScreenshotAssertion.Slug(Title, 100, "test"), "trace.md"));
        Assert.Equal(known, page.Contains("recorded with `model-A`", StringComparison.Ordinal));
    }

    /// <summary>
    /// Not upstream's: upstream reads a missing provider as a malformed identity. A port model names itself in one field, so a
    /// recording of the model alone is whole.
    /// </summary>
    [Fact]
    public async Task Keeps_a_recording_that_names_the_model_alone()
    {
        var directory = TwoTapCounter.NewDirectory();
        var results = NewResults();
        var file = await RecordAsync(directory, results);
        await RewriteAsync(file, document => document["recordedBy"] = new JsonObject { ["model"] = "test" });

        await using var session = await StartAsync(directory, results, CounterModel("model-B"), strict: true);
        var replayed = await TwoTapCounter.RunAsync(session);
        session.Complete();

        Assert.Equal("test", replayed.Cache?.RecordedBy?.Label);
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
            context.Budgets.RecordModelCall(configured ? new ExecutorModelCall { Provider = "fake-loop", ModelId = "text-model" } : null);
            var observed = await context.ObserveAsync(cancellationToken: token);
            var target = TargetFor(observed.Text, "button \"Increment\"");
            await context.Actions.TapAsync(target, token);
            await context.Actions.TapAsync(target, token);
            context.Budgets.RecordModelCall(configured ? new ExecutorModelCall { Provider = "decision-provider", ModelId = "decision-model" } : null);
            return Passed("the counter shows 2");
        })
        {
            Version = "1",
            Model = configured ? CounterModel("text-model") : null,
        };
        Task<E2ESession> Start() => E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(CounterPage.World()),
            AgentExecutor = executor,
            BaseUrl = "https://counter.test",
            Cache = new FileStepCache(directory),
            CacheEnabled = true,
            CacheMode = CacheMode.ReadWrite,
            TestTitle = Title,
        });

        await using (var session = await Start())
        {
            await TwoTapCounter.RunAsync(session);
            var model = session.Steps.Single(step => step.Api == "agent.act").Model;
            Assert.Equal(2, model?.Calls);
            Assert.Equal(configured ? ("decision-provider", "decision-model") : (null, null), (model?.Provider, model?.Model));
            session.Complete();
        }

        var recorded = TwoTapCounter.OnlyEntry(directory).RecordedBy;
        Assert.Equal(configured ? "text-model" : null, recorded?.Label);
        await using (var session = await Start())
        {
            var replayed = await TwoTapCounter.RunAsync(session);
            session.Complete();
            Assert.Equal(1, calls);
            Assert.Equal(recorded?.Label, replayed.Cache?.RecordedBy?.Label);
        }
    }
}
