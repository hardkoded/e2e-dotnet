// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using static E2E.Decision.Tests.TypeSafeAiDecisionModel.TypeSafeAiDecisionModelFixture;

namespace E2E.Decision.Tests.TypeSafeAiDecisionModel;

/// <summary>Upstream's <c>describe('long context')</c> of <c>typesafe-ai-decision-model.test.ts</c>.</summary>
[Collection("TypeSafeAiEnvironment")]
public sealed class LongContextTests
{
    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData(null)]
    public async Task Enables_the_header_only_when_longContext_is_true(string? longContext)
    {
        var (server, model) = Create();

        var result = await model.DoDecideAsync(
            Options(
                providerOptions: longContext is null ? null : """{"typesafe":{"longContext":""" + longContext + "}}"
            ),
            CancellationToken.None
        );

        Assert.Equal(
            longContext == "true" ? "true" : null,
            server.Calls[0].RequestHeaders.GetValueOrDefault("typesafe-danger-zone-long-context")
        );
        AssertJsonEqual(ExpectedRequest, server.Calls[0].RequestBodyJson);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Combines_long_context_with_image_support_and_warns_only_for_unknown_options()
    {
        var (server, model) = Create();

        var result = await model.DoDecideAsync(
            Options(
                state: [FilePart("image/png", "iVBORw==")],
                providerOptions: """{"typesafe":{"longContext":true,"temperature":0}}""",
                headers: new Dictionary<string, string> { ["x-custom"] = "custom" }
            ),
            CancellationToken.None
        );

        var headers = server.Calls[0].RequestHeaders;
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
        var (server, model) = Create();

        await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            model.DoDecideAsync(
                Options(providerOptions: """{"typesafe":{"longContext":""" + longContext + "}}"),
                CancellationToken.None
            )
        );
        Assert.Empty(server.Calls);
    }
}
