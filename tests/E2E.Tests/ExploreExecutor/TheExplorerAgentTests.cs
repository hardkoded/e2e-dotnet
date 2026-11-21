// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.ExploreExecutor;

/// <summary>Upstream's "the explorer agent" describe (explore-executor.test.ts, tester-army/e2e#1993).</summary>
public sealed class TheExplorerAgentTests
{
    [Fact]
    public void Puts_its_own_charter_over_the_built_in_commit_rule_submitting_twice_and_revisiting_after_a_save_are_deliberate()
    {
        var explorer = global::E2E.ExploreExecutor.ExplorerAgent(
            new ExplorerOptions(null!, "default", new AgentOptions { System = "Be careful." }, null!, _ => { })
        );

        // The built-in rules come first in the system prompt; the explorer's guidance follows them and says it wins.
        Assert.Contains("Never repeat a commit", DefaultAgent.BaseRules, StringComparison.Ordinal);
        Assert.Contains("Be careful.", explorer.System, StringComparison.Ordinal);
        Assert.Contains(
            "These rules take precedence over the built-in rules above where they differ",
            explorer.System,
            StringComparison.Ordinal
        );
        Assert.Contains("submitting twice quickly", explorer.System, StringComparison.Ordinal);
        Assert.Contains(
            "After a save, reload or revisit and check what was kept.",
            explorer.System,
            StringComparison.Ordinal
        );
    }
}
