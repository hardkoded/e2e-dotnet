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
            TestResult result;
            try
            {
                result = innerCommand.Execute(context);
            }
            catch (Exception ex)
            {
                // The body failed first: a handler failure that lands after it is collected so it does not reach the next test, and the body's own failure stays the verdict.
                Settle(context);
                Look(context, ex);
                throw;
            }

            // A route or dialog handler that failed after the last step fails the attempt here, as a step would.
            var late = Settle(context);
            if (late is not null && result.ResultState.Status == TestStatus.Passed)
            {
                result.SetResult(ResultState.Failure, late.Message, late.StackTrace);
                Look(context, late);
                return result;
            }

            Look(context, null);
            return result;
        }

        private static Exception? Settle(TestExecutionContext context)
        {
            if (context.TestObject is not E2ETest fixture)
            {
                return null;
            }

            try
            {
                fixture.SettleLate().GetAwaiter().GetResult();
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
#pragma warning disable CA1031 // The failure is the handler's: it becomes the test's result, or is dropped behind an earlier one.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                return ex;
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
