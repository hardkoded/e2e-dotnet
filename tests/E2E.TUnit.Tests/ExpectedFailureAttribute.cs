// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using TUnit.Core;
using TUnit.Core.Enums;
using TUnit.Core.Interfaces;

namespace E2E.TUnit.Tests;

/// <summary>
/// Marks a test that must fail with a message holding each given text. It runs after the test's hooks, where
/// TUnit has set the result, so it sees the failure an after-test hook throws. A test that fails as expected is reported as passed.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ExpectedFailureAttribute(params string[] messageParts) : Attribute, ITestEndEventReceiver
{
    /// <summary>Texts the failure must not hold.</summary>
    public string[] Without { get; init; } = [];

    public int Order => 0;

    public EventReceiverStage Stage => EventReceiverStage.Late;

    public ValueTask OnTestEnd(global::TUnit.Core.TestContext context)
    {
        var result = context.Execution.Result;
        var message = result?.Exception?.ToString() ?? "";
        if (result?.State != TestState.Failed)
        {
            context.Execution.OverrideResult(TestState.Failed, $"The test was to fail, and its state is {result?.State}.");
        }
        else if (messageParts.FirstOrDefault(part => !message.Contains(part, StringComparison.Ordinal)) is { } missing)
        {
            context.Execution.OverrideResult(TestState.Failed, $"The failure does not hold \"{missing}\": {message}");
        }
        else if (Without.FirstOrDefault(part => message.Contains(part, StringComparison.Ordinal)) is { } present)
        {
            context.Execution.OverrideResult(TestState.Failed, $"The failure holds \"{present}\": {message}");
        }
        else
        {
            context.Execution.OverrideResult(TestState.Passed, "failed as expected");
        }

        return ValueTask.CompletedTask;
    }
}
