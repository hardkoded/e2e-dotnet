// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using static E2E.Decision.Tests.GatewayExtractionModel.GatewayExtractionModelFixture;

namespace E2E.Decision.Tests.GatewayExtractionModel;

/// <summary>Upstream's <c>describe('state parts')</c> of <c>gateway-extraction-model.test.ts</c>.</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class StatePartsTests
{
    public static TheoryData<string, string> StatePartsCases =>
        new()
        {
            { "a text part", """[{"type":"text","text":"Inspect."}]""" },
            { "a JSON part", """[{"type":"json","value":{"product":"vase","history":["new"]}}]""" },
            {
                "mixed parts",
                """[{"type":"text","text":"Inspect."},{"type":"json","value":{"product":"vase"}},{"type":"text","text":"Again."}]"""
            },
        };

    [Theory]
    [MemberData(nameof(StatePartsCases))]
    public async Task Sends_name_as_ordered_state_parts(string name, string state)
    {
        Assert.NotEmpty(name);
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoExtractAsync(Call(State(state)), CancellationToken.None);

        AssertJsonEqual(
            new JsonObject { ["stateParts"] = JsonNode.Parse(state), ["fields"] = TestFieldsJson.DeepClone() },
            server.Calls[0].RequestBodyJson
        );
    }

    [Fact]
    public async Task Sends_file_bytes_as_base64_in_ordered_state_parts()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoExtractAsync(
            Call([
                new DecisionTextPart("Read this invoice."),
                new DecisionFilePart("application/pdf", [1, 2, 3]) { Filename = "invoice.pdf" },
            ]),
            CancellationToken.None
        );

        AssertJsonEqual(
            JsonNode.Parse(
                """
                [
                  { "type": "text", "text": "Read this invoice." },
                  {
                    "type": "file",
                    "mediaType": "application/pdf",
                    "filename": "invoice.pdf",
                    "data": { "type": "data", "data": "AQID" }
                  }
                ]
                """
            ),
            server.Calls[0].RequestBodyJson!["stateParts"]
        );
    }

    [Fact]
    public async Task Sends_base64_file_strings_unchanged()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoExtractAsync(
            Call([new DecisionFilePart("image/jpeg", []) { Base64 = "AQID" }]),
            CancellationToken.None
        );

        AssertJsonEqual(
            JsonNode.Parse(
                """[{ "type": "file", "mediaType": "image/jpeg", "data": { "type": "data", "data": "AQID" } }]"""
            ),
            server.Calls[0].RequestBodyJson!["stateParts"]
        );
    }

    [Fact]
    public async Task Sends_file_references_unchanged()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoExtractAsync(
            Call([
                new DecisionFilePart("application/pdf", [])
                {
                    DataType = "reference",
                    Reference = JsonDocument.Parse("""{"gateway":"file_123"}""").RootElement.Clone(),
                },
            ]),
            CancellationToken.None
        );

        AssertJsonEqual(
            JsonNode.Parse(
                """
                [
                  {
                    "type": "file",
                    "mediaType": "application/pdf",
                    "data": { "type": "reference", "reference": { "gateway": "file_123" } }
                  }
                ]
                """
            ),
            server.Calls[0].RequestBodyJson!["stateParts"]
        );
    }

    [Theory]
    [InlineData("url")]
    [InlineData("text")]
    public async Task Rejects_type_file_data_before_sending_a_Gateway_request(string type)
    {
        var server = new TestServer();
        using var model = CreateTestModel(server);
        var file = new DecisionFilePart("application/pdf", [])
        {
            DataType = type,
            Url = type == "url" ? new Uri("https://example.com/a.pdf") : null,
            Text = type == "text" ? "Invoice" : null,
        };

        var error = await Assert.ThrowsAsync<UnsupportedFunctionalityException>(() =>
            model.DoExtractAsync(Call([file]), CancellationToken.None)
        );

        Assert.Equal("Gateway extraction file input: " + type + " data", error.Functionality);
        Assert.Empty(server.Calls);
    }
}
