// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;

namespace E2E.Tests.AgentActSameTurnVerdict;

/// <summary>The pieces upstream's <c>agent-act-same-turn-verdict.test.ts</c> declares at the top of the file.</summary>
internal static class AgentActSameTurnVerdictHelpers
{
    public static readonly ModelResponse Passed = ModelResponses.Done("passed", "tapped it");

    /// <summary>The counter page with the delayed page beside it, so one world serves every test of the file.</summary>
    public static DocumentWorld FixtureWorld() => WithDelayed(CounterPage.World());

    public static StepRecord ActStep(IReadOnlyList<StepRecord>? steps) =>
        steps!.First(candidate => candidate.Api == "agent.act");

    public static string ToolResult(ScriptedModel model, int request, Index index) =>
        model
            .Requests[request]
            .Messages.Where(message => message.Role == "tool")
            .Select(message => message.Content ?? "")
            .ElementAt(index);

    /// <summary>Upstream's <c>/delayed</c> page (<c>fixture-pages/live.ts</c>): a Dead end button that does nothing, and a Continue that swaps the view a beat later.</summary>
    private static DocumentWorld WithDelayed(DocumentWorld world) => world.Map("/delayed", page =>
        {
            foreach (var label in new[] { "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta" })
            {
                page.Heading(label);
            }

            var second = page.Heading("Second view");
            second.Hidden = true;
            DocumentElement? next = null;
            page.Button("Dead end", () => { });
            next = page.Button("Continue", () => _ = Task.Delay(700)
                    .ContinueWith(
                        _ =>
                        {
                            next!.Hidden = true;
                            second.Hidden = false;
                        },
                        TaskScheduler.Default
                    ));
        });
}
