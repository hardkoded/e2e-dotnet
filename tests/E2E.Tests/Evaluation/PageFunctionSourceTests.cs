// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.CheckActions.CheckPage;

namespace E2E.Tests.Evaluation;

/// <summary>
/// Upstream's <c>pageFunctionSource</c> describe: a function is serialized to source and evaluated on the page with its argument.
/// </summary>
/// <remarks>
/// Partial: upstream's <c>pageFunctionSource</c> takes a function object and reads its source with <c>Function.prototype.toString</c>.
/// .NET has no such source, so these tests pass the function's source as a string, which the port's evaluate takes. The
/// extraction of the source from a function object is not ported (it has no counterpart in .NET).
/// </remarks>
[Collection(BrowserCollection.Name)]
public sealed class PageFunctionSourceTests
{
    [Fact]
    public async Task Partial_Evaluates_an_ordinary_method_with_its_argument()
    {
        await RunAsync(string.Empty, async session =>
        {
            var result = await ((IBrowserSession)session).EvaluateAsync("function add(value) { return value + 1; }", 41, hasArg: true, CancellationToken.None);
            Assert.Equal(42, result!.Value.GetInt32());
        });
    }

    [Fact]
    public async Task Partial_Evaluates_an_async_method_with_its_argument()
    {
        await RunAsync(string.Empty, async session =>
        {
            var result = await ((IBrowserSession)session).EvaluateAsync("async function add(value) { return value + 1; }", 41, hasArg: true, CancellationToken.None);
            Assert.Equal(42, result!.Value.GetInt32());
        });
    }
}
