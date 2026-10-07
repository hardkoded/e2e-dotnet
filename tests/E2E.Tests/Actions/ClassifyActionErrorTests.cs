// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.Actions;

public sealed class ClassifyActionErrorTests
{
    [Fact]
    public void Treats_an_action_the_operation_deadline_cut_off_as_uncertain_Playwright_never_said_whether_the_input_landed()
    {
        var cut = new EngineException(EngineErrorCodes.OperationTimeout, "tap timed out", retryable: false);

        var error = WebErrors.ClassifyAction(cut, new LocatorAction.Tap());

        Assert.Equal("ACTION_MAY_HAVE_COMMITTED", error.Code);
        Assert.False(error.Retryable);
        Assert.Same(cut, error.InnerException);
    }
}
