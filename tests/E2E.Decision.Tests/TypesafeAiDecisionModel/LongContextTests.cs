// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using static E2E.Decision.Tests.TypesafeAiDecisionModel.TypesafeAiDecisionModelTests;

namespace E2E.Decision.Tests.TypesafeAiDecisionModel;

/// <summary>Ports <c>describe('long context')</c> of <c>packages/typesafe-ai/src/typesafe-ai-decision-model.test.ts</c> of the AI SDK (<c>vercel/ai</c>).</summary>
[Collection("TypesafeAi")]
public sealed class LongContextTests
{
    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData(null)]
    public async Task Enables_the_header_only_when_longContext_is_true(string? longContext)
    {
        var server = new TypeSafeServer();
        var result = await server
            .Model()
            .DoDecideAsync(
                Options(
                    providerOptions: longContext is null ? null : Typesafe("{\"longContext\":" + longContext + "}")
                ),
                CancellationToken.None
            );

        Assert.Equal(
            longContext == "true" ? "true" : null,
            server.Calls[0].Headers.GetValueOrDefault("typesafe-danger-zone-long-context")
        );
        TypesafeAiDecisionModelTests.AssertJson(TypeSafeServer.NativeRequest, server.Calls[0].Json());
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Combines_long_context_with_image_support_and_warns_only_for_unknown_options()
    {
        var server = new TypeSafeServer();
        var result = await server
            .Model()
            .DoDecideAsync(
                Options(
                    state: [new DecisionFilePart("image/png", [0x89, 0x50, 0x4e, 0x47])],
                    providerOptions: Typesafe("""{"longContext":true,"temperature":0}"""),
                    headers: new Dictionary<string, string> { ["x-custom"] = "custom" }
                ),
                CancellationToken.None
            );

        var headers = server.Calls[0].Headers;
        Assert.Equal("true", headers["typesafe-danger-zone-images"]);
        Assert.Equal("true", headers["typesafe-danger-zone-long-context"]);
        Assert.Equal("custom", headers["x-custom"]);
        Assert.Equal(["unsupported setting: providerOptions.typesafe.temperature"], result.Warnings);
    }

    [Theory]
    [InlineData("\"true\"")]
    [InlineData("1")]
    [InlineData("null")]
    public async Task Rejects_invalid_longContext_values(string longContext)
    {
        var server = new TypeSafeServer();

        await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            server
                .Model()
                .DoDecideAsync(
                    Options(providerOptions: Typesafe("{\"longContext\":" + longContext + "}")),
                    CancellationToken.None
                )
        );
        Assert.Empty(server.Calls);
    }
}
