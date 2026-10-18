// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Tests.StepRecorderKit;
using Progress = global::E2E.StepProgress;

namespace E2E.Tests.StepProgress;

/// <summary>Upstream's <c>describe('StepRecorder progress identity')</c>.</summary>
public sealed class StepRecorderProgressIdentityTests
{
    [Fact]
    public async Task Keeps_every_nested_phase_attached_to_its_report_step_and_the_shared_retry_attempt()
    {
        var heard = new List<Progress>();
        var steps = new StepRecorder("member-attempt", new StepRecorderOptions { Attempt = ("group-attempt", 1), OnProgress = heard.Add });

        await steps.RunAsync(StepKind.Agent, "agent.act", "outer", async () =>
        {
            steps.Replaying(true);
            await steps.RunAsync(StepKind.Agent, "agent.act", "inner", () =>
            {
                steps.Activity(StepActivity.Observe);
                steps.RecordEvent(new StepEvent { Kind = StepEventKind.Observation, StartedAt = DateTimeOffset.UtcNow });
                return Task.CompletedTask;
            });
            steps.Replaying(false);
        });

        Assert.Equal([0, 0, 1, 1, 1, 1, 0, 0], heard.Select(progress => progress.Identity?.StepIndex));
        foreach (var progress in heard)
        {
            var record = Assert.Single(steps.All(), step => step.Id == progress.Identity?.StepId);
            Assert.Equal(new StepProgressIdentity("group-attempt", 1, record.Id, record.Index), progress.Identity);
        }
    }

    [Theory]
    [InlineData("AUTH_CREDENTIAL_UNAVAILABLE", true, "blocked")]
    [InlineData("CANCELLED", false, "cancelled")]
    public async Task Publishes_the_redacted_error_as_the_step_ends(string code, bool blocked, string status)
    {
        var heard = new List<Progress>();
        var steps = new StepRecorder("attempt", new StepRecorderOptions { OnProgress = heard.Add, Redact = text => text.Replace("private-value", "[REDACTED]", StringComparison.Ordinal) });

        await Assert.ThrowsAsync<AgentException>(() => steps.RunAsync(StepKind.Agent, "agent.act", "login", () =>
        {
            steps.AttachAgentDetails(new StepAgentDetails { Explanation = "private-value is unavailable" });
            throw new AgentException(code, "private-value is unavailable", blocked);
        }));

        var end = heard[^1];
        Assert.Equal(StepProgressPhase.End, end.Phase);
        Assert.Equal(new StepProgressIdentity("attempt", 0, "attempt:0", 0), end.Identity);
        Assert.Equal(status, StepNames.Of(end.Status!.Value));
        Assert.Equal(code, end.Error?.Code);
        Assert.Equal("[REDACTED] is unavailable", end.Error?.Message);
        Assert.Equal("[REDACTED] is unavailable", end.Explanation);
        Assert.DoesNotContain("private-value", JsonSerializer.Serialize(heard), StringComparison.Ordinal);
        Assert.Same(steps.All()[0].Error, end.Error);
    }

    [Fact]
    public async Task Records_and_publishes_a_label_with_a_secret_the_test_spelled_into_it_redacted()
    {
        var heard = new List<Progress>();
        var steps = new StepRecorder("attempt", new StepRecorderOptions { OnProgress = heard.Add, Redact = text => text.Replace("private-value", "[REDACTED]", StringComparison.Ordinal) });

        await steps.RunAsync(StepKind.Agent, "agent.act", "inspect private-value", () => Task.CompletedTask);

        Assert.Equal("inspect [REDACTED]", steps.All()[0].Label);
        Assert.Equal([StepProgressPhase.Start, StepProgressPhase.End], heard.Select(progress => progress.Phase));
        Assert.All(heard, progress => Assert.Equal("inspect [REDACTED]", progress.Label));
    }
}
