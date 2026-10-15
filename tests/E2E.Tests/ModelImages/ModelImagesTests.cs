// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Tests.ModelImages;

/// <summary>
/// The image part each provider puts in a <c>user</c> turn. Upstream hands images to the AI SDK, which builds the
/// provider bodies, so there is no upstream <c>it</c> to port: these tests are .NET-only, like the rest of the model providers.
/// </summary>
public sealed class ModelImagesTests
{
    private static readonly byte[] Bytes = [1, 2, 3, 4];

    private static readonly string Encoded = Convert.ToBase64String(Bytes);

    private static ModelImage Image(string mediaType = "image/png") => new(Bytes, mediaType, 2, 2);

    private static ModelRequest Request(params ModelMessage[] messages) => new()
    {
        System = "rules",
        Tools = ModelProviderTests.Tools(),
        Messages = messages,
    };

    private static ModelMessage User(string text, params ModelImage[] images) => new() { Role = "user", Content = text, Images = images };

    private static string Json(JsonNode? node) => node!.ToJsonString();

    [Fact]
    public void Chat_completions_sends_an_image_url_part_with_a_data_URL_after_the_text()
    {
        using var model = new OpenAiCompatibleModel(new OpenAiCompatibleModelOptions { Model = "m", ApiKey = "k" });
        var body = model.BuildBody(Request(User("look", Image())));
        var user = body["messages"]![1]!;
        Assert.Equal(
            Json(JsonNode.Parse($$"""[{ "type": "text", "text": "look" }, { "type": "image_url", "image_url": { "url": "data:image/png;base64,{{Encoded}}" } }]""")),
            Json(user["content"]));
    }

    [Fact]
    public void Responses_sends_an_input_image_part_after_the_text()
    {
        using var model = new OpenAiResponsesModel(new OpenAiResponsesModelOptions { Model = "m", ApiKey = "k" });
        var body = model.BuildBody(Request(User("look", Image())));
        Assert.Equal(
            Json(JsonNode.Parse($$"""[{ "type": "input_text", "text": "look" }, { "type": "input_image", "image_url": "data:image/png;base64,{{Encoded}}" }]""")),
            Json(body["input"]![0]!["content"]));
    }

    [Fact]
    public void Anthropic_sends_a_base64_image_block_after_the_text()
    {
        using var model = new AnthropicModel(new AnthropicModelOptions { Model = "m", ApiKey = "k" });
        var body = model.BuildBody(Request(User("look", Image())));
        Assert.Equal(
            Json(JsonNode.Parse($$"""[{ "type": "text", "text": "look" }, { "type": "image", "source": { "type": "base64", "media_type": "image/png", "data": "{{Encoded}}" } }]""")),
            Json(body["messages"]![0]!["content"]));
    }

    [Fact]
    public void Google_sends_an_inline_data_part_after_the_text()
    {
        using var model = new GoogleModel(new GoogleModelOptions { Model = "m", ApiKey = "k" });
        var body = model.BuildBody(Request(User("look", Image())));
        Assert.Equal(
            Json(JsonNode.Parse($$"""[{ "text": "look" }, { "inlineData": { "mimeType": "image/png", "data": "{{Encoded}}" } }]""")),
            Json(body["contents"]![0]!["parts"]));
    }

    [Theory]
    [InlineData("image/png", "png")]
    [InlineData("image/jpeg", "jpeg")]
    [InlineData("image/jpg", "jpeg")]
    public void Bedrock_sends_an_image_block_with_the_format_name_after_the_text(string mediaType, string format)
    {
        using var model = new BedrockModel(new BedrockModelOptions { Model = "m", Region = "us-east-1", ApiKey = "k" });
        var body = model.BuildBody(Request(User("look", Image(mediaType))));
        Assert.Equal(
            Json(JsonNode.Parse($$"""[{ "text": "look" }, { "image": { "format": "{{format}}", "source": { "bytes": "{{Encoded}}" } } }]""")),
            Json(body["messages"]![0]!["content"]));
    }

    [Fact]
    public void Sends_every_image_of_a_turn_in_order()
    {
        using var model = new AnthropicModel(new AnthropicModelOptions { Model = "m", ApiKey = "k" });
        var second = new ModelImage([9], "image/png", 1, 1);
        var content = model.BuildBody(Request(User("look", Image(), second)))["messages"]![0]!["content"]!.AsArray();
        Assert.Equal(["text", "image", "image"], content.Select(part => part!["type"]!.GetValue<string>()));
        Assert.Equal(Convert.ToBase64String([9]), content[2]!["source"]!["data"]!.GetValue<string>());
    }

    [Fact]
    public void Leaves_a_turn_without_images_as_plain_text()
    {
        using var chat = new OpenAiCompatibleModel(new OpenAiCompatibleModelOptions { Model = "m", ApiKey = "k" });
        Assert.Equal("look", chat.BuildBody(Request(User("look")))["messages"]![1]!["content"]!.GetValue<string>());
        using var responses = new OpenAiResponsesModel(new OpenAiResponsesModelOptions { Model = "m", ApiKey = "k" });
        Assert.Single(responses.BuildBody(Request(User("look")))["input"]![0]!["content"]!.AsArray());
    }

    [Fact]
    public void Sends_no_image_with_a_turn_that_is_not_from_the_user()
    {
        var assistant = new ModelMessage { Role = "assistant", Content = "said", Images = [Image()] };
        using var anthropic = new AnthropicModel(new AnthropicModelOptions { Model = "m", ApiKey = "k" });
        Assert.DoesNotContain("image", anthropic.BuildBody(Request(assistant)).ToJsonString(), StringComparison.Ordinal);
        using var chat = new OpenAiCompatibleModel(new OpenAiCompatibleModelOptions { Model = "m", ApiKey = "k" });
        Assert.DoesNotContain("image_url", chat.BuildBody(Request(assistant)).ToJsonString(), StringComparison.Ordinal);
    }
}
