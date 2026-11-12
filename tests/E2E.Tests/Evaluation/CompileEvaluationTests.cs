// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.CheckActions.CheckPage;

namespace E2E.Tests.Evaluation;

/// <summary>
/// The page-side evaluation boundary, run on a live page. Not ported: "runs a keepNames-compiled source that references
/// __name" and "gives the wrapped binding the name esbuild kept" (they cover the TypeScript loader's output). The
/// <c>pageFunctionSource</c> describe is in <c>PageFunctionSourceTests</c> (partial: the port takes the source as a string). The port has no <c>__name</c> helper, so upstream's
/// "runs keepNames output after the code deleted Object" has nothing to port.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class CompileEvaluationTests
{
    [Theory]
    [InlineData("document.title")]
    [InlineData("(() => document.title)()")]
    [InlineData("Promise.resolve(document.title)")]
    [InlineData("() => document.title")]
    public async Task Evaluates_a_string_as_an_expression_calling_a_function_it_evaluates_to(string source)
    {
        await RunAsync("<title>Cart</title>", async session =>
            Assert.Equal("Cart", await EvaluateAsync<string>(session, source)));
    }

    [Fact]
    public async Task Leaves_a_page_global_of_any_name_visible_to_the_source()
    {
        await RunAsync(
            "<script>window.result = 'page value'; window.value = 'page value'</script>",
            async session => Assert.Equal(["page value", "page value"], await EvaluateAsync<string[]>(session, "[result, value]")));
    }

    [Fact]
    public async Task Calls_a_function_expression_with_the_argument()
    {
        await RunAsync(string.Empty, async session =>
        {
            var result = await ((IBrowserSession)session).EvaluateAsync("(n) => n + 1", 41, hasArg: true, CancellationToken.None);
            Assert.Equal(42, result!.Value.GetInt32());
        });
    }

    [Fact]
    public async Task Reports_an_exception_from_the_evaluated_code_by_message()
    {
        await RunAsync(string.Empty, async session =>
        {
            var error = await Assert.ThrowsAsync<TestException>(() => ((IBrowserSession)session).EvaluateAsync("() => { throw new Error(\"boom\"); }", null, hasArg: false, CancellationToken.None));
            Assert.Equal("EVALUATE_FAILED", error.Code);
            Assert.Equal("boom", error.Message);
        });
    }

    [Fact]
    public async Task Reports_a_result_that_is_not_JSON_by_why()
    {
        await RunAsync(string.Empty, async session =>
        {
            var error = await Assert.ThrowsAsync<TestException>(() => ((IBrowserSession)session).EvaluateAsync("() => [1, undefined]", null, hasArg: false, CancellationToken.None));
            Assert.Equal("INVALID_ARGUMENT", error.Code);
            Assert.Equal("evaluate result must be JSON-safe, found undefined", error.Message);
        });
    }
}
