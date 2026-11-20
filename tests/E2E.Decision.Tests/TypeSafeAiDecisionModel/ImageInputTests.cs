// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using static E2E.Decision.Tests.TypeSafeAiDecisionModel.TypeSafeAiDecisionModelFixture;

namespace E2E.Decision.Tests.TypeSafeAiDecisionModel;

/// <summary>Upstream's <c>describe('image input')</c> of <c>typesafe-ai-decision-model.test.ts</c>.</summary>
[Collection("TypeSafeAiEnvironment")]
public sealed class ImageInputTests
{
    private const string Png = "iVBORw==";
    private const string Jpeg = "/9j/4A==";

    private static JsonNode MultipartBody(TestServerCall call) =>
        JsonNode.Parse(call.RequestBodyMultipart.Single(part => part.Name == "request").Text)!;

    [Theory]
    [InlineData("[animal-photo]")]
    [InlineData("__proto__")]
    [InlineData("constructor")]
    [InlineData("request")]
    public async Task Uses_an_explicit_reference_in_assets_state_instructions_and_criteria(string reference)
    {
        var (server, model) = Create();
        var cat = new DecisionQuestion
        {
            Type = "boolean",
            Instructions = "Does " + reference + " show a cat?",
            TrueCriteria = Json("""{"image":""" + JsonSerializer.Serialize(reference) + "}"),
        };

        var result = await model.DoDecideAsync(
            Options(
                state:
                [
                    FilePart("image/png", Png, """{"reference":""" + JsonSerializer.Serialize(reference) + "}") with
                    {
                        Filename = "unrelated-name.png",
                    },
                    FilePart("image/jpeg", Jpeg),
                ],
                questions: new Dictionary<string, DecisionQuestion> { ["cat"] = cat }
            ),
            CancellationToken.None
        );

        var multipart = server.Calls[0].RequestBodyMultipart;
        var body = MultipartBody(server.Calls[0]);
        var references = body["assets"]!.AsObject().Select(pair => pair.Key).ToList();
        Assert.Equal(2, references.Count);
        Assert.Contains(reference, references);
        var generatedReference = references.Single(key => key != reference);
        Assert.Equal(reference + "\n" + generatedReference, (string?)body["state"]);
        AssertJsonEqual(JsonNode.Parse("""{"type":"image","part":"image-0"}"""), body["assets"]![reference]);
        Assert.Equal("unrelated-name.png", multipart.Single(part => part.Name == "image-0").FileName);
        AssertJsonEqual(
            JsonNode.Parse(
                "{\"type\":\"noul\",\"instructions\":"
                    + JsonSerializer.Serialize("Does " + reference + " show a cat?")
                    + ",\"criteria\":{\"true\":{\"image\":"
                    + JsonSerializer.Serialize(reference)
                    + "}}}"
            ),
            body["questions"]!["cat"]
        );
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\" \\t\\n\"")]
    [InlineData("123")]
    [InlineData("true")]
    [InlineData("null")]
    public async Task Rejects_invalid_references_before_HTTP(string reference)
    {
        var (server, model) = Create();

        await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            model.DoDecideAsync(
                Options(state: [FilePart("image/png", Png, """{"reference":""" + reference + "}")]),
                CancellationToken.None
            )
        );
        Assert.Empty(server.Calls);
    }

    [Fact]
    public async Task Keeps_JSON_state_as_an_object_when_it_already_contains_the_reference()
    {
        var (server, model) = Create();

        await model.DoDecideAsync(
            Options(
                state:
                [
                    new DecisionJsonPart(
                        JsonDocument.Parse("""{"photo":"[photo]","note":"Box arrived wet."}""").RootElement
                    ),
                    FilePart("image/png", Png, """{"reference":"[photo]"}"""),
                ]
            ),
            CancellationToken.None
        );

        var body = MultipartBody(server.Calls[0]);
        AssertJsonEqual(JsonNode.Parse("""{"photo":"[photo]","note":"Box arrived wet."}"""), body["state"]);
        AssertJsonEqual(JsonNode.Parse("""{"[photo]":{"type":"image","part":"image-1"}}"""), body["assets"]);
        Assert.Equal(["request", "image-1"], server.Calls[0].RequestBodyMultipart.Select(part => part.Name));
        Assert.Equal("true", server.Calls[0].RequestHeaders["typesafe-danger-zone-images"]);
    }

    [Fact]
    public async Task Leaves_text_state_unchanged_when_it_already_contains_the_reference()
    {
        var (server, model) = Create();

        await model.DoDecideAsync(
            Options(
                state:
                [
                    new DecisionTextPart("Is [photo] damaged?"),
                    FilePart("image/jpeg", Jpeg, """{"reference":"[photo]"}"""),
                ]
            ),
            CancellationToken.None
        );

        var body = MultipartBody(server.Calls[0]);
        Assert.Equal("Is [photo] damaged?", (string?)body["state"]);
        Assert.Equal(["[photo]"], body["assets"]!.AsObject().Select(pair => pair.Key));
    }

    [Fact]
    public async Task Finds_references_in_nested_JSON_string_values_but_not_in_object_keys()
    {
        var (server, model) = Create();
        const string value =
            """{"frames":[{"seconds_ago":0,"frame":"Latest frame: [frame]"}],"[label]":"A key is not a reference."}""";

        await model.DoDecideAsync(
            Options(
                state:
                [
                    new DecisionJsonPart(JsonDocument.Parse(value).RootElement),
                    FilePart("image/png", Png, """{"reference":"[frame]"}"""),
                    FilePart("image/png", Png, """{"reference":"[label]"}"""),
                ]
            ),
            CancellationToken.None
        );

        Assert.Equal(value + "\n[label]", (string?)MultipartBody(server.Calls[0])["state"]);
    }

    [Fact]
    public async Task Sends_a_question_only_image_without_touching_state_when_insertReference_is_false()
    {
        var (server, model) = Create();

        await model.DoDecideAsync(
            Options(
                state:
                [
                    new DecisionJsonPart(JsonDocument.Parse("""{"photo":"[found]"}""").RootElement),
                    FilePart("image/jpeg", Jpeg, """{"reference":"[found]"}"""),
                    FilePart("image/jpeg", Jpeg, """{"reference":"[poster]","insertReference":false}"""),
                ],
                questions: new Dictionary<string, DecisionQuestion>
                {
                    ["same"] = DecisionQuestion.Boolean("Is the dog in `photo` the dog in [poster]?"),
                }
            ),
            CancellationToken.None
        );

        var body = MultipartBody(server.Calls[0]);
        AssertJsonEqual(JsonNode.Parse("""{"photo":"[found]"}"""), body["state"]);
        Assert.Equal(["[found]", "[poster]"], body["assets"]!.AsObject().Select(pair => pair.Key));
    }

    [Fact]
    public async Task Inserts_a_reference_already_in_state_when_insertReference_is_true()
    {
        var (server, model) = Create();

        await model.DoDecideAsync(
            Options(
                state:
                [
                    new DecisionTextPart("Before: [photo]"),
                    FilePart("image/png", Png, """{"reference":"[photo]","insertReference":true}"""),
                ]
            ),
            CancellationToken.None
        );

        Assert.Equal("Before: [photo]\n[photo]", (string?)MultipartBody(server.Calls[0])["state"]);
    }

    [Fact]
    public async Task Inserts_only_the_references_that_are_not_already_placed()
    {
        var (server, model) = Create();

        await model.DoDecideAsync(
            Options(
                state:
                [
                    new DecisionTextPart("Compare [before] with the new photo."),
                    FilePart("image/png", Png, """{"reference":"[before]","insertReference":false}"""),
                    FilePart("image/png", Png, """{"reference":"[after]"}"""),
                ]
            ),
            CancellationToken.None
        );

        var body = MultipartBody(server.Calls[0]);
        Assert.Equal("Compare [before] with the new photo.\n[after]", (string?)body["state"]);
        AssertJsonEqual(
            JsonNode.Parse(
                """{"[before]":{"type":"image","part":"image-1"},"[after]":{"type":"image","part":"image-2"}}"""
            ),
            body["assets"]
        );
    }

    [Fact]
    public async Task Rejects_insertReference_false_without_an_explicit_reference_before_HTTP()
    {
        var (server, model) = Create();

        var error = await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            model.DoDecideAsync(
                Options(state: [FilePart("image/png", Png, """{"insertReference":false}""")]),
                CancellationToken.None
            )
        );

        Assert.Equal("providerOptions.typesafe.reference", error.Parameter);
        Assert.Empty(server.Calls);
    }

    [Fact]
    public async Task Rejects_duplicate_image_references_before_HTTP()
    {
        var (server, model) = Create();

        var error = await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            model.DoDecideAsync(
                Options(
                    state:
                    [
                        FilePart("image/png", Png, """{"reference":"[photo]"}"""),
                        FilePart("image/png", Jpeg, """{"reference":"[photo]"}"""),
                    ]
                ),
                CancellationToken.None
            )
        );

        Assert.Equal("providerOptions.typesafe.reference", error.Parameter);
        Assert.Equal(
            "Invalid argument for parameter providerOptions.typesafe.reference: Duplicate TypeSafe image reference: [photo]",
            error.Message
        );
        Assert.Empty(server.Calls);
    }

    [Fact]
    public async Task Maps_mixed_text_JSON_and_multiple_images_to_referenced_assets()
    {
        var (server, model) = Create();

        await model.DoDecideAsync(
            Options(
                state:
                [
                    new DecisionTextPart("Compare these images."),
                    new DecisionFilePart("image/png", [137, 80, 78, 71]),
                    new DecisionJsonPart(JsonDocument.Parse("""{"label":"second"}""").RootElement),
                    FilePart("image/jpeg", Jpeg),
                ]
            ),
            CancellationToken.None
        );

        var call = server.Calls[0];
        var multipart = call.RequestBodyMultipart;
        var body = MultipartBody(call);
        var references = body["assets"]!.AsObject().Select(pair => pair.Key).ToList();
        Assert.Equal(2, references.Count);
        Assert.NotEqual(references[0], references[1]);
        Assert.Equal(
            "Compare these images.\n" + references[0] + "\n{\"label\":\"second\"}\n" + references[1],
            (string?)body["state"]
        );
        Assert.Equal(["image-1", "image-3"], body["assets"]!.AsObject().Select(pair => (string?)pair.Value!["part"]));
        Assert.Equal(["request", "image-1", "image-3"], multipart.Select(part => part.Name));
        Assert.Equal("application/json", multipart[0].ContentType);
        Assert.Equal("image/png", multipart[1].ContentType);
        Assert.Equal("image-1.png", multipart[1].FileName);
        Assert.Equal<byte>([137, 80, 78, 71], multipart[1].Data);
        Assert.Equal("image/jpeg", multipart[2].ContentType);
        Assert.Equal<byte>([255, 216, 255, 224], multipart[2].Data);
        Assert.StartsWith(
            "multipart/form-data; boundary=",
            call.RequestHeaders["content-type"],
            StringComparison.Ordinal
        );
        Assert.Equal("true", call.RequestHeaders["typesafe-danger-zone-images"]);
    }

    [Fact]
    public async Task Omits_assets_and_the_image_header_for_text_only_input()
    {
        var (server, model) = Create();

        await model.DoDecideAsync(Options(), CancellationToken.None);

        Assert.False(server.Calls[0].RequestBodyJson!.AsObject().ContainsKey("assets"));
        Assert.False(server.Calls[0].RequestHeaders.ContainsKey("typesafe-danger-zone-images"));
    }

    [Fact]
    public async Task Uses_fresh_references_for_each_request()
    {
        var (server, model) = Create();
        var imageOptions = Options(state: [FilePart("image/png", "iVBORw0KGgo=")]);

        await model.DoDecideAsync(imageOptions, CancellationToken.None);
        await model.DoDecideAsync(imageOptions, CancellationToken.None);

        Assert.NotEqual(
            (string?)MultipartBody(server.Calls[0])["state"],
            (string?)MultipartBody(server.Calls[1])["state"]
        );
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/webp")]
    [InlineData("image/gif")]
    [InlineData("image")]
    public async Task Rejects_unsupported_or_unrecognized_media_before_HTTP(string mediaType)
    {
        var (server, model) = Create();

        await Assert.ThrowsAsync<UnsupportedFunctionalityException>(() =>
            model.DoDecideAsync(Options(state: [FilePart(mediaType, "AAAA")]), CancellationToken.None)
        );
        Assert.Empty(server.Calls);
    }

    [Theory]
    [InlineData("url")]
    [InlineData("reference")]
    [InlineData("text")]
    public async Task Rejects_unsupported_file_data_before_HTTP(string type)
    {
        var (server, model) = Create();
        var file = new DecisionFilePart("image/png", []) { DataType = type };

        await Assert.ThrowsAsync<UnsupportedFunctionalityException>(() =>
            model.DoDecideAsync(Options(state: [file]), CancellationToken.None)
        );
        Assert.Empty(server.Calls);
    }
}
