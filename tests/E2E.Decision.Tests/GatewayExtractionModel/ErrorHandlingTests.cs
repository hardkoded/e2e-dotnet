// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayExtractionModel.GatewayExtractionModelFixture;

namespace E2E.Decision.Tests.GatewayExtractionModel;

/// <summary>Upstream's <c>describe('error handling')</c> of <c>gateway-extraction-model.test.ts</c>.</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class ErrorHandlingTests
{
    [Fact]
    public async Task Should_throw_ExtractionUnsupportedFieldTypeError_for_an_unsupported_field_type()
    {
        var server = new TestServer();
        PrepareErrorResponse(
            server,
            400,
            """
            {
              "message": "Field \"total\" has type \"number\", which is not supported by provider \"upstream.extraction\" and model \"extract-1\".",
              "type": "invalid_request_error",
              "param": {
                "name": "AI_ExtractionUnsupportedFieldTypeError",
                "fieldId": "total",
                "fieldType": "number",
                "statusCode": 400
              }
            }
            """
        );
        using var model = CreateTestModel(server);

        var error = await Assert.ThrowsAsync<ExtractionUnsupportedFieldTypeException>(() =>
            model.DoExtractAsync(Call(), CancellationToken.None)
        );

        Assert.Equal("total", error.FieldId);
        Assert.Equal("number", error.FieldType);
        Assert.Equal("gateway", error.Provider);
        Assert.Equal("scaledown/extract-1", error.ModelId);
        Assert.Equal(
            "Field \"total\" has type \"number\", which is not supported by provider \"gateway\" and model \"scaledown/extract-1\".",
            error.Message
        );
    }

    [Fact]
    public async Task Should_keep_the_list_item_type_of_an_unsupported_list_field()
    {
        var server = new TestServer();
        PrepareErrorResponse(
            server,
            400,
            """
            {
              "message": "Unsupported field type.",
              "type": "invalid_request_error",
              "param": {
                "name": "AI_ExtractionUnsupportedFieldTypeError",
                "fieldId": "contacts",
                "fieldType": "list<number>"
              }
            }
            """
        );
        using var model = CreateTestModel(server);

        var error = await Assert.ThrowsAsync<ExtractionUnsupportedFieldTypeException>(() =>
            model.DoExtractAsync(Call(), CancellationToken.None)
        );

        Assert.Equal("contacts", error.FieldId);
        Assert.Equal("list<number>", error.FieldType);
    }

    [Theory]
    [InlineData("no param", null)]
    [InlineData("another error class", """{"name":"AI_InvalidArgumentError","fieldId":"a","fieldType":"b"}""")]
    [InlineData("no field", """{"name":"AI_ExtractionUnsupportedFieldTypeError"}""")]
    public async Task Should_throw_GatewayInvalidRequestError_on_a_400_with_name(string name, string? param)
    {
        Assert.NotEmpty(name);
        var server = new TestServer();
        PrepareErrorResponse(
            server,
            400,
            "{\"message\":\"Invalid fields format\",\"type\":\"invalid_request_error\""
                + (param is null ? "" : ",\"param\":" + param)
                + "}"
        );
        using var model = CreateTestModel(server);

        var error = await Assert.ThrowsAsync<GatewayInvalidRequestException>(() =>
            model.DoExtractAsync(Call(), CancellationToken.None)
        );

        Assert.Equal(400, error.StatusCode);
        Assert.Equal("Invalid fields format", error.Message);
    }

    [Fact]
    public async Task Should_throw_GatewayInternalServerError_on_500()
    {
        var server = new TestServer();
        PrepareErrorResponse(server, 500, """{"message":"Internal server error","type":"internal_server_error"}""");
        using var model = CreateTestModel(server);

        var error = await Assert.ThrowsAsync<GatewayInternalServerException>(() =>
            model.DoExtractAsync(Call(), CancellationToken.None)
        );

        Assert.Equal(500, error.StatusCode);
    }
}
