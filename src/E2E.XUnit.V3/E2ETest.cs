// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Reflection;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace E2E.XUnit.V3;

/// <summary>
/// Base class for an xUnit v3 test that drives the app. <see cref="InitializeAsync"/> starts one
/// engine session. <see cref="DisposeAsync"/> commits the replay cache from the xUnit result: a
/// pass or a skip records verified acts, a failure evicts the unverified ones it recorded or
/// replayed, and a cancelled test leaves the cache alone. xUnit has no retry, so every test is a
/// first attempt and can replay. A check in a derived <c>DisposeAsync</c> after a failure verifies nothing.
/// Each <c>Expect.Soft</c> failure is kept on the session. The test body runs on, and the test
/// fails when it is disposed, with every soft failure in one message.
/// Settings come from <see cref="E2EFixture"/>.
/// </summary>
[LooksBeforeTeardown]
[TestMethodOrderer(typeof(SetupTestMethodOrderer))]
public abstract class E2ETest : E2EFixture, IAsyncLifetime
{
    // The attribute that looks before the teardown gets the test, not the instance.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, E2ETest> Running = new(
        StringComparer.Ordinal
    );

    // A run that registers no E2ERunFixture ends here, so the sessions xUnit abandoned are still completed.
    static E2ETest() => AppDomain.CurrentDomain.ProcessExit += (_, _) => CompleteAbandonedAtExit();

    private static void CompleteAbandonedAtExit()
    {
        try
        {
            CompleteAbandonedAsync().Wait(TimeSpan.FromSeconds(30));
        }
#pragma warning disable CA1031 // The process is ending: a cleanup failure must not change the exit code.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Console.Error.WriteLine("E2E: completing abandoned sessions failed: " + ex.Message);
        }
    }

    /// <summary>
    /// The test's title: its cache identity, and the <c>Test:</c> line every prompt's context opens with.
    /// The default is the class and method name, with every theory
    /// argument in full, so each test and theory row keeps its own replay. The xUnit display name
    /// is not used, because xUnit shortens long arguments in it and a display setting can change it.
    /// </summary>
    protected virtual string CacheTitle(ITest test)
    {
        ArgumentNullException.ThrowIfNull(test);
        var name = test.TestCase.TestClassName + "." + test.TestCase.TestMethodName;
        return test is IXunitTest { TestMethodArguments.Length: > 0 } row
            ? name
                + "("
                + string.Join(
                    ", ",
                    row.TestMethodArguments.Select(argument =>
                        FormatArgument(argument, row.TestMethodArguments.Length > 1)
                    )
                )
                + ")"
            : name;
    }

    // A lazy or self-containing collection must not hang the title.
    private const int MaxTitleDepth = 4;
    private const int MaxTitleItems = 1000;

    // A simple argument keeps its plain text. A null, a collection, or a string that could pass for
    // another row's argument list is written so that no two rows share a title. A comma only matters
    // when the row has several arguments, where ("a, b") and ("a", "b") would read alike.
    private static string FormatArgument(object? argument, bool quoteComma, int depth = 0)
    {
        return argument switch
        {
            null => "null",
            string text => text.Contains('"', StringComparison.Ordinal)
            || text.Contains('\\', StringComparison.Ordinal)
            || text.StartsWith('[')
            || text == "null"
            || (quoteComma && text.Contains(',', StringComparison.Ordinal))
                ? "\""
                    + text.Replace("\\", "\\\\", StringComparison.Ordinal)
                        .Replace("\"", "\\\"", StringComparison.Ordinal)
                    + "\""
                : text,
            System.Collections.IEnumerable items when depth < MaxTitleDepth => "["
                + string.Join(
                    ", ",
                    items.Cast<object?>().Take(MaxTitleItems).Select(item => FormatArgument(item, true, depth + 1))
                )
                + "]",
            _ => Convert.ToString(argument, CultureInfo.InvariantCulture) ?? "null",
        };
    }

    /// <summary>
    /// Skips the running test as fixme when <paramref name="condition"/> holds (upstream's <c>test.fixme(condition, reason)</c>). The skip reports
    /// the cause <c>fixme</c> and the reason <c>fixme</c>, or <c>fixme: &lt;reason&gt;</c> for a reason that is not blank. Like <c>Assert.Skip</c>
    /// it ends the test method at once. A call from a setup test fails it with the message "Fixme() cannot skip a setup test" (the report code is <c>INVALID_ARGUMENT</c>); a call outside a test throws
    /// <see cref="InvalidOperationException"/> where upstream has <c>COLLECTION_ERROR</c>.
    /// </summary>
    public void Fixme(bool condition = true, string? reason = null)
    {
        try
        {
            if (E2E.Internal.Skip.FixmeRunningTest(CurrentSession, condition, reason) is { } skip)
            {
                throw new InvalidOperationException(RunSelection.DynamicSkipToken + skip.Reason);
            }
        }
        catch (TestException refused)
        {
            // xUnit keeps only a failure's type and message, so the refusal is kept here for the report to read its code.
            _refused = refused;
            throw;
        }
    }

    private TestException? _refused;

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

    // A derived fixture that overrides this must not make the override async around the base call, or the slot is lost to the body.
    // Not async: StartSessionAsync reserves the slot a host the test body launches finds its attempt through, in the flow of its caller, and xUnit
    // runs the body in the flow it called this method from. An async method would keep the slot to itself.
    public virtual ValueTask InitializeAsync()
    {
        var current = Xunit.TestContext.Current;
        var test = current.Test
            ?? throw new InvalidOperationException("The E2E session starts inside a running xUnit test.");
        RunSelection.SkipIfLeftOut(GetType(), test.TestCase.TestMethodName);
        RunSelection.SkipIfSetupRan(GetType(), test.TestCase.TestMethodName);
        var method = (test.TestCase as IXunitTestCase)?.TestMethod?.Method;
        if (method?.GetCustomAttribute<FixmeAttribute>(true) is { } fixme)
        {
            return new ValueTask(SkipFixmeWithoutSessionAsync(method, fixme, current.CancellationToken));
        }

        var started = StartSessionAsync(CacheTitle(test), 1, null, HasFailed, current.CancellationToken, method);
        return new ValueTask(RegisterRunningAsync(started, test.UniqueID, method!, current.CancellationToken));
    }

    // The setup test that saves the session this test restores failed: the test is reported skipped, as upstream's scheduler does.
    private async Task SkipSetupFailedAsync(
        SetupFailedException failure,
        MethodInfo method,
        CancellationToken cancellationToken
    )
    {
        var discovered = RunSelection
            .Discover([GetType()], GetType().Assembly, includeExplicit: true)
            .FirstOrDefault(test => test.Title == method.Name);
        if (discovered is not null)
        {
            await HostTestFilters
                .ReportSetupFailedAsync(Config, discovered, failure, ResultsDirectory, cancellationToken)
                .ConfigureAwait(false);
        }

        RunSelection.SkipWith(E2E.Units.SetupFailedSkip(failure.Session, failure.SetupTestId).Reason);
    }

    // A test registered with [Fixme] that the run fixture did not collect (an [Explicit] fixture runs alone) is reported here, and skips before its session starts.
    private async Task SkipFixmeWithoutSessionAsync(
        MethodInfo method,
        FixmeAttribute fixme,
        CancellationToken cancellationToken
    )
    {
        var discovered = RunSelection
            .Discover([GetType()], GetType().Assembly, includeExplicit: true)
            .FirstOrDefault(test => test.Fixme && test.Title == method.Name);
        if (discovered is not null)
        {
            await HostTestFilters
                .ReportFixmeAsync(Config, discovered, ResultsDirectory, cancellationToken)
                .ConfigureAwait(false);
        }

        RunSelection.SkipWith(E2E.Internal.Skip.FixmeReason(fixme.Reason));
    }

    private async Task RegisterRunningAsync(
        Task started,
        string uniqueId,
        MethodInfo method,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await started.ConfigureAwait(false);
        }
        catch (SetupFailedException failure)
        {
            await SkipSetupFailedAsync(failure, method, cancellationToken).ConfigureAwait(false);
        }

        Running[uniqueId] = this;
    }

    /// <summary>
    /// Completes the sessions of tests that xUnit never disposed: a derived <c>InitializeAsync</c> that threw after the base started the session
    /// (a skip from a hook, say) ends the test with neither the look nor <see cref="DisposeAsync"/>. The run calls it before its report is final.
    /// </summary>
    internal static async Task CompleteAbandonedAsync()
    {
        List<Exception>? errors = null;
        foreach (var uniqueId in Running.Keys.ToArray())
        {
            if (!Running.TryRemove(uniqueId, out var fixture) || fixture.TakeSession() is not { } session)
            {
                continue;
            }

            try
            {
                // The hook ended the test before xUnit could say how: a fixme skip is the one verdict the session knows.
                session.Complete(
                    session.RuntimeSkip is { } skip
                        ? new SkipException(skip.Reason)
                        : new TestException("TEST_FAILED", "The test ended in InitializeAsync.")
                );
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                (errors ??= []).Add(ex);
            }
            finally
            {
                try
                {
                    await DisposeSessionAsync(session, null).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    (errors ??= []).Add(ex);
                }
            }
        }

        if (errors is not null)
        {
            throw new AggregateException(errors);
        }
    }

    // The test body has ended and the class is not disposed yet: a failed body is looked at now, ahead of the teardown.
    // The entry goes with the look, or with the dispose when the test method never ran.
    internal static void LookAtFailure(IXunitTest test)
    {
        if (!Running.TryRemove(test.UniqueID, out var fixture))
        {
            return;
        }

        // A route or dialog handler that failed after the last step fails the attempt here, as a step would. A body that failed first keeps
        // its own failure as the verdict, and the handler's is only collected so it does not reach the next test.
        var late = fixture.SettleLate();
        if (Xunit.TestContext.Current.TestState is { Result: TestResult.Failed } state)
        {
            Look(fixture, ErrorForCache(state, null, fixture._refused)!);
            return;
        }

        if (late is not null)
        {
            Look(fixture, late);
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(late);
        }
    }

    private static void Look(E2ETest fixture, Exception failure)
    {
        try
        {
            fixture.CaptureFailureEvidenceAsync(failure).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // The look is a courtesy: the test's own failure is the one to report.
        }
    }

    private Exception? SettleLate()
    {
        try
        {
            SettleAttemptAsync(Xunit.TestContext.Current.CancellationToken).GetAwaiter().GetResult();
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

    public virtual async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        if (Xunit.TestContext.Current.Test is { } running)
        {
            // The look removes it when the test method ran. A test that never got there (a failed setup) leaves it here.
            Running.TryRemove(running.UniqueID, out _);
        }

        var session = TakeSession();
        if (session is null)
        {
            return;
        }

        var current = Xunit.TestContext.Current;
        var soft = session.CloseSoftFailures();

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
        if (current.TestState?.Result is TestResult.Passed && soft is null && late is null)
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
                late = ex;
            }
        }

        try
        {
            session.Complete(ErrorForCache(current.TestState, soft, _refused) ?? late, current.CancellationToken);
        }
        finally
        {
            await DisposeSessionAsync(session, soft).ConfigureAwait(false);
        }

        if (soft is not null)
        {
            throw soft;
        }

        // A test that failed already keeps its first failure as the verdict.
        if (late is not null && current.TestState?.Result != TestResult.Failed)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(late);
        }
    }

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

    // The xUnit result is set when the test body ends, before the test class is disposed.
    private static bool HasFailed()
    {
        return Xunit.TestContext.Current.TestState?.Result == TestResult.Failed;
    }

    private static readonly HashSet<string> CancellationTypes = new(StringComparer.Ordinal)
    {
        typeof(OperationCanceledException).FullName!,
        typeof(TaskCanceledException).FullName!,
    };

    // xUnit keeps only the failure's type and message. The session itself knows when an agent call hit
    // a model outage or a stale replay, so the cache still keeps its entries then.
    private static Exception? ErrorForCache(TestResultState? state, TestException? soft, TestException? refused = null)
    {
        // A soft failure outranks a skip: xUnit fails the test at dispose, so the cache sees a failure.
        if (state?.Result is TestResult.Passed || (soft is not null && state?.Result is TestResult.Skipped))
        {
            return soft;
        }

        var type = state?.ExceptionTypes?.FirstOrDefault();
        var message = state?.ExceptionMessages?.FirstOrDefault();
        if (
            state?.Result == TestResult.Skipped
            || string.Equals(type, typeof(SkipException).FullName, StringComparison.Ordinal)
        )
        {
            return new SkipException(string.IsNullOrWhiteSpace(message) ? "skipped" : message);
        }

        // A cancelled test stays a cancellation: the session reads the runner's token to tell the user's cancel from any other.
        if (type is not null && CancellationTypes.Contains(type))
        {
            return new OperationCanceledException(
                string.IsNullOrWhiteSpace(message) ? "The test was cancelled." : message
            );
        }

        // The refusal of Fixme keeps its own code, which the host's type and message cannot carry.
        if (
            refused is not null
            && string.Equals(type, typeof(TestException).FullName, StringComparison.Ordinal)
            && string.Equals(message, refused.Message, StringComparison.Ordinal)
        )
        {
            return refused;
        }

        return new TestException("TEST_FAILED", string.IsNullOrWhiteSpace(message) ? "The test failed." : message);
    }
}
