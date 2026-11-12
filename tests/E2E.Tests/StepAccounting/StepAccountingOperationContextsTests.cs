// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.StepAccounting;

// Upstream's describe('step accounting operation contexts') in packages/e2e/tests/unit/step-accounting.test.ts.
public sealed class StepAccountingOperationContextsTests
{
    [Fact]
    public void Marks_actions_as_the_agents_and_as_the_tests_while_a_trace_replays()
    {
        var accounting = new E2E.StepAccounting(maxActions: 5, maxModelCalls: 5);
        var actionTimeout = TimeSpan.FromSeconds(10);

        Assert.False(accounting.ReplayingTrace);
        Assert.Equal(OperationOrigin.Agent, accounting.ActionOperation(OperationContext.Agent, actionTimeout).Origin);
        Assert.Equal(OperationOrigin.Agent, accounting.Operation(OperationContext.Agent, actionTimeout).Origin);

        accounting.Replaying(true);
        // Both contexts: targeted actions take ActionOperation, navigation takes Operation.
        Assert.True(accounting.ReplayingTrace);
        Assert.Equal(OperationOrigin.Test, accounting.ActionOperation(OperationContext.Agent, actionTimeout).Origin);
        Assert.Equal(OperationOrigin.Test, accounting.Operation(OperationContext.Agent, actionTimeout).Origin);

        accounting.Replaying(false);
        Assert.False(accounting.ReplayingTrace);
        Assert.Equal(OperationOrigin.Agent, accounting.ActionOperation(OperationContext.Agent, actionTimeout).Origin);
        Assert.Equal(OperationOrigin.Agent, accounting.Operation(OperationContext.Agent, actionTimeout).Origin);
    }

    [Fact]
    public void Caps_a_targeted_action_at_fifteen_seconds_whatever_the_action_timeout_is()
    {
        var accounting = new E2E.StepAccounting(maxActions: 5, maxModelCalls: 5);

        Assert.Equal(TimeSpan.FromSeconds(15), accounting.ActionOperation(OperationContext.Agent, TimeSpan.FromMinutes(1)).Timeout);
        Assert.Equal(TimeSpan.FromSeconds(10), accounting.ActionOperation(OperationContext.Agent, TimeSpan.FromSeconds(10)).Timeout);
        Assert.Equal(TimeSpan.FromMinutes(1), accounting.Operation(OperationContext.Agent, TimeSpan.FromMinutes(1)).Timeout);
    }
}
