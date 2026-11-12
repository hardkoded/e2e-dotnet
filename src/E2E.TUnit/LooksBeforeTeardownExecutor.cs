// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using TUnit.Core;
using TUnitContext = global::TUnit.Core.TestContext;
using TUnit.Core.Interfaces;

namespace E2E.TUnit;

/// <summary>What the test body did: it threw, it skipped itself, or it ran to the end.</summary>
internal sealed record TestOutcome(Exception? Failure, string? Skipped);

/// <summary>
/// Wraps the test method, so the app is looked at the moment the body fails, as upstream does. A derived
/// <c>[After(Test)]</c> hook runs after the method, and runs before this fixture's own, so it could navigate away first.
/// The wrapper also keeps what the body did, because TUnit sets the test result only after its hooks have run.
/// </summary>
public sealed class LooksBeforeTeardownExecutor : ITestExecutor
{
    private static readonly ConditionalWeakTable<TUnitContext, TestOutcome> Outcomes = new();

    internal static void Forget(TUnitContext context) => Outcomes.Remove(context);

    internal static TestOutcome? OutcomeOf(TUnitContext context) => Outcomes.TryGetValue(context, out var outcome) ? outcome : null;

    public async ValueTask ExecuteTest(TUnitContext context, Func<ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var outcome = Outcome(ex);
            // A soft failure outranks a skip, as in the other adapters. TUnit reports a body that threw a skip as skipped, whatever its
            // hooks throw after it, so the failure is thrown here in place of the skip. The session caches the close, so the after-test hook gets the same one.
            if (outcome.Skipped is not null && context.Metadata.TestDetails.ClassInstance is E2ETest fixture && fixture.CloseSoftFailures() is { } soft)
            {
                Outcomes.AddOrUpdate(context, new TestOutcome(soft, null));
                await LookAsync(context, soft).ConfigureAwait(false);
                throw new TestException(soft.Code, soft.Message + Environment.NewLine + "Skipped: " + outcome.Skipped, ex);
            }

            Outcomes.AddOrUpdate(context, outcome);
            if (outcome.Failure is not null)
            {
                // A handler failure after the body's own is only collected, so it does not reach the next test.
                await SettleLateAsync(context).ConfigureAwait(false);
                await LookAsync(context, outcome.Failure).ConfigureAwait(false);
            }

            throw;
        }

        // A route or dialog handler that failed after the last step fails the attempt here, as a step would.
        if (await SettleLateAsync(context).ConfigureAwait(false) is { } late)
        {
            Outcomes.AddOrUpdate(context, new TestOutcome(late, null));
            await LookAsync(context, late).ConfigureAwait(false);
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(late);
        }

        Outcomes.AddOrUpdate(context, new TestOutcome(null, null));
    }

    private static async Task<Exception?> SettleLateAsync(TUnitContext context)
    {
        if (context.Metadata.TestDetails.ClassInstance is not E2ETest fixture)
        {
            return null;
        }

        try
        {
            await fixture.SettleLateAsync(context.Execution.CancellationToken).ConfigureAwait(false);
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

    private static TestOutcome Outcome(Exception ex) =>
        ex.GetType().Name == "SkipTestException" ? new TestOutcome(null, ex.Message) : new TestOutcome(ex, null);

    private static async Task LookAsync(TUnitContext context, Exception failure)
    {
        if (context.Metadata.TestDetails.ClassInstance is not E2ETest fixture)
        {
            return;
        }

        try
        {
            await fixture.CaptureEvidenceOf(failure).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The look is a courtesy: the test's own failure is the one to report.
        }
    }
}
