// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk;
using static E2E.Decision.Tests.GatewayImageModel.GatewayImageModelFixture;

namespace E2E.Decision.Tests.GatewayImageModel;

/// <summary>Upstream's <c>describe('file encoding')</c> of <c>gateway-image-model.test.ts</c>.</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class FileEncodingTests
{
    [Fact]
    public async Task Should_encode_Uint8Array_files_to_base64_strings()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);
        byte[] binaryData = [72, 101, 108, 108, 111]; // "Hello"

        await model.DoGenerateAsync(
            Call("Edit this image", files: [new ImageModelV4FileData("image/png", binaryData)])
        );

        var files = server.Calls[0].RequestBodyJson!["files"]!.AsArray();
        Assert.Single(files);
        AssertJsonEqual(Json("""{"type":"file","mediaType":"image/png","data":"SGVsbG8="}"""), files[0]);
    }

    [Fact]
    public async Task Should_pass_through_files_with_string_data_unchanged()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoGenerateAsync(
            Call("Edit this image", files: [new ImageModelV4FileData("image/png", "already-base64-encoded")])
        );

        var files = server.Calls[0].RequestBodyJson!["files"]!.AsArray();
        Assert.Single(files);
        AssertJsonEqual(Json("""{"type":"file","mediaType":"image/png","data":"already-base64-encoded"}"""), files[0]);
    }

    [Fact]
    public async Task Should_pass_through_URL_type_files_unchanged()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoGenerateAsync(
            Call("Edit this image", files: [new ImageModelV4FileUrl("https://example.com/image.png")])
        );

        var files = server.Calls[0].RequestBodyJson!["files"]!.AsArray();
        Assert.Single(files);
        AssertJsonEqual(Json("""{"type":"url","url":"https://example.com/image.png"}"""), files[0]);
    }

    [Fact]
    public async Task Should_encode_Uint8Array_mask_to_base64_string()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);
        byte[] maskData = [255, 0, 255, 0]; // Simple mask

        await model.DoGenerateAsync(Call("Inpaint this area", mask: new ImageModelV4FileData("image/png", maskData)));

        AssertJsonEqual(
            Json("""{"type":"file","mediaType":"image/png","data":"/wD/AA=="}"""),
            server.Calls[0].RequestBodyJson!["mask"]
        );
    }

    [Fact]
    public async Task Should_handle_mixed_file_types_with_encoding()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoGenerateAsync(
            Call(
                "Edit these images",
                files:
                [
                    new ImageModelV4FileData("image/png", new byte[] { 1, 2, 3 }),
                    new ImageModelV4FileData("image/jpeg", "already-encoded"),
                    new ImageModelV4FileUrl("https://example.com/image.png"),
                ],
                mask: new ImageModelV4FileData("image/png", new byte[] { 4, 5, 6 })
            )
        );

        var requestBody = server.Calls[0].RequestBodyJson!;
        var files = requestBody["files"]!.AsArray();
        Assert.Equal(3, files.Count);
        AssertJsonEqual(Json("""{"type":"file","mediaType":"image/png","data":"AQID"}"""), files[0]);
        AssertJsonEqual(Json("""{"type":"file","mediaType":"image/jpeg","data":"already-encoded"}"""), files[1]);
        AssertJsonEqual(Json("""{"type":"url","url":"https://example.com/image.png"}"""), files[2]);
        AssertJsonEqual(Json("""{"type":"file","mediaType":"image/png","data":"BAUG"}"""), requestBody["mask"]);
    }

    [Fact]
    public async Task Should_preserve_providerOptions_on_files_during_encoding()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);
        byte[] binaryData = [72, 101, 108, 108, 111];

        await model.DoGenerateAsync(
            Call(
                "Edit this image",
                files:
                [
                    new ImageModelV4FileData("image/png", binaryData)
                    {
                        ProviderOptions = Options("""{"openai":{"quality":"hd"}}"""),
                    },
                ]
            )
        );

        AssertJsonEqual(
            Json(
                """{"type":"file","mediaType":"image/png","data":"SGVsbG8=","providerOptions":{"openai":{"quality":"hd"}}}"""
            ),
            server.Calls[0].RequestBodyJson!["files"]![0]
        );
    }
}
