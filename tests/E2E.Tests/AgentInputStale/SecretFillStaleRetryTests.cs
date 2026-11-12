// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using E2E.Reports;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentInputStale;

/// <summary>
/// The <c>describe('secret fill stale retry')</c> of upstream's <c>agent-input-stale.test.ts</c>: a stale fill re-enters secret
/// authorization, on a live action and on a cached replay alike.
/// </summary>
public sealed class SecretFillStaleRetryTests
{
    private const string Value = "private-fixture-value";

    private const string Title = "fills the account password";

    private const string Config = """{ "cache": { "mode": "read-write" }, "credentials": { "member": { "username": "example", "password": "private-fixture-value" } } }""";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reauthorizes_a_changed_sink_with_disabled_during_live_execution(bool disabled)
    {
        var project = RunProject.CreateProject();
        try
        {
            await AssertRetryAsync(project, disabled, replay: false);
        }
        finally
        {
            project.Cleanup();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reauthorizes_a_changed_sink_with_disabled_during_replay(bool disabled)
    {
        var project = RunProject.CreateProject();
        try
        {
            await AssertRetryAsync(project, disabled, replay: true);
        }
        finally
        {
            project.Cleanup();
        }
    }

    private static async Task AssertRetryAsync(FixtureProject project, bool disabled, bool replay)
    {
        if (replay)
        {
            // A recording is made first, on a page the fill does not change.
            var (recorded, _) = await RunFillAsync(project, disabled: false, stale: false);
            Assert.Equal(E2E.Reports.ResultStatus.Passed, RunProject.ResultByTitle(recorded, Title).Status);
            Assert.Equal(E2E.Reports.CacheWrite.Saved, ActStep(recorded).Cache?.Write);
        }

        var (outcome, recorder) = await RunFillAsync(project, disabled, stale: true);
        var result = RunProject.ResultByTitle(outcome, Title);
        var step = ActStep(outcome);
        Assert.Equal(disabled ? E2E.Reports.ResultStatus.Failed : E2E.Reports.ResultStatus.Passed, result.Status);
        Assert.Equal(disabled ? 0 : 1, recorder.Dispatched);
        var origin = replay ? OperationOrigin.Test : OperationOrigin.Agent;
        Assert.Equal(Enumerable.Repeat(origin, disabled ? 1 : 2), recorder.Origins);

        // A denied replay hands off to the executor, whose own denied action also counts.
        Assert.Equal(replay && disabled ? 2 : 1, step.Metrics!.ActionSteps);
        Assert.Single(step.Events, item => item.Kind == E2E.Reports.StepEventKind.Observation && item.Name == "relocate");
        Assert.Equal(disabled ? 1 : 2, step.Events.Count(item => item.Kind == E2E.Reports.StepEventKind.Policy && item.Name == "secret.fill" && item.Status == E2E.Reports.StepEventStatus.Passed));
        if (disabled)
        {
            Assert.Contains("POLICY_DENIED", JsonSerializer.Serialize(step), StringComparison.Ordinal);
        }

        if (replay && !disabled)
        {
            Assert.Equal((E2E.Reports.CacheMode.SelfFinalized, 1), (step.Cache?.Mode, step.Cache?.ReplayedActions));
        }

        Assert.DoesNotContain(Value, JsonSerializer.Serialize(outcome.Report), StringComparison.Ordinal);
    }

    /// <summary>
    /// Upstream's suite: <c>agent.act('fill the password', { params: { password: credentials.user('member').password } })</c>, then the
    /// status it shows is visible. The fill is the step's one action.
    /// </summary>
    private static async Task<(RunOutcome Outcome, Recorder Recorder)> RunFillAsync(FixtureProject project, bool disabled, bool stale)
    {
        var recorder = new Recorder();
        var field = new DocumentElement
        {
            Role = "textbox",
            Name = "Password",
            TestId = "password",
            InputPurpose = "password",
            Secure = true,
            Rect = new BoundingBox(40, 237, 322, 49),
            OnFill = _ =>
            {
                recorder.Dispatched++;
                recorder.Current!.Roots.Add(new DocumentElement { Role = "status", Name = "Saved" });
            },
        };
        var world = new DocumentWorld().Map("/", page =>
        {
            recorder.Current = page;
            page.Roots.Add(field);
        });
        var engine = new StaleOnceEngine(new DocumentEngine(world), () =>
        {
            // The input moved and, in the disabled case, was disabled, before the engine saw the fill.
            field.Rect = new BoundingBox(40, 126, 322, 49);
            field.Disabled = disabled;
        }, recorder)
        {
            Stale = stale,
        };
        var executor = new TestExecutor("stale-secret", async (context, token) =>
        {
            var observation = await context.ObserveAsync(cancellationToken: token);
            await context.Actions.TypeSecretAsync(TargetFor(observation.Text, "textbox \"Password\""), "member.password", token);
            return Passed("filled");
        });
        var test = new InMemoryTest(Title, async session =>
        {
            await session.App.OpenAsync("/");
            await session.Agent.ActAsync(
                "fill the password",
                new ActOptions { Params = new Dictionary<string, object?> { ["password"] = Credentials.User("member").Password } });
            await Expect.That(session.Screen.GetByRole("status")).ToBeVisibleAsync();
        });
        var outcome = await RunProject.RunExistingAsync(project, new RunProjectOptions
        {
            Tests = [test],
            Engine = engine,
            Config = Config,
            Agents = new Dictionary<string, AgentOptions> { ["default"] = new AgentOptions { Executor = executor } },
        });
        return (outcome, recorder);
    }

    private static ReportStep ActStep(RunOutcome outcome)
    {
        var attempt = RunProject.ResultByTitle(outcome, Title).Attempts[^1];
        return Assert.Single(attempt.Steps, candidate => candidate.Api == "agent.act");
    }

    /// <summary>What the fill did across a run: the origin of every fill the engine was asked for, and how many dispatched.</summary>
    private sealed class Recorder
    {
        public int Dispatched { get; set; }

        public List<OperationOrigin> Origins { get; } = [];

        public DocumentPage? Current { get; set; }
    }

    /// <summary>The document engine, failing the first fill after <see cref="Stale"/> is set with a stale node, once the page has changed.</summary>
    private sealed class StaleOnceEngine(IEngine inner, Action onStale, Recorder recorder) : IEngine
    {
        public bool Stale { get; set; }

        private Action OnStale { get; } = onStale;

        private Recorder Recorder { get; } = recorder;

        public string Platform => inner.Platform;

        public string Version => inner.Version;

        public EngineCapabilities Capabilities => inner.Capabilities;

        public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) =>
            new Session(this, await inner.StartAsync(options, cancellationToken));

        private sealed class Session(StaleOnceEngine owner, IEngineSession session) : IEngineSession
        {
            public string Route => session.Route;

            public bool CanLocate => session.CanLocate;

            public Task OpenAsync(string url, CancellationToken cancellationToken) => session.OpenAsync(url, cancellationToken);

            public Task<Observation> ObserveAsync(CancellationToken cancellationToken) => session.ObserveAsync(cancellationToken);

            public Task PressAsync(string key, CancellationToken cancellationToken) => session.PressAsync(key, cancellationToken);

            public Task<IReadOnlyList<SemanticNode>> LocateAsync(LocatorExpression expression, TimeSpan timeout, CancellationToken cancellationToken) =>
                session.LocateAsync(expression, timeout, cancellationToken);

            public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) =>
                PerformAsync(node, action, OperationContext.Agent, cancellationToken);

            public Task PerformAsync(SemanticNode node, LocatorAction action, OperationContext context, CancellationToken cancellationToken)
            {
                if (action is LocatorAction.Fill)
                {
                    // Recorded before the stale check, as upstream records the origin of every fill it is asked to perform.
                    owner.Recorder.Origins.Add(context.Origin);
                    if (owner.Stale)
                    {
                        owner.Stale = false;
                        owner.OnStale();
                        throw new EngineException(EngineErrorCodes.NodeStale, "input moved before mutation", retryable: true);
                    }
                }

                return session.PerformAsync(node, action, context, cancellationToken);
            }

            public ValueTask DisposeAsync() => session.DisposeAsync();
        }
    }
}
