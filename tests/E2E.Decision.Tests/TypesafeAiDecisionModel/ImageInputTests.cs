// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using static E2E.Decision.Tests.TypesafeAiDecisionModel.TypesafeAiDecisionModelTests;

namespace E2E.Decision.Tests.TypesafeAiDecisionModel;

/// <summary>Ports <c>describe('image input')</c> of <c>packages/typesafe-ai/src/typesafe-ai-decision-model.test.ts</c> of the AI SDK (<c>vercel/ai</c>).</summary>
[Collection("TypesafeAi")]
public sealed class ImageInputTests
{
    private static Task<DecisionModelResult> Decide(TypeSafeServer server, DecisionModelCall call) =>
        server.Model().DoDecideAsync(call, CancellationToken.None);

    private static string Quote(string text) => JsonValue.Create(text)!.ToJsonString();

    [Theory]
    [InlineData("[animal-photo]")]
    [InlineData("__proto__")]
    [InlineData("constructor")]
    [InlineData("request")]
    public async Task Uses_an_explicit_reference_in_assets_state_instructions_and_criteria(string reference)
    {
        var server = new TypeSafeServer();
        var cat = DecisionQuestion.Boolean(
            "Does " + reference + " show a cat?",
            Input("{\"image\":" + Quote(reference) + "}")
        );

        var result = await Decide(
            server,
            Options(
                state:
                [
                    new DecisionFilePart("image/png", [0x89, 0x50, 0x4e, 0x47])
                    {
                        Filename = "unrelated-name.png",
                        ProviderOptions = Typesafe("{\"reference\":" + Quote(reference) + "}"),
                    },
                    Jpeg(),
                ],
                questions: new Dictionary<string, DecisionQuestion> { ["cat"] = cat }
            )
        );

        var call = server.Calls[0];
        var body = call.MultipartRequest();
        var references = body["assets"]!.AsObject().Select(entry => entry.Key).ToList();
        Assert.Equal(2, references.Count);
        Assert.Contains(reference, references);
        var generatedReference = references.Single(key => key != reference);
        Assert.Equal(reference + "\n" + generatedReference, body["state"]!.GetValue<string>());
        AssertJson(JsonNode.Parse("""{"type":"image","part":"image-0"}""")!, body["assets"]![reference]!);
        Assert.Equal("unrelated-name.png", call.Multipart().Single(part => part.Name == "image-0").Filename);
        var question = body["questions"]!["cat"]!;
        Assert.Equal("noul", question["type"]!.GetValue<string>());
        Assert.Equal("Does " + reference + " show a cat?", question["instructions"]!.GetValue<string>());
        AssertJson(JsonNode.Parse("{\"image\":" + Quote(reference) + "}")!, question["criteria"]!["true"]!);
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
        var server = new TypeSafeServer();

        await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            Decide(server, Options(state: [Png("{\"reference\":" + reference + "}")]))
        );
        Assert.Empty(server.Calls);
    }

    [Fact]
    public async Task Keeps_JSON_state_as_an_object_when_it_already_contains_the_reference()
    {
        var server = new TypeSafeServer();

        await Decide(
            server,
            Options(
                state:
                [
                    new DecisionJsonPart(Element("""{"photo":"[photo]","note":"Box arrived wet."}""")),
                    Png("""{"reference":"[photo]"}"""),
                ]
            )
        );

        var call = server.Calls[0];
        var body = call.MultipartRequest();
        AssertJson(JsonNode.Parse("""{"photo":"[photo]","note":"Box arrived wet."}""")!, body["state"]!);
        AssertJson(JsonNode.Parse("""{"[photo]":{"type":"image","part":"image-1"}}""")!, body["assets"]!);
        Assert.Equal(["request", "image-1"], call.Multipart().Select(part => part.Name));
        Assert.Equal("true", call.Headers["typesafe-danger-zone-images"]);
    }

    [Fact]
    public async Task Leaves_text_state_unchanged_when_it_already_contains_the_reference()
    {
        var server = new TypeSafeServer();

        await Decide(
            server,
            Options(state: [new DecisionTextPart("Is [photo] damaged?"), Jpeg("""{"reference":"[photo]"}""")])
        );

        var body = server.Calls[0].MultipartRequest();
        Assert.Equal("Is [photo] damaged?", body["state"]!.GetValue<string>());
        Assert.Equal(["[photo]"], body["assets"]!.AsObject().Select(entry => entry.Key));
    }

    [Fact]
    public async Task Finds_references_in_nested_JSON_string_values_but_not_in_object_keys()
    {
        var server = new TypeSafeServer();
        const string state =
            """{"frames":[{"seconds_ago":0,"frame":"Latest frame: [frame]"}],"[label]":"A key is not a reference."}""";

        await Decide(
            server,
            Options(
                state:
                [
                    new DecisionJsonPart(Element(state)),
                    Png("""{"reference":"[frame]"}"""),
                    Png("""{"reference":"[label]"}"""),
                ]
            )
        );

        Assert.Equal(state + "\n[label]", server.Calls[0].MultipartRequest()["state"]!.GetValue<string>());
    }

    [Fact]
    public async Task Sends_a_question_only_image_without_touching_state_when_insertReference_is_false()
    {
        var server = new TypeSafeServer();

        await Decide(
            server,
            Options(
                state:
                [
                    new DecisionJsonPart(Element("""{"photo":"[found]"}""")),
                    Jpeg("""{"reference":"[found]"}"""),
                    Jpeg("""{"reference":"[poster]","insertReference":false}"""),
                ],
                questions: new Dictionary<string, DecisionQuestion>
                {
                    ["same"] = DecisionQuestion.Boolean("Is the dog in `photo` the dog in [poster]?"),
                }
            )
        );

        var body = server.Calls[0].MultipartRequest();
        AssertJson(JsonNode.Parse("""{"photo":"[found]"}""")!, body["state"]!);
        Assert.Equal(["[found]", "[poster]"], body["assets"]!.AsObject().Select(entry => entry.Key));
    }

    [Fact]
    public async Task Inserts_a_reference_already_in_state_when_insertReference_is_true()
    {
        var server = new TypeSafeServer();

        await Decide(
            server,
            Options(
                state:
                [
                    new DecisionTextPart("Before: [photo]"),
                    Png("""{"reference":"[photo]","insertReference":true}"""),
                ]
            )
        );

        Assert.Equal("Before: [photo]\n[photo]", server.Calls[0].MultipartRequest()["state"]!.GetValue<string>());
    }

    [Fact]
    public async Task Inserts_only_the_references_that_are_not_already_placed()
    {
        var server = new TypeSafeServer();

        await Decide(
            server,
            Options(
                state:
                [
                    new DecisionTextPart("Compare [before] with the new photo."),
                    Png("""{"reference":"[before]","insertReference":false}"""),
                    Png("""{"reference":"[after]"}"""),
                ]
            )
        );

        var body = server.Calls[0].MultipartRequest();
        Assert.Equal("Compare [before] with the new photo.\n[after]", body["state"]!.GetValue<string>());
        AssertJson(
            JsonNode.Parse(
                """{"[before]":{"type":"image","part":"image-1"},"[after]":{"type":"image","part":"image-2"}}"""
            )!,
            body["assets"]!
        );
    }

    [Fact]
    public async Task Rejects_insertReference_false_without_an_explicit_reference_before_HTTP()
    {
        var server = new TypeSafeServer();

        var error = await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            Decide(server, Options(state: [Png("""{"insertReference":false}""")]))
        );

        Assert.Equal("providerOptions.typesafe.reference", error.Parameter);
        Assert.Empty(server.Calls);
    }

    [Fact]
    public async Task Rejects_duplicate_image_references_before_HTTP()
    {
        var server = new TypeSafeServer();

        var error = await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            Decide(
                server,
                Options(state: [Png("""{"reference":"[photo]"}"""), Png("""{"reference":"[photo]"}""", [0xff, 0xd8])])
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
        var server = new TypeSafeServer();

        await Decide(
            server,
            Options(
                state:
                [
                    new DecisionTextPart("Compare these images."),
                    Png(),
                    new DecisionJsonPart(Element("""{"label":"second"}""")),
                    Jpeg(),
                ]
            )
        );

        var call = server.Calls[0];
        var body = call.MultipartRequest();
        var assets = body["assets"]!.AsObject();
        var references = assets.Select(entry => entry.Key).ToList();
        Assert.Equal(2, references.Count);
        Assert.NotEqual(references[0], references[1]);
        Assert.Equal(
            "Compare these images.\n" + references[0] + "\n{\"label\":\"second\"}\n" + references[1],
            body["state"]!.GetValue<string>()
        );
        Assert.Equal(
            ["""{"type":"image","part":"image-1"}""", """{"type":"image","part":"image-3"}"""],
            assets.Select(entry => entry.Value!.ToJsonString())
        );
        var parts = call.Multipart();
        Assert.Equal(["request", "image-1", "image-3"], parts.Select(part => part.Name));
        Assert.Equal("application/json", parts[0].ContentType);
        Assert.Equal("image/png", parts[1].ContentType);
        Assert.Equal("image-1.png", parts[1].Filename);
        Assert.Equal<byte>([137, 80, 78, 71], parts[1].Data);
        Assert.Equal("image/jpeg", parts[2].ContentType);
        Assert.Equal<byte>([255, 216, 255, 224], parts[2].Data);
        Assert.StartsWith("multipart/form-data; boundary=", call.Headers["content-type"], StringComparison.Ordinal);
        Assert.Equal("true", call.Headers["typesafe-danger-zone-images"]);
    }

    [Fact]
    public async Task Omits_assets_and_the_image_header_for_text_only_input()
    {
        var server = new TypeSafeServer();

        await Decide(server, Options());

        Assert.False(server.Calls[0].Json().AsObject().ContainsKey("assets"));
        Assert.False(server.Calls[0].Headers.ContainsKey("typesafe-danger-zone-images"));
    }

    [Fact]
    public async Task Uses_fresh_references_for_each_request()
    {
        var server = new TypeSafeServer();
        var call = Options(
            state: [new DecisionFilePart("image/png", [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a])]
        );

        await Decide(server, call);
        await Decide(server, call);

        Assert.NotEqual(
            server.Calls[0].MultipartRequest()["state"]!.GetValue<string>(),
            server.Calls[1].MultipartRequest()["state"]!.GetValue<string>()
        );
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/webp")]
    [InlineData("image/gif")]
    [InlineData("image")]
    public async Task Rejects_unsupported_or_unrecognized_media_before_HTTP(string mediaType)
    {
        var server = new TypeSafeServer();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            Decide(server, Options(state: [new DecisionFilePart(mediaType, [0, 0, 0])]))
        );
        Assert.Empty(server.Calls);
    }
}
