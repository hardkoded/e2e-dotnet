// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
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
public abstract class E2ETest : E2EFixture, IAsyncLifetime
{
    // The attribute that looks before the teardown gets the test, not the instance.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, E2ETest> Running = new(StringComparer.Ordinal);

    /// <summary>
    /// Cache identity for this test. The default is the class and method name, with every theory
    /// argument in full, so each test and theory row keeps its own replay. The xUnit display name
    /// is not used, because xUnit shortens long arguments in it and a display setting can change it.
    /// </summary>
    protected virtual string CacheTitle(ITest test)
    {
        ArgumentNullException.ThrowIfNull(test);
        var name = test.TestCase.TestClassName + "." + test.TestCase.TestMethodName;
        return test is IXunitTest { TestMethodArguments.Length: > 0 } row
            ? name + "(" + string.Join(", ", row.TestMethodArguments.Select(argument => FormatArgument(argument, row.TestMethodArguments.Length > 1))) + ")"
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
                ? "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\""
                : text,
            System.Collections.IEnumerable items when depth < MaxTitleDepth => "[" + string.Join(", ", items.Cast<object?>().Take(MaxTitleItems).Select(item => FormatArgument(item, true, depth + 1))) + "]",
            _ => Convert.ToString(argument, CultureInfo.InvariantCulture) ?? "null",
        };
    }

    public virtual async ValueTask InitializeAsync()
    {
        var current = Xunit.TestContext.Current;
        var test = current.Test ?? throw new InvalidOperationException("The E2E session starts inside a running xUnit test.");
        await StartSessionAsync(CacheTitle(test), 1, null, HasFailed, current.CancellationToken, (test.TestCase as IXunitTestCase)?.TestMethod?.Method).ConfigureAwait(false);
        Running[test.UniqueID] = this;
    }

    // The test body has ended and the class is not disposed yet: a failed body is looked at now, ahead of the teardown.
    // The entry goes with the look, so a derived DisposeAsync that skips the base one leaves nothing behind.
    internal static void LookAtFailure(IXunitTest test)
    {
        if (!Running.TryRemove(test.UniqueID, out var fixture) || Xunit.TestContext.Current.TestState is not { Result: TestResult.Failed } state)
        {
            return;
        }

        try
        {
            fixture.CaptureFailureEvidenceAsync(ErrorForCache(state, null)!).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // The look is a courtesy: the test's own failure is the one to report.
        }
    }

    public virtual async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        var session = TakeSession();
        if (session is null)
        {
            return;
        }

        var current = Xunit.TestContext.Current;
        var soft = session.CloseSoftFailures();
        try
        {
            session.Complete(ErrorForCache(current.TestState, soft), current.CancellationToken);
        }
        finally
        {
            await DisposeSessionAsync(session, soft).ConfigureAwait(false);
        }

        if (soft is not null)
        {
            throw soft;
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

    // xUnit keeps only the failure's type and message. The session itself knows when an agent call hit
    // a model outage or a stale replay, so the cache still keeps its entries then.
    private static Exception? ErrorForCache(TestResultState? state, TestException? soft)
    {
        // A soft failure outranks a skip: xUnit fails the test at dispose, so the cache sees a failure.
        if (state?.Result is TestResult.Passed || (soft is not null && state?.Result is TestResult.Skipped))
        {
            return soft;
        }

        var type = state?.ExceptionTypes?.FirstOrDefault();
        var message = state?.ExceptionMessages?.FirstOrDefault();
        if (state?.Result == TestResult.Skipped || string.Equals(type, typeof(SkipException).FullName, StringComparison.Ordinal))
        {
            return new SkipException(string.IsNullOrWhiteSpace(message) ? "skipped" : message);
        }

        return new TestException("TEST_FAILED", string.IsNullOrWhiteSpace(message) ? "The test failed." : message);
    }
}
