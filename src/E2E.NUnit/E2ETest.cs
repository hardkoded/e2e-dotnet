// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using E2E.Internal;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace E2E.NUnit;

/// <summary>
/// Base class for an NUnit test that drives the app. Setup starts one engine
/// session. Teardown commits the replay cache from the NUnit result: a pass
/// or a skip records verified acts, a failure evicts the unverified ones it
/// recorded or replayed, and a cancelled test leaves the cache alone. The first
/// attempt can replay. <c>[Retry]</c> attempts run live and still record. Each
/// <c>[Repeat]</c> iteration is a first attempt. A check in a derived [TearDown]
/// after a failure verifies nothing.
/// Each <c>Expect.Soft</c> failure is recorded on the NUnit result, as inside
/// <c>Assert.EnterMultipleScope</c>, so the test fails when its body ends and
/// lists every soft failure. A soft failure outranks a skip: <c>Assert.Ignore</c> or
/// <c>Assert.Inconclusive</c> after one fails the test, and the skip reason stays in the message.
/// Settings come from <see cref="E2EFixture"/>.
/// </summary>
[LooksBeforeTeardown]
public abstract partial class E2ETest : E2EFixture
{
    /// <summary>
    /// The test's title: its cache identity, and the <c>Test:</c> line every prompt's context opens with. The default is the NUnit
    /// full name, so each test keeps its own replay.
    /// </summary>
    protected virtual string CacheTitle(global::NUnit.Framework.TestContext.TestAdapter test)
    {
        ArgumentNullException.ThrowIfNull(test);
        return string.IsNullOrWhiteSpace(test.FullName) ? test.Name : test.FullName;
    }

    /// <summary>Records an <c>Expect.Soft</c> failure on the running NUnit test. The test body runs on and fails when it ends.</summary>
    protected virtual void RecordSoftFailure(TestException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        TestExecutionContext.CurrentContext.CurrentResult.RecordAssertion(
            AssertionStatus.Failed,
            failure.Message,
            failure.StackTrace
        );
    }

    /// <summary>
    /// Marks the running test expected to fail when <paramref name="condition"/> holds (upstream's <c>test.fail(condition, reason)</c>).
    /// It does not throw: the body runs on, and the verdict follows <c>settleExpectedFailure</c>. A blank <paramref name="reason"/> becomes
    /// <c>expected to fail</c>. A mark from a teardown, a setup test, or a serial group member is a test error, <c>INVALID_ARGUMENT</c>.
    /// </summary>
    public void Fail(bool condition = true, string? reason = null) =>
        ExpectedFailureMarks.MarkRunningTestExpectedToFail(CurrentSession, condition, reason, IsSerial, _bodyDone);

    /// <summary>
    /// Skips the running test as fixme when <paramref name="condition"/> holds (upstream's <c>test.fixme(condition, reason)</c>). The skip reports
    /// the cause <c>fixme</c> and the reason <c>fixme</c>, or <c>fixme: &lt;reason&gt;</c> for a reason that is not blank. Like <c>Assert.Ignore</c>
    /// it ends the test method at once. A call from a setup test is a test error, <c>INVALID_ARGUMENT</c>; a call outside a test throws
    /// <see cref="InvalidOperationException"/> where upstream has <c>COLLECTION_ERROR</c>.
    /// </summary>
    public void Fixme(bool condition = true, string? reason = null)
    {
        if (E2E.Internal.Skip.FixmeRunningTest(CurrentSession, condition, reason) is { } skip)
        {
            Assert.Ignore(skip.Reason);
        }
    }

    /// <summary>
    /// Changes the running test's timeout from its body or a <c>[SetUp]</c>, counted from the start of the attempt like the configured
    /// <c>timeout</c>, which it replaces for this attempt (upstream's <c>test.setTimeout(timeout)</c>). A timeout shorter than the time already
    /// spent times the test out at once. A timeout that is not a positive whole number of milliseconds, or above 2147483647, a call from a teardown (after the test method returned), and one
    /// from code the test left running after it ended are test errors, <c>INVALID_ARGUMENT</c>. A call outside a test throws
    /// <see cref="InvalidOperationException"/> where upstream has <c>COLLECTION_ERROR</c>.
    /// </summary>
    public void SetTimeout(TimeSpan timeout)
    {
        var attempt = Attempts.CurrentAttempt()
            ?? throw new InvalidOperationException(
                "SetTimeout(timeout) must be called inside a test body or a [SetUp]; to set a test's timeout ahead of time, set the timeout in the configuration"
            );
        E2E.Internal.TestTimeout.SetRunningTestTimeout(attempt, timeout, _bodyDone);
    }

    /// <summary>Set once the test method returned: what runs after it (a derived [TearDown]) comes after the verdict is reached.</summary>
    internal void BodyDone() => _bodyDone = true;

    private bool _bodyDone;

    // Not async: StartSessionAsync reserves the slot a host the test body launches finds its attempt through, in the flow of its caller. An
    // async [SetUp] would keep the slot to itself, and NUnit runs the body in the flow of the method it calls, not in the one an async method ends in.
    [SetUp]
    public Task StartE2ESessionAsync()
    {
        _bodyDone = false;
        var current = global::NUnit.Framework.TestContext.CurrentContext;
        if (RunSelection.ReasonFor(GetType(), current.Test.MethodName) is { } leftOut)
        {
            Assert.Ignore(leftOut);
        }

        if (SetupDependencies.HasRun(GetType(), current.Test.MethodName))
        {
            Assert.Ignore("the setup test already ran for a test that restores its session");
        }

        RejectParallelChildren(current.Test.MethodName);
        if (IsSerial)
        {
            // Upstream refuses a skip inside a serial group at collection (COLLECTION_ERROR); an unfiltered run does not collect, so it is refused here, ahead of the session handover.
            if (
                TestExecutionContext.CurrentContext.CurrentTest.Method?.MethodInfo.IsDefined(
                    typeof(FixmeAttribute),
                    true
                ) == true
            )
            {
                throw new TestException(
                    "COLLECTION_ERROR",
                    "[Fixme] cannot be used inside a serial group, which skips as one unit; set skip on the serial describe instead"
                );
            }

            RejectInstancePerTest();
            if (
                TestExecutionContext.CurrentContext.CurrentTest.Method is { } member
                && RunSelection.FailErrorOf(GetType(), member.MethodInfo) is { } failError
            )
            {
                // Upstream refuses fail on, inside, or around a serial group at collection (COLLECTION_ERROR); an unfiltered run does not collect, so it is refused here.
                throw new TestException("COLLECTION_ERROR", failError);
            }

            if (_serialFailure is not null)
            {
                Assert.Ignore("a previous test of the serial group failed");
            }

            if (HasSession)
            {
                HandOverAttempt();
                return Task.CompletedTask;
            }
        }

        var method = TestExecutionContext.CurrentContext.CurrentTest.Method?.MethodInfo;
        var started = StartSessionAsync(
            CacheTitle(current.Test),
            AttemptOf(current),
            RecordSoftFailure,
            HasFailed,
            current.CancellationToken,
            method
        );
        return method?.GetCustomAttribute<FixmeAttribute>(true) is { } fixme
            ? FixmeAfterAsync(started, fixme)
            : started;
    }

    // A test registered with [Fixme] skips once its session has started, as a Fixme() from a [SetUp] does: the report tells the cause fixme.
    private async Task FixmeAfterAsync(Task started, FixmeAttribute fixme)
    {
        await started.ConfigureAwait(false);
        Fixme(true, fixme.Reason);
    }

    [TearDown]
    public async Task FinishE2ESessionAsync()
    {
        if (IsSerial)
        {
            await FinishSerialMemberAsync().ConfigureAwait(false);
            return;
        }

        var session = TakeSession();
        if (session is null)
        {
            return;
        }

        var skipped = FailSkippedAfterSoftFailure();
        var current = global::NUnit.Framework.TestContext.CurrentContext;
        KeepSkipBesideFailure(session);

        // A derived [TearDown] that threw ran before this one: NUnit keeps its failure in the result, which the report tells as an afterEach secondary error.
        if (HookFailureOf(current.Result) is { } hookFailure)
        {
            session.RecordHookFailure(hookFailure);
        }

        // A handler that failed during the teardown, with no step after it, fails the attempt before the verdict.
        Exception? late = null;
        try
        {
            await session.SettleAttemptAsync(current.CancellationToken).ConfigureAwait(false);
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

        // Upstream's enter('afterEach'): the rest of the teardown runs under a budget of its own, so a body its timeout
        // cut short can still drive the session.
        session.EnterTeardownBudget(current.CancellationToken);

        // A setup test that passed saves its sessions; a contract it broke fails it, and its result stands as the verdict.
        if (current.Result.Outcome.Status == global::NUnit.Framework.Interfaces.TestStatus.Passed)
        {
            try
            {
                await session.FinishSetupAsync(true, current.CancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The run was cancelled: the test's own result stands.
            }
#pragma warning disable CA1031 // Reported below, after the session is released.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                late ??= ex;
            }
        }

        // A test marked as expected to fail (the NUnit property E2E.Fail) has its verdict settled with the attempt.
        if (
            !skipped
            && NUnitSessionTestRunner.ExpectedFailureOf(TestExecutionContext.CurrentContext.CurrentTest) is { } mark
        )
        {
            session.ExpectedFailure = mark;
        }

        // A host that asked for this test (e2e mcp's open_session {test}) keeps the session open: it completes and disposes it at close.
        if (
            EngineInUse is { } engine
            && SessionHandover.TryOffer(
                current.Test.FullName,
                session,
                engine,
                session.VerdictOf(ErrorForCache(current.Result) ?? late, current.CancellationToken)
            )
        )
        {
            return;
        }

        try
        {
            session.Complete(ErrorForCache(current.Result) ?? late, current.CancellationToken);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }

        if (session.SettledMark is { } settled)
        {
            // A product failure the mark covers passes; a pass fails with UNEXPECTED_PASS (upstream's settleExpectedFailure).
            if (settled.Error is not null)
            {
                TestExecutionContext.CurrentContext.CurrentResult.SetResult(ResultState.Success);
                late = null;
            }
            else if (session.SettledError is TestException { Code: "UNEXPECTED_PASS" } unexpected)
            {
                ExceptionDispatchInfo.Throw(unexpected);
            }
        }

        // A test that failed already keeps its first failure as the verdict.
        if (late is not null && current.Result.Outcome.Status != global::NUnit.Framework.Interfaces.TestStatus.Failed)
        {
            ExceptionDispatchInfo.Throw(late);
        }
    }

    /// <summary>
    /// Ends the serial group: completes the session the members shared with the first failure of the group, if any, and disposes it.
    /// </summary>
    [OneTimeTearDown]
    public async Task FinishE2ESerialGroupAsync()
    {
        var session = TakeSession();
        if (session is null)
        {
            return;
        }

        try
        {
            session.Complete(_serialFailure, global::NUnit.Framework.TestContext.CurrentContext.CancellationToken);
        }
        finally
        {
            _serialFailure = null;
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    internal Task CaptureEvidenceOf(Exception failure) => CaptureFailureEvidenceAsync(failure);

    internal Task SettleLate() => SettleAttemptAsync();

    private Exception? _serialFailure;

    private bool IsSerial => GetType().IsDefined(typeof(SerialAttribute), true);

    // A member of a serial group settles its own attempt and leaves the session open for the next member; the first failure of the group is the verdict.
    private async Task FinishSerialMemberAsync()
    {
        if (!HasSession)
        {
            return;
        }

        FailSkippedAfterSoftFailure();
        var current = global::NUnit.Framework.TestContext.CurrentContext;
        if (CurrentSession is { } session)
        {
            KeepSkipBesideFailure(session);
        }

        Exception? late = null;
        try
        {
            await SettleAttemptAsync(current.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The run was cancelled: the test's own result stands.
        }
#pragma warning disable CA1031 // Reported below, after the member is recorded.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            late = ex;
        }

        // A member that failed or errored ends the group: the later members skip, and the session commits as failed. A skip does not.
        var memberError = ErrorForCache(current.Result) ?? late;
        if (memberError is not null and not SkipException)
        {
            _serialFailure ??= memberError;
        }

        if (late is not null && current.Result.Outcome.Status != global::NUnit.Framework.Interfaces.TestStatus.Failed)
        {
            ExceptionDispatchInfo.Throw(late);
        }
    }

    private void RejectInstancePerTest()
    {
        if (
            GetType()
                .GetCustomAttributes(typeof(FixtureLifeCycleAttribute), true)
                .Cast<FixtureLifeCycleAttribute>()
                .Any(attribute => attribute.LifeCycle == LifeCycle.InstancePerTestCase)
        )
        {
            throw new InvalidOperationException(
                $"{GetType().Name} is [Serial], so its tests share one fixture instance and one session: remove [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]."
            );
        }
    }

    // NUnit shares one fixture instance among its tests, and the session lives on that instance.
    // Tests that run in parallel on one instance would overwrite each other's session.
    private void RejectParallelChildren(string? methodName)
    {
        var type = GetType();
        var methods = methodName is null
            ? []
            : type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static
                )
                .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
                .ToArray();
        var sources = new List<ICustomAttributeProvider> { type, type.Assembly };
        sources.AddRange(methods);

        // A fixture or an assembly that lets children run in parallel, or a test method that opts in itself,
        // unless the fixture or the method opts out.
        var parallel =
            sources.Any(source =>
                source
                    .GetCustomAttributes(typeof(ParallelizableAttribute), true)
                    .Cast<ParallelizableAttribute>()
                    .Any(attribute => (attribute.Scope & ParallelScope.Children) != 0)
            )
            || methods.Any(method =>
                method
                    .GetCustomAttributes(typeof(ParallelizableAttribute), true)
                    .Cast<ParallelizableAttribute>()
                    .Any(attribute => (attribute.Scope & ParallelScope.Self) != 0)
            );
        var fixtureOptedOut =
            type.IsDefined(typeof(NonParallelizableAttribute), true)
            && !type.GetCustomAttributes(typeof(ParallelizableAttribute), true)
                .Cast<ParallelizableAttribute>()
                .Any(attribute => (attribute.Scope & ParallelScope.Children) != 0);
        var optedOut =
            fixtureOptedOut
            || (
                methods.Length > 0 && methods.All(method => method.IsDefined(typeof(NonParallelizableAttribute), true))
            );
        var perTest = sources.Any(source =>
            source
                .GetCustomAttributes(typeof(FixtureLifeCycleAttribute), true)
                .Cast<FixtureLifeCycleAttribute>()
                .Any(attribute => attribute.LifeCycle == LifeCycle.InstancePerTestCase)
        );
        if (parallel && !perTest && !optedOut)
        {
            throw new InvalidOperationException(
                $"{type.Name} runs its tests in parallel on one fixture instance, so they would share one E2E session. "
                    + "Use [Parallelizable(ParallelScope.Fixtures)] to run fixtures in parallel, or add [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]."
            );
        }
    }

    // NUnit reports a test that skipped itself as skipped, whatever it recorded before.
    // A failed Expect.Soft before the skip fails the test, as upstream does. Returns whether the test skipped itself: the mark drops with the skip,
    // also beside a failure (upstream's settleExpectedFailure returns early on a skip).
    private static bool FailSkippedAfterSoftFailure()
    {
        var result = TestExecutionContext.CurrentContext.CurrentResult;
        if (result.ResultState.Status is not (TestStatus.Skipped or TestStatus.Inconclusive))
        {
            return false;
        }

        if (result.AssertionResults.Any(assertion => assertion.Status == AssertionStatus.Failed))
        {
            // NUnit's message already lists the soft failures and the skip reason.
            result.SetResult(ResultState.Failure, result.Message, result.StackTrace);
        }

        return true;
    }

    // A failure outranks a skip, and the report keeps the skip beside it, as for Fixme (upstream's execute.ts keeps the runtime skip on a failed attempt).
    // NUnit's host skip message is not carried to the session, but a result with several outcomes lists the skip as an inconclusive assertion result.
    private static void KeepSkipBesideFailure(E2ESession session)
    {
        var result = TestExecutionContext.CurrentContext.CurrentResult;

        // A skip that decides the test keeps the host's own message.
        if (session.RuntimeSkip is not null || result.ResultState.Status != TestStatus.Failed)
        {
            return;
        }

        var skip = result.AssertionResults.FirstOrDefault(assertion =>
            assertion.Status == AssertionStatus.Inconclusive
        );
        if (skip is not null)
        {
            session.RuntimeSkip = RuntimeSkip.Create(skip.Message, "explicit");
        }
    }

    // NUnit counts both [Retry] and [Repeat] in CurrentRepeatCount. Only a retry is a later attempt.
    private int AttemptOf(global::NUnit.Framework.TestContext current)
    {
        var name = current.Test.MethodName;
        var retried =
            name is not null
            && GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Any(method =>
                    string.Equals(method.Name, name, StringComparison.Ordinal)
                    && method.IsDefined(typeof(RetryAttribute), true)
                );
        return retried ? current.CurrentRepeatCount + 1 : 1;
    }

    // A derived [TearDown] runs before this fixture's teardown, after the result of the test body is known.
    private static bool HasFailed()
    {
        return global::NUnit.Framework.TestContext.CurrentContext.Result.Outcome.Status
            == global::NUnit.Framework.Interfaces.TestStatus.Failed;
    }

    // NUnit writes the failure of a [TearDown] into the result's message as "TearDown : <exception>", beside the body's own failure when there is
    // one ("Multiple failures", each numbered), and its stack trace follows a "--TearDown" line.
    private static Exception? HookFailureOf(global::NUnit.Framework.TestContext.ResultAdapter result)
    {
        if (result.Outcome.Site != global::NUnit.Framework.Interfaces.FailureSite.TearDown)
        {
            return null;
        }

        var found = HookFailure().Match(result.Message ?? "");
        return found.Success ? new TestException("TEST_FAILED", found.Groups["text"].Value.Trim()) : null;
    }

    [GeneratedRegex(
        @"^(?:\s*\d+\)\s*)?TearDown : (?<text>.*?)(?=\r?\n\s*--TearDown|\z)",
        RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex HookFailure();

    private static Exception? ErrorForCache(global::NUnit.Framework.TestContext.ResultAdapter result)
    {
        var recorded = result.RecordedException;
        return result.Outcome.Status switch
        {
            global::NUnit.Framework.Interfaces.TestStatus.Passed
            or global::NUnit.Framework.Interfaces.TestStatus.Warning => null,
            global::NUnit.Framework.Interfaces.TestStatus.Skipped
            or global::NUnit.Framework.Interfaces.TestStatus.Inconclusive => recorded as SkipException
                ?? new SkipException(string.IsNullOrWhiteSpace(result.Message) ? "skipped" : result.Message),
            _ => recorded
                ?? new TestException(
                    "TEST_FAILED",
                    string.IsNullOrWhiteSpace(result.Message) ? "The test failed." : result.Message
                ),
        };
    }
}
