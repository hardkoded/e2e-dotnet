// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.GatewayLanguageModel.GatewayLanguageModelFixture;

namespace E2E.Decision.Tests.GatewayLanguageModel;

/// <summary>
/// Upstream's <c>describe('doGenerate cancellation')</c> of <c>gateway-language-model.test.ts</c> (<c>describe.each</c> over
/// <c>doGenerate</c> and <c>doStream</c>). Upstream's <c>AbortError</c> is an <see cref="OperationCanceledException"/> and its
/// <c>TimeoutError</c> is a <see cref="TaskCanceledException"/>; the caller's token is cancelled, as the abort signal is.
/// </summary>
public sealed class DoGenerateCancellationTests
{
    [Theory]
    [InlineData("AbortError")]
    [InlineData("TimeoutError")]
    // Upstream asserts the error is the very one fetch rejected with. HttpClient wraps the error its handler throws in a new cancellation,
    // so the test checks that the call surfaces as a cancellation (not a Gateway error) whose cause is that error.
    public async Task Partial_Should_preserve_the_original_error_from_fetch(string name)
    {
        Exception error =
            name == "AbortError"
                ? new OperationCanceledException("Request cancelled")
                : new TaskCanceledException("Request cancelled");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        using var handler = new ThrowingHandler(error);
        using var http = new HttpClient(handler);
        using var model = CreateTestModel(http);

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => model.DoGenerateAsync(Options(), cancellation.Token));

        Assert.IsAssignableFrom<OperationCanceledException>(thrown);
        Assert.Same(error, thrown.InnerException);
    }
}
