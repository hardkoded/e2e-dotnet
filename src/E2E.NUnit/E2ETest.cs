// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Runtime.ExceptionServices;
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
public abstract class E2ETest : E2EFixture
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
        TestExecutionContext.CurrentContext.CurrentResult.RecordAssertion(AssertionStatus.Failed, failure.Message, failure.StackTrace);
    }

    [SetUp]
    public async Task StartE2ESessionAsync()
    {
        var current = global::NUnit.Framework.TestContext.CurrentContext;
        RejectParallelChildren(current.Test.MethodName);
        await StartSessionAsync(CacheTitle(current.Test), AttemptOf(current), RecordSoftFailure, HasFailed, current.CancellationToken, TestExecutionContext.CurrentContext.CurrentTest.Method?.MethodInfo).ConfigureAwait(false);
    }

    [TearDown]
    public async Task FinishE2ESessionAsync()
    {
        var session = TakeSession();
        if (session is null)
        {
            return;
        }

        FailSkippedAfterSoftFailure();
        var current = global::NUnit.Framework.TestContext.CurrentContext;

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

        // A host that asked for this test (e2e mcp's open_session {test}) keeps the session open: it completes and disposes it at close.
        if (EngineInUse is { } engine && SessionHandover.TryOffer(current.Test.FullName, session, engine, ErrorForCache(current.Result) ?? late))
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

        // A test that failed already keeps its first failure as the verdict.
        if (late is not null && current.Result.Outcome.Status != global::NUnit.Framework.Interfaces.TestStatus.Failed)
        {
            ExceptionDispatchInfo.Throw(late);
        }
    }

    internal Task CaptureEvidenceOf(Exception failure) => CaptureFailureEvidenceAsync(failure);

    internal Task SettleLate() => SettleAttemptAsync();

    // NUnit shares one fixture instance among its tests, and the session lives on that instance.
    // Tests that run in parallel on one instance would overwrite each other's session.
    private void RejectParallelChildren(string? methodName)
    {
        var type = GetType();
        var methods = methodName is null
            ? []
            : type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
                .ToArray();
        var sources = new List<ICustomAttributeProvider> { type, type.Assembly };
        sources.AddRange(methods);

        // A fixture or an assembly that lets children run in parallel, or a test method that opts in itself,
        // unless the fixture or the method opts out.
        var parallel = sources.Any(source => source.GetCustomAttributes(typeof(ParallelizableAttribute), true).Cast<ParallelizableAttribute>().Any(attribute => (attribute.Scope & ParallelScope.Children) != 0))
            || methods.Any(method => method.GetCustomAttributes(typeof(ParallelizableAttribute), true).Cast<ParallelizableAttribute>().Any(attribute => (attribute.Scope & ParallelScope.Self) != 0));
        var fixtureOptedOut = type.IsDefined(typeof(NonParallelizableAttribute), true)
            && !type.GetCustomAttributes(typeof(ParallelizableAttribute), true).Cast<ParallelizableAttribute>().Any(attribute => (attribute.Scope & ParallelScope.Children) != 0);
        var optedOut = fixtureOptedOut || (methods.Length > 0 && methods.All(method => method.IsDefined(typeof(NonParallelizableAttribute), true)));
        var perTest = sources.Any(source => source.GetCustomAttributes(typeof(FixtureLifeCycleAttribute), true).Cast<FixtureLifeCycleAttribute>().Any(attribute => attribute.LifeCycle == LifeCycle.InstancePerTestCase));
        if (parallel && !perTest && !optedOut)
        {
            throw new InvalidOperationException(
                $"{type.Name} runs its tests in parallel on one fixture instance, so they would share one E2E session. " +
                "Use [Parallelizable(ParallelScope.Fixtures)] to run fixtures in parallel, or add [FixtureLifeCycle(LifeCycle.InstancePerTestCase)].");
        }
    }

    // NUnit reports a test that skipped itself as skipped, whatever it recorded before.
    // A failed Expect.Soft before the skip fails the test, as upstream does.
    private static void FailSkippedAfterSoftFailure()
    {
        var result = TestExecutionContext.CurrentContext.CurrentResult;
        if (result.ResultState.Status is not (TestStatus.Skipped or TestStatus.Inconclusive))
        {
            return;
        }

        if (result.AssertionResults.Any(assertion => assertion.Status == AssertionStatus.Failed))
        {
            // NUnit's message already lists the soft failures and the skip reason.
            result.SetResult(ResultState.Failure, result.Message, result.StackTrace);
        }
    }

    // NUnit counts both [Retry] and [Repeat] in CurrentRepeatCount. Only a retry is a later attempt.
    private int AttemptOf(global::NUnit.Framework.TestContext current)
    {
        var name = current.Test.MethodName;
        var retried = name is not null && GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Any(method => string.Equals(method.Name, name, StringComparison.Ordinal) && method.IsDefined(typeof(RetryAttribute), true));
        return retried ? current.CurrentRepeatCount + 1 : 1;
    }

    // A derived [TearDown] runs before this fixture's teardown, after the result of the test body is known.
    private static bool HasFailed()
    {
        return global::NUnit.Framework.TestContext.CurrentContext.Result.Outcome.Status == global::NUnit.Framework.Interfaces.TestStatus.Failed;
    }

    private static Exception? ErrorForCache(global::NUnit.Framework.TestContext.ResultAdapter result)
    {
        var recorded = result.RecordedException;
        return result.Outcome.Status switch
        {
            global::NUnit.Framework.Interfaces.TestStatus.Passed or global::NUnit.Framework.Interfaces.TestStatus.Warning => null,
            global::NUnit.Framework.Interfaces.TestStatus.Skipped or global::NUnit.Framework.Interfaces.TestStatus.Inconclusive => recorded as SkipException ?? new SkipException(string.IsNullOrWhiteSpace(result.Message) ? "skipped" : result.Message),
            _ => recorded ?? new TestException("TEST_FAILED", string.IsNullOrWhiteSpace(result.Message) ? "The test failed." : result.Message),
        };
    }
}
