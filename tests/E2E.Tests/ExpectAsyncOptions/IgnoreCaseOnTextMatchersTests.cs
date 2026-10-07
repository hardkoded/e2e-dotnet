// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.ExpectAsyncOptions;

public sealed class IgnoreCaseOnTextMatchersTests
{
    [Fact]
    public async Task Keeps_case_sensitive_matching_as_the_default()
    {
        var error = await ExpectTests.RunAsync(
            screen => Expect.That(screen.GetByText("Invoice preview")).ToHaveTextAsync("invoice preview", timeout: TimeSpan.FromMilliseconds(100)));

        var failure = Assert.IsType<TestException>(error);
        Assert.Contains("observed text \"Invoice preview\"", failure.Message, StringComparison.Ordinal);
    }
}
