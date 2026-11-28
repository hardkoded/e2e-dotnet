// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.StepRecorderKit;

namespace E2E.Tests.TestStep;

/// <summary>
/// <c>describe('test.step')</c> of upstream's <c>tests/unit/test-step.test.ts</c> (tester-army/e2e#2312). The first <c>it</c> is <c>Partial_</c>: the
/// port throws <see cref="InvalidOperationException"/> where upstream collects a <c>COLLECTION_ERROR</c>.
/// </summary>
public sealed class TestStepTests
{
    [Fact]
    public async Task Partial_Is_a_collection_error_outside_a_test()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            E2E.Internal.TestStep.RunAsync("Log in", () => Task.CompletedTask)
        );
        Assert.Contains("must be called inside a test body", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Runs_on_the_attempts_recorder()
    {
        var steps = new StepRecorder("a");
        using var published = Attempts.PublishAttempt(
            new PublishedAttempt { AttemptId = "a", Step = async (title, body) => await steps.GroupAsync(title, body) }
        );

        Assert.Equal("done", await E2E.Internal.TestStep.RunAsync("Log in", () => Task.FromResult("done")));
        Assert.Equal(
            [(0, "test.step", "Log in", (int?)null, StepStatus.Passed)],
            steps.All().Select(step => (step.Index, step.Api, step.Label, step.Parent, step.Status))
        );
    }

    [Fact]
    public async Task Just_runs_the_body_where_the_attempt_records_no_steps()
    {
        using var published = Attempts.PublishAttempt(new PublishedAttempt { AttemptId = "a" });
        Assert.Equal("done", await E2E.Internal.TestStep.RunAsync("Log in", () => Task.FromResult("done")));
    }

    [Fact]
    public async Task Rejects_a_blank_title_and_a_missing_body_before_anything_runs()
    {
        var steps = new StepRecorder("a");
        using var published = Attempts.PublishAttempt(
            new PublishedAttempt { AttemptId = "a", Step = async (title, body) => await steps.GroupAsync(title, body) }
        );

        var blank = await Assert.ThrowsAsync<TestException>(() =>
            E2E.Internal.TestStep.RunAsync("  ", () => Task.CompletedTask)
        );
        Assert.Equal("INVALID_ARGUMENT", blank.Code);
        var missing = await Assert.ThrowsAsync<TestException>(() =>
            E2E.Internal.TestStep.RunAsync("Log in", (Func<Task>)null!)
        );
        Assert.Equal("INVALID_ARGUMENT", missing.Code);
        Assert.Empty(steps.All());
    }
}
