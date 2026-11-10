// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;


namespace E2E.Tests.AgentFallbackHandoff;

/// <summary>
/// The four tests of upstream's <c>agent semantic fallback through the engine contract</c> suite, run once for the class as upstream runs
/// its one project. Each test is its own session on a screen with a password field, whose tree a plain look cannot read. The port runs
/// the suite's four tests in four sessions and keeps what each one left for the assertions.
/// </summary>
public sealed class MaskedFallbackRuns : IAsyncLifetime
{
    /// <summary>The password the secret fills, which no request, step, or report may carry.</summary>
    public const string Password = "fixture-secret-value";

    /// <summary>Upstream's <c>agent.assert</c> title that judges the fallback screenshot with vision on.</summary>
    public const string VisionJudgment = "vision judgment receives fallback";

    /// <summary>Upstream's title of the tree-only judgment that must not reach the model.</summary>
    public const string TreeOnlyJudgment = "tree-only judgment cannot infer absence";

    /// <summary>Upstream's title of the act that reads the fallback after a secret fill.</summary>
    public const string ActAfterFill = "act reads fallback pixels after a secret fill";

    /// <summary>Upstream's title of the judgment that reads the fallback after a secret fill.</summary>
    public const string JudgmentAfterFill = "judgment reads fallback pixels after a secret fill";

    /// <summary>Every look the masked engine answered: whether a secret had been filled, and whether the look asked for the pixel fallback.</summary>
    public List<(bool Filled, bool Fallback)> Captures { get; } = [];

    /// <summary>The scripted judge every judgment asks, shared by the four sessions as upstream's fake model is.</summary>
    public ScriptedModel Judge { get; } = new(_ => ModelResponses.Judgment("holds", "visible in the screenshot"));

    /// <summary>The scripted loop model the act asks.</summary>
    public ScriptedModel Loop { get; } = new(_ => ModelResponses.Done("passed", "read pixels"));

    /// <summary>What each title's session ended with, keyed by title.</summary>
    public Dictionary<string, FallbackOutcome> Outcomes { get; } = new(StringComparer.Ordinal);

    public async Task InitializeAsync()
    {
        Outcomes[VisionJudgment] = await RunAsync(session => session.Agent.AssertAsync("the screenshot shows the result", new AssertOptions { Vision = VisionMode.On }));
        Outcomes[TreeOnlyJudgment] = await RunAsync(session => session.Agent.AssertAsync("nothing is visible", new AssertOptions { Vision = VisionMode.Off }));
        Outcomes[ActAfterFill] = await RunAsync(
            async session =>
            {
                await session.Screen.GetByRole("textbox").FillAsync(Secret.Create("memberPassword", Password));
                await session.Agent.ActAsync("read the screenshot");
            });
        Outcomes[JudgmentAfterFill] = await RunAsync(
            async session =>
            {
                await session.Screen.GetByRole("textbox").FillAsync(Secret.Create("memberPassword", Password));
                await session.Agent.AssertAsync("the screenshot shows the result", new AssertOptions { Vision = VisionMode.On });
            });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<FallbackOutcome> RunAsync(Func<E2ESession, Task> body)
    {
        var secretFilled = false;
        var engine = new FallbackEngine
        {
            LocatorKinds = ["fill"],
            Locate = _ => [new SemanticNode { Ref = "password", Role = "textbox", InputPurpose = "password", States = new NodeStates { Secure = true } }],
            Perform = (_, _) => secretFilled = true,
            Observe = (options, _, _) =>
            {
                lock (Captures)
                {
                    Captures.Add((secretFilled, options?.PixelFallback == true));
                }

                if (options?.PixelFallback != true)
                {
                    throw new EngineException("OPERATION_TIMEOUT", "semantic capture timed out");
                }

                return Task.FromResult(FallbackFixtures.PixelSnapshot(1));
            },
        };
        return await FallbackRun.RunAsync(new FallbackSetup { Engine = engine, Model = Loop, Judge = Judge }, body);
    }
}
