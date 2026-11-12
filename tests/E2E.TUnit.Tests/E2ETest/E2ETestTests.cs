// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.TUnit.Tests.E2ETest;

/// <summary>
/// .NET-only: the TUnit adapter has no upstream <c>it</c>. Each test checks that <c>E2E.TUnit.E2ETest</c>
/// does for TUnit what <c>E2E.NUnit.E2ETest</c> and <c>E2E.XUnit.V3.E2ETest</c> do for their frameworks.
/// </summary>
public sealed class E2ETestTests : BrowserTest
{
    private static int _retryAttempts;

    [Test]
    public async Task Starts_a_session_before_the_test_body()
    {
        await Expect.That(Screen.GetByLabel("Loads")).ToBeVisibleAsync();
    }

    [Test]
    [Arguments("a")]
    [Arguments("a, b")]
    public async Task Titles_the_test_by_its_class_method_and_arguments(string argument)
    {
        await Assert.That(Title).IsEqualTo(
            argument == "a"
                ? "E2E.TUnit.Tests.E2ETest.E2ETestTests.Titles_the_test_by_its_class_method_and_arguments(a)"
                : "E2E.TUnit.Tests.E2ETest.E2ETestTests.Titles_the_test_by_its_class_method_and_arguments(a, b)");
    }

    [Test]
    [ExpectedFailure("toHaveText failed", "expected: text \"the count\"", "toBeHidden failed")]
    public async Task Soft_failures_let_the_body_run_on_then_fail_the_test()
    {
        await Expect.Soft(Screen.GetByLabel("Loads")).ToHaveTextAsync("the count", timeout: SoftTimeout);
        await Expect.Soft(Screen.GetByLabel("Loads")).ToBeHiddenAsync(SoftTimeout);
        await Expect.That(Screen.GetByLabel("Loads")).ToBeVisibleAsync();
    }

    [Test]
    [ExpectedFailure("the body gave up")]
    public async Task A_body_error_fails_the_test()
    {
        await Expect.Soft(Screen.GetByLabel("Loads")).ToHaveTextAsync("the count", timeout: SoftTimeout);
        throw new InvalidOperationException("the body gave up");
    }

    [Test]
    [ExpectedFailure("toHaveText failed", "expected: text \"the count\"")]
    public async Task A_soft_failure_before_a_skip_fails_the_test()
    {
        await Expect.Soft(Screen.GetByLabel("Loads")).ToHaveTextAsync("the count", timeout: SoftTimeout);
        Skip.Test("feature disabled");
    }

    [Test]
    [Retry(2)]
    public async Task Retries_a_failed_attempt_with_a_new_session()
    {
        var attempt = Interlocked.Increment(ref _retryAttempts);
        await Expect.That(Screen.GetByLabel("Loads")).ToBeVisibleAsync();
        if (attempt == 1)
        {
            throw new InvalidOperationException("the first attempt fails");
        }

        await Assert.That(attempt).IsEqualTo(2);
    }
}
