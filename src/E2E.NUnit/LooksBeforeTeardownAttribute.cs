// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;

namespace E2E.NUnit;

/// <summary>
/// Wraps the test method, so the app is looked at the moment the body fails, as upstream does. A derived [TearDown] runs
/// after the method, and an <see cref="ITestAction"/> runs after that, so neither can look before the teardown does.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class LooksBeforeTeardownAttribute : Attribute, IWrapTestMethod
{
    public TestCommand Wrap(TestCommand command) => new LookAtFailureCommand(command);

    private sealed class LookAtFailureCommand(TestCommand inner) : DelegatingTestCommand(inner)
    {
        public override TestResult Execute(TestExecutionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            try
            {
                var result = innerCommand.Execute(context);
                Look(context, null);
                return result;
            }
            catch (Exception ex)
            {
                Look(context, ex);
                throw;
            }
        }

        // A skip or a pass is no failure, unless an Expect.Soft failed before it: that fails the test, as the teardown reports.
        private static void Look(TestExecutionContext context, Exception? thrown)
        {
            var failure = thrown is not null and not ResultStateException { ResultState.Status: TestStatus.Skipped or TestStatus.Inconclusive or TestStatus.Passed }
                ? thrown
                : SoftFailureOf(context.CurrentResult);
            if (failure is null || context.TestObject is not E2ETest fixture)
            {
                return;
            }

            try
            {
                fixture.CaptureEvidenceOf(failure).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // The look is a courtesy: the test's own failure is the one to report.
            }
        }

        private static TestException? SoftFailureOf(TestResult result)
        {
            var failed = result.AssertionResults.FirstOrDefault(assertion => assertion.Status == AssertionStatus.Failed);
            return failed is null ? null : new TestException("TEST_FAILED", failed.Message);
        }
    }
}
