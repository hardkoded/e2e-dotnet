// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentInputStale;

/// <summary>
/// The <c>describe('secret fill stale retry')</c> of upstream's <c>agent-input-stale.test.ts</c>. Partial: only the live-execution half
/// is ported. The port has no <c>credentials</c> (the secret is a <see cref="Secret"/> param), no replay of a cached secret fill through
/// a project runner, and it records no <c>secret.fill</c> policy event and no action origin, so those assertions are not checked.
/// </summary>
public sealed class SecretFillStaleRetryTests
{
    private const string Value = "private-fixture-value";

    /// <summary>Partial: live execution only, and without the origin, the policy events, or the credentials of upstream's project.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Partial_Reauthorizes_a_changed_sink_with_disabled_during_live_execution(bool disabled)
    {
        var dispatched = 0;
        DocumentPage? current = null;
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
                dispatched++;
                current!.Roots.Add(new DocumentElement { Role = "status", Name = "Saved" });
            },
        };
        var world = new DocumentWorld().Map("/", page =>
        {
            current = page;
            page.Roots.Add(field);
        });
        var engine = new StaleOnceEngine(new DocumentEngine(world), () =>
        {
            // The input moved and, in the disabled case, was disabled, before the engine saw the fill.
            field.Rect = new BoundingBox(40, 126, 322, 49);
            field.Disabled = disabled;
        });
        var executor = new TestExecutor("stale-secret", async (context, _) =>
        {
            var observation = await context.ObserveAsync();
            await context.Actions.TypeSecretAsync(TargetFor(observation.Text, "textbox \"Password\""), "password");
            return Passed("filled");
        });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            AgentExecutor = executor,
            BaseUrl = "https://app.test",
            TestTitle = "fills the account password",
        });
        await session.App.OpenAsync("/");
        engine.Stale = true;

        var act = () => session.Agent.ActAsync(
            "fill the password",
            new ActOptions { Params = new Dictionary<string, object?> { ["password"] = Secret.Create("password", Value, "password") } });
        AgentException? error = null;
        if (disabled)
        {
            error = await Assert.ThrowsAsync<AgentException>(act);
            session.Complete(error);
        }
        else
        {
            await act();
            session.Complete(null);
        }

        var step = session.StepRecords.Single(candidate => candidate.Api == "agent.act");
        Assert.Equal(disabled ? StepStatus.Blocked : StepStatus.Passed, step.Status);
        Assert.Equal(disabled ? 0 : 1, dispatched);
        Assert.Equal(1, step.Metrics!.ActionSteps);
        Assert.Single(step.Events, item => item.Kind == StepEventKind.Observation && item.Name == "relocate");
        if (disabled)
        {
            Assert.Equal("POLICY_DENIED", error!.Code);
            Assert.Contains("the target field is disabled", JsonSerializer.Serialize(step), StringComparison.Ordinal);
        }

        Assert.DoesNotContain(Value, JsonSerializer.Serialize(session.StepRecords), StringComparison.Ordinal);
    }

    /// <summary>The document engine, failing the first fill after <see cref="Stale"/> is set with a stale node, once the page has changed.</summary>
    private sealed class StaleOnceEngine(IEngine inner, Action onStale) : IEngine
    {
        public bool Stale { get; set; }

        private Action OnStale { get; } = onStale;

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

            public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken)
            {
                if (owner.Stale && action is LocatorAction.Fill)
                {
                    owner.Stale = false;
                    owner.OnStale();
                    throw new EngineException(EngineErrorCodes.NodeStale, "input moved before mutation", retryable: true);
                }

                return session.PerformAsync(node, action, cancellationToken);
            }

            public ValueTask DisposeAsync() => session.DisposeAsync();
        }
    }
}
