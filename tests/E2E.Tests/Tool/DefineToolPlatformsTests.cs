// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Tests.Tool;

/// <summary>The port has no <c>defineTool</c>: <see cref="AgentTool"/> takes its place.</summary>
public sealed class DefineToolPlatformsTests
{
    private static AgentTool Define(bool mutates, IReadOnlyList<string>? platforms) =>
        new("noop", new JsonObject { ["type"] = "object" }, mutates, (_, _) => Task.FromResult("ok"), platforms);

    /// <summary>A step context of a host from before reads: it observes and does not override <see cref="StepExecutorContext.ReadAsync"/>.</summary>
    private sealed class OlderContext : StepExecutorContext
    {
        public static OlderContext Create() => new()
        {
            Step = new ExecutorStep { Kind = ExecutorStepKind.Act, Instruction = "inspect" },
            Attempt = new ExecutorAttempt { TestId = "t", AttemptId = "a" },
            Target = new ExecutorTargetInfo { Name = "web", Platform = "web" },
            Budgets = null!,
            Actions = null!,
        };

        public override Task<ExecutorObservation> ObserveAsync(ExecutorObserveOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ExecutorObservation("screen"));
    }

    // Upstream's `it('reports unsupported reads from an older tool context')` of `describe('defineTool platforms')` (tester-army/e2e#1633).
    [Fact]
    public async Task Reports_unsupported_reads_from_an_older_tool_context()
    {
        var context = new AgentToolContext(OlderContext.Create(), mutates: false);

        var error = await Assert.ThrowsAsync<AgentException>(() => context.ReadAsync(new ExecutorTarget("n1"), new ExecutorReadOptions { Style = ["color"] }));

        Assert.Equal("POLICY_DENIED", error.Code);
    }

    [Fact]
    public void Offers_a_scoped_tool_only_on_its_declared_platforms()
    {
        var defined = Define(true, ["ios", "android"]);

        Assert.True(defined.AppliesTo("ios"));
        Assert.False(defined.AppliesTo("web"));
        Assert.Equal(["ios", "android"], defined.Platforms);
    }

    [Fact]
    public void Rejects_an_empty_or_malformed_platforms_list()
    {
        Assert.Contains("platforms", Assert.Throws<TestException>(() => Define(false, [])).Message, StringComparison.Ordinal);
        Assert.Contains("platforms", Assert.Throws<TestException>(() => Define(false, [""])).Message, StringComparison.Ordinal);
    }
}
