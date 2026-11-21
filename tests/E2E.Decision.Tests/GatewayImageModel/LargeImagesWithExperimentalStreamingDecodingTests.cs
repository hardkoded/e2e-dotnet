// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayImageModel.GatewayImageModelFixture;

namespace E2E.Decision.Tests.GatewayImageModel;

/// <summary>Upstream's <c>describe('large images with experimental streaming decoding')</c> of <c>gateway-image-model.test.ts</c>.</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class LargeImagesWithExperimentalStreamingDecodingTests
{
    private static readonly byte[] Large = BytesOf(70_000, 1);
    private static readonly string LargeBase64 = Convert.ToBase64String(Large);
    private static readonly byte[] Small = BytesOf(1_000, 2);
    private static readonly string SmallBase64 = Convert.ToBase64String(Small);

    private static ImageModelV4CallOptions CallOptions(
        string providerOptions = """{"gateway":{"experimental_streamingImageDecoding":true}}"""
    ) => Call(n: 2, providerOptions: providerOptions);

    private static byte[] BytesOf(int length, uint seed)
    {
        var bytes = new byte[length];
        var state = seed;
        for (var index = 0; index < length; index++)
        {
            state = unchecked(state * 1103515245 + 12345);
            bytes[index] = (byte)(state >> 24);
        }

        return bytes;
    }

    private static string Images(params string[] images) =>
        new JsonArray(images.Select(i => (JsonNode?)i).ToArray()).ToJsonString();

    private static string Base64Url(string base64) => base64.Replace('+', '-').Replace('/', '_');

    private static (TestServer Server, E2E.Decision.Sdk.Gateway.GatewayImageModel Model) Respond(string body)
    {
        var server = new TestServer();
        server.RespondJson(ImageUrl, body);
        return (server, CreateTestModel(server));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task Should_preserve_base64_results_when_the_option_is(bool? enabled)
    {
        var (_, model) = Respond("""{"images":""" + Images(LargeBase64, SmallBase64) + "}");
        using var _ = model;

        var result = await model.DoGenerateAsync(
            CallOptions(
                enabled is null
                    ? "{}"
                    : $$$"""{"gateway":{"experimental_streamingImageDecoding":{{{enabled.ToString()!.ToLowerInvariant()}}}}}"""
            )
        );

        Assert.Equal([LargeBase64, SmallBase64], result.Images);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Should_remove_the_client_option_set_to_while_preserving_service_options(bool enabled)
    {
        var (server, model) = Respond("""{"images":""" + Images(LargeBase64) + "}");
        using var _ = model;
        var providerOptions =
            $$$"""{"gateway":{"experimental_streamingImageDecoding":{{{enabled.ToString().ToLowerInvariant()}}},"tags":["images"]},"openai":{"quality":"hd"}}""";
        var options = CallOptions(providerOptions);

        await model.DoGenerateAsync(options);

        AssertJsonEqual(
            Json("""{"gateway":{"tags":["images"]},"openai":{"quality":"hd"}}"""),
            server.Calls[0].RequestBodyJson!["providerOptions"]
        );
        // The caller's options are not changed.
        Assert.Equal(
            enabled,
            options.ProviderOptions["gateway"].GetProperty("experimental_streamingImageDecoding").GetBoolean()
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Should_omit_providerOptions_when_the_client_option_set_to_is_its_only_entry(bool enabled)
    {
        var (server, model) = Respond("""{"images":""" + Images(SmallBase64) + "}");
        using var _ = model;

        await model.DoGenerateAsync(
            CallOptions(
                $$$"""{"gateway":{"experimental_streamingImageDecoding":{{{enabled.ToString().ToLowerInvariant()}}}}}"""
            )
        );

        Assert.False(server.Calls[0].RequestBodyJson!.AsObject().ContainsKey("providerOptions"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Should_omit_an_empty_gateway_object_when_the_client_option_is(bool enabled)
    {
        var (server, model) = Respond("""{"images":""" + Images(SmallBase64) + "}");
        using var _ = model;

        await model.DoGenerateAsync(
            CallOptions(
                $$$"""{"gateway":{"experimental_streamingImageDecoding":{{{enabled.ToString().ToLowerInvariant()}}}},"openai":{"quality":"hd"}}"""
            )
        );

        AssertJsonEqual(Json("""{"openai":{"quality":"hd"}}"""), server.Calls[0].RequestBodyJson!["providerOptions"]);
    }

    [Fact]
    public async Task Should_return_images_of_64_KiB_and_more_as_Uint8Array()
    {
        var (_, model) = Respond("""{"images":""" + Images(LargeBase64) + "}");
        using var _ = model;

        var result = await model.DoGenerateAsync(CallOptions());

        Assert.Equal([Large], result.ImageBytes);
        Assert.Null(result.Images);
    }

    [Fact]
    public async Task Should_keep_images_below_64_KiB_as_base64_strings()
    {
        var (_, model) = Respond("""{"images":""" + Images(SmallBase64, SmallBase64) + "}");
        using var _ = model;

        var result = await model.DoGenerateAsync(CallOptions());

        Assert.Equal([SmallBase64, SmallBase64], result.Images);
    }

    [Fact]
    public async Task Should_return_bytes_for_every_image_when_large_and_small_images_are_mixed()
    {
        var (_, model) = Respond("""{"images":""" + Images(SmallBase64, LargeBase64, SmallBase64) + "}");
        using var _ = model;

        var result = await model.DoGenerateAsync(CallOptions());

        Assert.Equal([Small, Large, Small], result.ImageBytes);
    }

    [Fact]
    public async Task Should_keep_strings_that_cannot_be_decoded_unambiguously_and_return_all_images_as_strings()
    {
        var base64Url = Base64Url(LargeBase64);
        var (_, model) = Respond("""{"images":""" + Images(base64Url, SmallBase64) + "}");
        using var _ = model;

        var result = await model.DoGenerateAsync(CallOptions());

        Assert.Equal([base64Url, SmallBase64], result.Images);
    }

    [Fact]
    public async Task Should_decode_large_strings_that_are_not_canonical_base64_like_the_regular_path_when_mixed_with_bytes()
    {
        var base64Url = Base64Url(LargeBase64);
        var (_, model) = Respond("""{"images":""" + Images(base64Url, LargeBase64) + "}");
        using var _ = model;

        var result = await model.DoGenerateAsync(CallOptions());

        Assert.Equal([Large, Large], result.ImageBytes);
    }

    [Fact]
    public async Task Should_not_change_the_other_fields_of_the_response()
    {
        var (_, model) = Respond(
            """{"images":"""
                + Images(LargeBase64)
                + ""","warnings":[{"type":"other","message":"careful"}],"providerMetadata":{"gateway":{"images":[{}],"cost":"0.04"}},"usage":{"inputTokens":1,"outputTokens":2,"totalTokens":3}}"""
        );
        using var _ = model;

        var result = await model.DoGenerateAsync(CallOptions());

        Assert.Equal([Large], result.ImageBytes);
        Assert.Equal([new SharedV4Warning { Type = "other", Message = "careful" }], result.Warnings);
        AssertJsonEqual(
            Json("""{"gateway":{"images":[{}],"cost":"0.04"}}"""),
            System.Text.Json.JsonSerializer.SerializeToNode(result.ProviderMetadata)
        );
        Assert.Equal(new ImageModelV4Usage(1, 2, 3), result.Usage);
    }

    [Fact]
    public async Task Should_read_a_body_that_arrives_in_small_chunks()
    {
        var body = """{"images":""" + Images(LargeBase64, SmallBase64) + "}";
        var server = new TestServer();
        server.RespondChunks(
            ImageUrl,
            Enumerable
                .Range(0, (body.Length + 1998) / 1999)
                .Select(i => body.Substring(i * 1999, Math.Min(1999, body.Length - i * 1999)))
                .ToList(),
            "application/json"
        );
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(CallOptions());

        Assert.Equal([Large, Small], result.ImageBytes);
    }

    [Fact]
    public async Task Should_reject_a_response_whose_images_are_not_strings()
    {
        var (_, model) = Respond("""{"images":[""" + "\"" + LargeBase64 + "\"" + ",5]}");
        using var _ = model;

        var error = await Assert.ThrowsAsync<GatewayResponseException>(() => model.DoGenerateAsync(CallOptions()));

        // The error does not carry the image.
        Assert.True(error.Message.Length < 2000);
    }

    // .NET-only: no upstream `it` covers whitespace in a string decoded like the regular path.
    [Fact]
    public async Task Should_decode_a_string_with_trailing_whitespace_like_the_regular_path_when_mixed_with_bytes()
    {
        var (_, model) = Respond("""{"images":""" + Images(LargeBase64, "YQ==\n") + "}");
        using var _ = model;

        var result = await model.DoGenerateAsync(CallOptions());

        Assert.Equal([Large, [(byte)'a']], result.ImageBytes);
    }
}
