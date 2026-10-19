// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.StepHooks;

/// <summary>
/// .NET-only: a step knows the hook it ran in. Upstream's runner sets it (<c>enter</c> in <c>run/execute.ts</c>); the port has no runner,
/// so <see cref="E2E.StepHooks"/> reads the step's call stack. NUnit's marks are covered in <c>E2E.NUnit.Tests</c>; these are
/// xUnit's, where a test class sets up and tears down through <c>InitializeAsync</c> and <c>DisposeAsync</c>.
/// </summary>
public sealed class StepHooksTests
{
    [Fact]
    public async Task Names_beforeEach_for_a_step_of_InitializeAsync_afterEach_for_one_of_DisposeAsync_and_none_for_the_test()
    {
        var steps = new StepRecorder("attempt");
        var lifecycle = new Lifecycle(steps);

        await lifecycle.InitializeAsync();
        await Step(steps);
        await lifecycle.DisposeAsync();

        Assert.Equal([StepPhase.BeforeEach, null, StepPhase.AfterEach, StepPhase.AfterEach], steps.All().Select(step => step.Phase));
    }

    [Fact]
    public async Task Names_the_hook_of_a_step_called_after_an_await_in_the_hook()
    {
        var steps = new StepRecorder("attempt");

        await new Lifecycle(steps).DisposeAsync();

        Assert.Equal([StepPhase.AfterEach, StepPhase.AfterEach], steps.All().Select(step => step.Phase));
    }

    [Fact]
    public async Task Takes_the_phase_the_recorder_was_given_before_the_stack()
    {
        var steps = new StepRecorder("attempt") { Phase = StepPhase.BeforeEach };

        await Step(steps);
        steps.Phase = null;
        await Step(steps);

        Assert.Equal([StepPhase.BeforeEach, null], steps.All().Select(step => step.Phase));
    }

    private static Task Step(StepRecorder steps) => steps.RunAsync(StepKind.App, "app.open", "/", () => Task.CompletedTask);

    private sealed class Lifecycle(StepRecorder steps) : E2EFixture
    {
        public Task InitializeAsync() => Step(steps);

        public async Task DisposeAsync()
        {
            await Step(steps);
            await Task.Yield();
            await Step(steps);
        }
    }
}
