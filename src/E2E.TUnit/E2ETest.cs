// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using TUnit.Core;
using TUnit.Core.Enums;
using TUnit.Core.Exceptions;
using TUnit.Core.Executors;
using TUnit.Core.Interfaces;
using TUnitContext = global::TUnit.Core.TestContext;

namespace E2E.TUnit;

/// <summary>
/// Base class for a TUnit test that drives the app. A before-test hook starts one engine session.
/// An after-test hook commits the replay cache from the TUnit result: a pass or a skip records
/// verified acts, a failure evicts the unverified ones it recorded or replayed, and a cancelled test
/// leaves the cache alone. A derived <c>[After(Test)]</c> hook that throws fails the test, and the cache sees that failure: the commit
/// runs when TUnit ends the test, because the result holds the hook's failure only then. The first attempt can replay. <c>[Retry]</c> attempts run live and still
/// record. TUnit builds one instance per test, so the session lives on the instance.
/// Each <c>Expect.Soft</c> failure is kept on the session. The test body runs on, and the test
/// fails when its after-test hook runs, with every soft failure in one message.
/// Settings come from <see cref="E2EFixture"/>.
/// </summary>
[TestExecutor<LooksBeforeTeardownExecutor>]
public abstract class E2ETest : E2EFixture, ITestEndEventReceiver, ITestRegisteredEventReceiver
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
            ? name
                + "("
                + string.Join(
                    ", ",
                    details.TestMethodArguments.Select(argument =>
                        ArgumentTitle.Format(argument, details.TestMethodArguments.Length > 1)
                    )
                )
                + ")"
            : name;
    }

    /// <summary>
    /// .NET-only: the cancellation token of the before-test hook that starts the session (TUnit's hook timeout and the run's cancellation).
    /// <see cref="E2EFixture.CreateEngine"/> runs inside that hook and can wait on it, so a subclass that blocks there passes this token, not the test's.
    /// </summary>
    protected CancellationToken SessionStartToken { get; private set; }

    /// <summary>
    /// Skips the running test as fixme when <paramref name="condition"/> holds (upstream's <c>test.fixme(condition, reason)</c>). The skip reports
    /// the cause <c>fixme</c> and the reason <c>fixme</c>, or <c>fixme: &lt;reason&gt;</c> for a reason that is not blank. Like <c>Skip.Test</c>
    /// it ends the test method at once. A call from a setup test fails it with the message "Fixme() cannot skip a setup test" (the report code is <c>INVALID_ARGUMENT</c>); a call outside a test throws
    /// <see cref="InvalidOperationException"/> where upstream has <c>COLLECTION_ERROR</c>.
    /// </summary>
    public void Fixme(bool condition = true, string? reason = null)
    {
        if (E2E.Internal.Skip.FixmeRunningTest(CurrentSession, condition, reason) is { } skip)
        {
            Skip.Test(skip.Reason);
        }
    }

    /// <summary>
    /// Changes the running test's timeout from its body or a set-up, counted from the start of the attempt like the configured
    /// <c>timeout</c>, which it replaces for this attempt (upstream's <c>test.setTimeout(timeout)</c>). A timeout shorter than the time already
    /// spent times the test out at once. A timeout that is not a positive whole number of milliseconds, or above 2147483647, and a call from code
    /// the test left running after its attempt ended are test errors, <c>INVALID_ARGUMENT</c>. A call outside a test throws
    /// <see cref="InvalidOperationException"/> where upstream has <c>COLLECTION_ERROR</c>. The port cannot tell a teardown from the body here:
    /// a call from one is refused only once the session has completed.
    /// </summary>
    public void SetTimeout(TimeSpan timeout)
    {
        var attempt = Attempts.CurrentAttempt()
            ?? throw new InvalidOperationException(
                "SetTimeout(timeout) must be called inside a test body or a set-up; to set a test's timeout ahead of time, set the timeout in the configuration"
            );
        E2E.Internal.TestTimeout.SetRunningTestTimeout(attempt, timeout);
    }

    [Before(HookType.Test)]
    public async Task StartE2ESessionAsync(TUnitContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        SessionStartToken = cancellationToken;
        var details = context.Metadata.TestDetails;
        // TUnit reuses one context for every [Retry] attempt, so what an earlier attempt's body did must not read as this attempt's.
        LooksBeforeTeardownExecutor.Forget(context);
        RunSelection.SkipIfLeftOut(GetType(), details.MethodName);
        RunSelection.SkipIfSetupRan(GetType(), details.MethodName);
        var started = StartSessionAsync(
            CacheTitle(context),
            context.Execution.CurrentRetryAttempt + 1,
            null,
            () => LooksBeforeTeardownExecutor.OutcomeOf(context) is { Failure: not null },
            cancellationToken,
            TestMethodOf(details)
        );

        // StartSessionAsync reserved the slot a host the test body launches finds its attempt through, in this hook's flow; TUnit hands the body only
        // the async-local values a hook asks it to.
        context.AddAsyncLocalValues();
        await started.ConfigureAwait(false);

        // A test registered with [Fixme] skips once its session has started, as a Fixme() from a hook does: the report tells the cause fixme.
        if (TestMethodOf(details)?.GetCustomAttribute<FixmeAttribute>(true) is { } fixme)
        {
            var skip = E2E.Internal.Skip.FixmeRunningTest(CurrentSession, true, fixme.Reason)!;

            // The session is completed here: the outcome TUnit hands the After hook does not tell a skip from a hook that failed.
            if (TakeSession() is { } session)
            {
                try
                {
                    session.Complete(new SkipException(skip.Reason), cancellationToken);
                }
                finally
                {
                    await session.DisposeAsync().ConfigureAwait(false);
                }
            }

            Skip.Test(skip.Reason);
        }
    }

    /// <summary>
    /// The base class hook runs after the derived ones, so a teardown has run. It collects the soft failures and a handler failure
    /// the teardown left, and hands the session to <see cref="OnTestEnd"/>: TUnit sets the test result, which holds a failure of a
    /// derived <c>[After(Test)]</c> hook, only after every hook has run.
    /// </summary>
    [After(HookType.Test)]
    public async Task FinishE2ESessionAsync(TUnitContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var session = TakeSession();
        if (session is null)
        {
            return;
        }

        // From here the session is released by OnTestEnd, whatever this hook does.
        var pending = _pending = new Pending(session, cancellationToken);
        pending.Outcome = LooksBeforeTeardownExecutor.OutcomeOf(context);
        pending.Soft = pending.Outcome?.Soft ?? session.CloseSoftFailures();

        // A handler that failed during the teardown, with no step after it, fails the attempt before the verdict.
        try
        {
            await session.SettleAttemptAsync(cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Reported below, after the session is released.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            pending.Late = ex;
        }

        // A setup test that passed saves its sessions; a contract it broke fails it, and its result stands as the verdict.
        if (pending.Late is null && pending.Outcome is { Failure: null, Skipped: null } && pending.Soft is null)
        {
            try
            {
                await session.FinishSetupAsync(true, cancellationToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Reported below, after the session is released.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                pending.Late = ex;
            }
        }

        if (pending.Soft is not null)
        {
            throw pending.Soft;
        }

        if (pending.Late is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(pending.Late);
        }
    }

    /// <summary>
    /// Runs after every hook, when the test result holds what a derived <c>[After(Test)]</c> hook threw: the verdict is the test body's, the soft
    /// failures', or the result's own, and then the session is completed and disposed.
    /// </summary>
    public int Order => int.MinValue;

    /// <inheritdoc cref="Order"/>
    public EventReceiverStage Stage => EventReceiverStage.Late;

    /// <summary>.NET-only: starts the likeliest tests first, in the priority of the relevance band that <see cref="E2ERunFixture"/> gave them (see <c>RunSelection.RankRegistered</c>).</summary>
    ValueTask ITestRegisteredEventReceiver.OnTestRegistered(TestRegisteredContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RunSelection.RankRegistered(context.TestContext);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc cref="Order"/>
    public async ValueTask OnTestEnd(TUnitContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await FinishPendingAsync(context.Execution.Result).ConfigureAwait(false);
    }

    private async Task FinishPendingAsync(TestResult? result)
    {
        var pending = Interlocked.Exchange(ref _pending, null);
        if (pending is null)
        {
            return;
        }

        try
        {
            CompletedWith = ErrorForCache(pending.Outcome, pending.Soft, result) ?? pending.Late;
            pending.Session.Complete(CompletedWith, pending.CancellationToken);
        }
        finally
        {
            await pending.Session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private Pending? _pending;

    /// <summary>.NET-only, for the tests of this adapter: the error the session was completed with (the cache's verdict), or null for a pass.</summary>
    internal Exception? CompletedWith { get; private set; }

    private sealed class Pending(E2ESession session, CancellationToken cancellationToken)
    {
        public E2ESession Session { get; } = session;

        public CancellationToken CancellationToken { get; } = cancellationToken;

        public TestException? Soft { get; set; }

        public TestOutcome? Outcome { get; set; }

        public Exception? Late { get; set; }
    }

    // TUnit hands out its own method metadata, so the reflection method is found by name and parameter types.
    private static System.Reflection.MethodBase? TestMethodOf(TestDetails details)
    {
        var types = details.MethodMetadata.Parameters.Select(parameter => parameter.Type).ToArray();
        for (var type = details.ClassType; type is not null; type = type.BaseType)
        {
            var found = type.GetMethods(
                    System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.Static
                        | System.Reflection.BindingFlags.DeclaredOnly
                )
                .FirstOrDefault(method =>
                    string.Equals(method.Name, details.MethodName, StringComparison.Ordinal)
                    && method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(types)
                );
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    internal Task SettleLateAsync(CancellationToken cancellationToken) => SettleAttemptAsync(cancellationToken);

    internal Task CaptureEvidenceOf(Exception failure) => CaptureFailureEvidenceAsync(failure);

    internal static Exception? ErrorForCache(TestOutcome? outcome, TestException? soft, TestResult? result)
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
            if (soft is not null)
            {
                return soft;
            }

            // The body passed or skipped, and TUnit's result is the body's with the hooks' failures added: a derived [After(Test)] hook that threw fails it.
            if (result is { State: TestState.Failed or TestState.Timeout })
            {
                return ResultFailure(result);
            }

            return result is { State: TestState.Cancelled }
                    ? result.Exception as OperationCanceledException ?? new OperationCanceledException()
                : outcome.Skipped is { } reason ? new SkipException(reason)
                : null;
        }

        return failure is OperationCanceledException
            ? failure
            : failure as TestException
                ?? new TestException(
                    "TEST_FAILED",
                    string.IsNullOrWhiteSpace(failure.Message) ? "The test failed." : failure.Message
                );
    }

    private static Exception ResultFailure(TestResult result) =>
        result.Exception as TestException
        ?? new TestException(
            "TEST_FAILED",
            string.IsNullOrWhiteSpace(result.Exception?.Message)
                ? "A hook of the test failed."
                : result.Exception.Message
        );
}
