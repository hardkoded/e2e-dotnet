// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.NoOutputError;

/// <summary>
/// <c>describe('empty model output')</c> of upstream <c>tests/unit/no-output-error.test.ts</c>, the two <c>it</c> the plain-call change added.
/// The port's model is an <see cref="IAgentModel"/>, not an AI SDK language model, and has no plain call that parses the reply: a call that
/// asks for no tools and no schema is <c>e2e doctor --check-model</c>'s, which treats any answer as proof the model replied. The port's
/// providers report no <c>error</c> finish; a custom model that does is a provider failure, whatever text came with it. Not ported: 'a
/// finished reply that is not JSON is MODEL_OUTPUT_INVALID with its text', which asserts the adapter's JSON parse of a plain call.
/// </summary>
public sealed class NoOutputErrorTests
{
    [Fact]
    public async Task Partial_Plain_call_an_error_finish_is_MODEL_PROVIDER_FAILED_not_MODEL_OUTPUT_INVALID()
    {
        var model = new ScriptedModel(_ => new ModelResponse { Content = "{\"a\":1}", FinishReason = ModelResponse.ErrorFinish, RawFinishReason = "error" });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(new DocumentWorld().Map("/", page => page.Paragraph("page content"))),
            Model = model,
            BaseUrl = "https://todos.test",
            TestTitle = "error finish",
            CacheEnabled = false,
        });
        await session.App.OpenAsync("/");

        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.AssertAsync("the page shows the target token"));
        session.Complete(error);

        Assert.Equal("MODEL_PROVIDER_FAILED", error.Code);
        Assert.Contains("error finish (raw finish reason: error)", error.Message, StringComparison.Ordinal);
    }
}
