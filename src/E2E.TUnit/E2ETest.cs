// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using TUnit.Core;
using TUnit.Core.Exceptions;
using TUnit.Core.Executors;
using TUnitContext = global::TUnit.Core.TestContext;

namespace E2E.TUnit;

/// <summary>
/// Base class for a TUnit test that drives the app. A before-test hook starts one engine session.
/// An after-test hook commits the replay cache from the TUnit result: a pass or a skip records
/// verified acts, a failure evicts the unverified ones it recorded or replayed, and a cancelled test
/// leaves the cache alone. The first attempt can replay. <c>[Retry]</c> attempts run live and still
/// record. TUnit builds one instance per test, so the session lives on the instance.
/// Each <c>Expect.Soft</c> failure is kept on the session. The test body runs on, and the test
/// fails when its after-test hook runs, with every soft failure in one message.
/// Settings come from <see cref="E2EFixture"/>.
/// </summary>
[TestExecutor<LooksBeforeTeardownExecutor>]
public abstract class E2ETest : E2EFixture
{
    /// <summary>
    /// The test's title: its cache identity, and the <c>Test:</c> line every prompt's context opens with.
    /// The default is the class and method name, with every argument in full, so each test and
    /// each data row keeps its own replay. The TUnit display name is not used, because a display name
    /// can be changed by an attribute or shortened for long arguments.
    /// </summary>
    protected virtual string CacheTitle(TUnitContext test)
    {
        ArgumentNullException.ThrowIfNull(test);
        var details = test.Metadata.TestDetails;
        var name = details.ClassType.FullName + "." + details.MethodName;
        return details.TestMethodArguments.Length > 0
            ? name + "(" + string.Join(", ", details.TestMethodArguments.Select(argument => ArgumentTitle.Format(argument, details.TestMethodArguments.Length > 1))) + ")"
            : name;
    }

    [Before(HookType.Test)]
    public async Task StartE2ESessionAsync(TUnitContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var details = context.Metadata.TestDetails;
        // TUnit reuses one context for every [Retry] attempt, so what an earlier attempt's body did must not read as this attempt's.
        LooksBeforeTeardownExecutor.Forget(context);
        RunSelection.SkipIfLeftOut(GetType(), details.MethodName);
        RunSelection.SkipIfSetupRan(GetType(), details.MethodName);
        await StartSessionAsync(
            CacheTitle(context),
            context.Execution.CurrentRetryAttempt + 1,
            null,
            () => LooksBeforeTeardownExecutor.OutcomeOf(context) is { Failure: not null },
            cancellationToken,
            TestMethodOf(details)).ConfigureAwait(false);
    }

    [After(HookType.Test)]
    public async Task FinishE2ESessionAsync(TUnitContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var session = TakeSession();
        if (session is null)
        {
            return;
        }

        var soft = session.CloseSoftFailures();
        var outcome = LooksBeforeTeardownExecutor.OutcomeOf(context);

        // A setup test that passed saves its sessions; a contract it broke fails it, and its result stands as the verdict.
        Exception? late = null;
        if (outcome is { Failure: null, Skipped: null } && soft is null)
        {
            try
            {
                await session.FinishSetupAsync(true, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The run was cancelled: the test's own result stands.
            }
#pragma warning disable CA1031 // Reported below, after the session is released.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                late = ex;
            }
        }

        try
        {
            session.Complete(ErrorForCache(outcome, soft) ?? late, cancellationToken);
        }
        finally
        {
            await DisposeSessionAsync(session, soft).ConfigureAwait(false);
        }

        if (soft is not null)
        {
            throw soft;
        }

        if (late is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(late);
        }
    }

    // TUnit hands out its own method metadata, so the reflection method is found by name and parameter types.
    private static System.Reflection.MethodBase? TestMethodOf(TestDetails details)
    {
        var types = details.MethodMetadata.Parameters.Select(parameter => parameter.Type).ToArray();
        for (var type = details.ClassType; type is not null; type = type.BaseType)
        {
            var found = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly)
                .FirstOrDefault(method => string.Equals(method.Name, details.MethodName, StringComparison.Ordinal) && method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(types));
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    internal Task SettleLateAsync(CancellationToken cancellationToken) => SettleAttemptAsync(cancellationToken);

    internal Task CaptureEvidenceOf(Exception failure) => CaptureFailureEvidenceAsync(failure);

    // A cleanup error must not hide the soft failures, so both are reported.
    private static async Task DisposeSessionAsync(E2ESession session, TestException? soft)
    {
        try
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (soft is not null)
        {
            throw new AggregateException(soft, ex);
        }
    }

    private static Exception? ErrorForCache(TestOutcome? outcome, TestException? soft)
    {
        // No outcome means the body never ran (a derived before-test hook threw): TUnit fails the test, so the cache must not see a pass.
        if (outcome is null)
        {
            return soft ?? new TestException("TEST_FAILED", "The test body did not run.");
        }

        var failure = outcome.Failure;
        if (failure is null)
        {
            // A soft failure outranks a skip: the test fails when the hook throws, so the cache sees a failure.
            return soft is null && outcome.Skipped is { } reason ? new SkipException(reason) : soft;
        }

        return failure is OperationCanceledException
            ? failure
            : failure as TestException ?? new TestException("TEST_FAILED", string.IsNullOrWhiteSpace(failure.Message) ? "The test failed." : failure.Message);
    }
}
