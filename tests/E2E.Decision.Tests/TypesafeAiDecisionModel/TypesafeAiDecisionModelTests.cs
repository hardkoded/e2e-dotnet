// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using static E2E.Decision.Tests.TypesafeAiDecisionModel.TypeSafeServer;

namespace E2E.Decision.Tests.TypesafeAiDecisionModel;

/// <summary>Ports the tests outside any <c>describe</c> of <c>packages/typesafe-ai/src/typesafe-ai-decision-model.test.ts</c> of the AI SDK (<c>vercel/ai</c>).</summary>
[Collection("TypesafeAi")]
public sealed class TypesafeAiDecisionModelTests
{
    internal static JsonElement Element(string json) => JsonDocument.Parse(json).RootElement.Clone();

    internal static DecisionInput Input(string json) => DecisionInput.FromJson(Element(json));

    internal static IReadOnlyDictionary<string, JsonElement> Typesafe(string json) =>
        new Dictionary<string, JsonElement> { ["typesafe"] = Element(json) };

    internal static DecisionFilePart Png(string? options = null, byte[]? data = null) =>
        new("image/png", data ?? [0x89, 0x50, 0x4e, 0x47])
        {
            ProviderOptions = options is null ? null : Typesafe(options),
        };

    internal static DecisionFilePart Jpeg(string? options = null) =>
        new("image/jpeg", [0xff, 0xd8, 0xff, 0xe0]) { ProviderOptions = options is null ? null : Typesafe(options) };

    internal static IReadOnlyDictionary<string, DecisionQuestion> Questions { get; } =
        new Dictionary<string, DecisionQuestion>
        {
            ["department"] = DecisionQuestion.Choice(
                Input("""{"question":"Which team should handle this?"}"""),
                [
                    new DecisionChoice("billing", Input("""{"includes":["Charges","Invoices","Refunds"]}""")),
                    new DecisionChoice("technical", Input("""["Bugs","Outages"]""")),
                    new DecisionChoice("other"),
                ]
            ),
            ["severity"] = DecisionQuestion.Score(
                Input("""["How severe is the issue?"]"""),
                [
                    Input("""{"meaning":"Cosmetic; functionality works"}"""),
                    "Functionality impaired; workaround exists",
                    "Blocking; no workaround",
                ]
            ),
            ["requestsRefund"] = DecisionQuestion.Boolean(
                "Is the customer requesting a refund?",
                Input("""{"meaning":"Explicit request for money back"}""")
            ),
        };

    internal static DecisionModelCall Options(
        IReadOnlyList<DecisionStatePart>? state = null,
        IReadOnlyDictionary<string, DecisionQuestion>? questions = null,
        IReadOnlyDictionary<string, JsonElement>? providerOptions = null,
        IReadOnlyDictionary<string, string>? headers = null
    ) =>
        new()
        {
            State = state ?? [new DecisionJsonPart(Element(NativeRequest["state"]!.ToJsonString()))],
            Questions = questions ?? Questions,
            ProviderOptions = providerOptions,
            Headers = headers ?? new Dictionary<string, string>(),
        };

    internal static void AssertJson(JsonNode expected, JsonNode actual) =>
        Assert.True(
            JsonNode.DeepEquals(expected, actual),
            "Expected " + expected.ToJsonString() + " but got " + actual.ToJsonString()
        );

    [Fact]
    public async Task Sends_all_three_question_types_and_structured_rubrics_in_one_request()
    {
        var server = new TypeSafeServer();
        await server.Model().DoDecideAsync(Options(), CancellationToken.None);

        Assert.Single(server.Calls);
        AssertJson(NativeRequest, server.Calls[0].Json());
        Assert.Equal("boolean", Questions["requestsRefund"].Type);
        Assert.Equal("Bearer test-api-key", server.Calls[0].Headers["authorization"]);
        Assert.Equal("application/json", server.Calls[0].Headers["content-type"]);
    }

    [Fact]
    public async Task Preserves_native_scores_probabilities_confidence_metadata_and_usage()
    {
        var server = new TypeSafeServer();
        var result = await server.Model().DoDecideAsync(Options(), CancellationToken.None);

        var department = result.Answers["department"];
        Assert.Equal(("choice", "billing"), (department.Type, department.Choice));
        Assert.Equal(
            new Dictionary<string, double>
            {
                ["technical"] = 0,
                ["other"] = 0,
                ["billing"] = 1,
            },
            department.Probabilities
        );
        var severity = result.Answers["severity"];
        Assert.Equal(("score", 0.97), (severity.Type, severity.Score));
        Assert.Equal(
            new Dictionary<string, double>
            {
                ["0"] = 0.13,
                ["1"] = 0.76,
                ["2"] = 0.11,
            },
            severity.Probabilities
        );
        Assert.Equal(
            ("boolean", 0.99),
            (result.Answers["requestsRefund"].Type, result.Answers["requestsRefund"].Probability)
        );
        Assert.Equal(new DecisionRounding(2, 2), result.Rounding);
        AssertJson(
            JsonNode.Parse(
                new JsonObject
                {
                    ["typesafe"] = new JsonObject
                    {
                        ["confidence"] = new JsonObject { ["department"] = 1, ["severity"] = 0.64 },
                        ["legend"] = new JsonObject
                        {
                            ["severity"] = NativeResponse["answers"]!["severity"]!["legend"]!.DeepClone(),
                        },
                    },
                }.ToJsonString()
            )!,
            JsonNode.Parse(result.ProviderMetadata!.Value.GetRawText())!
        );
        Assert.Equal(new DecisionUsage(471, 71), result.Usage);
        Assert.Equal("jev-1.13.0", result.ResponseModelId);
        AssertJson(NativeResponse, JsonNode.Parse(result.ResponseBody!.Value.GetRawText())!);
    }

    [Fact]
    public async Task Passes_custom_http_client_headers_base_URL_and_future_model_IDs()
    {
        var server = new TypeSafeServer();
        var custom = TypeSafeAi.Create(
            new TypeSafeAiProviderSettings
            {
                ApiKey = "custom-key",
                BaseUrl = "https://custom.example/v1/",
                Headers = new Dictionary<string, string> { ["custom"] = "provider", ["shared"] = "provider" },
                HttpClient = new HttpClient(server),
            }
        );

        var result = await custom
            .DecisionModel("jev-future")
            .DoDecideAsync(
                Options(headers: new Dictionary<string, string> { ["shared"] = "call" }),
                CancellationToken.None
            );

        var call = Assert.Single(server.Calls);
        Assert.Equal("https://custom.example/v1/systemone", call.Url);
        Assert.Equal("Bearer custom-key", call.Headers["authorization"]);
        Assert.Equal("provider", call.Headers["custom"]);
        Assert.Equal("call", call.Headers["shared"]);
        Assert.Contains("ai-sdk-typesafe-ai/", call.Headers["user-agent"], StringComparison.Ordinal);
        Assert.Equal("jev-future", call.Json()["model"]!.GetValue<string>());
        Assert.True(result.ResponseHeaders!.ContainsKey("content-type"));
    }

    [Fact]
    public async Task Loads_TYPESAFE_AI_API_KEY_lazily_from_the_environment()
    {
        var server = new TypeSafeServer();
        var model = TypeSafeAi
            .Create(new TypeSafeAiProviderSettings { HttpClient = new HttpClient(server) })
            .DecisionModel("jev-latest");
        Environment.SetEnvironmentVariable("TYPESAFE_AI_API_KEY", "environment-key");
        try
        {
            await model.DoDecideAsync(Options(), CancellationToken.None);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TYPESAFE_AI_API_KEY", null);
        }

        Assert.Equal("Bearer environment-key", server.Calls[0].Headers["authorization"]);
    }

    [Fact]
    public async Task Reports_unsupported_provider_options_without_passing_them_through()
    {
        var server = new TypeSafeServer();
        var result = await server
            .Model()
            .DoDecideAsync(Options(providerOptions: Typesafe("""{"temperature":0}""")), CancellationToken.None);

        Assert.Equal(["unsupported setting: providerOptions.typesafe.temperature"], result.Warnings);
        AssertJson(NativeRequest, server.Calls[0].Json());
    }

    [Theory]
    [InlineData("jev-1.13.0")]
    [InlineData("jev-1.14.0")]
    [InlineData("jev-latest")]
    [InlineData("jev-preview")]
    public async Task Sends_the_model_ID_or_alias_unchanged(string modelId)
    {
        var server = new TypeSafeServer();
        await server.Model(modelId).DoDecideAsync(Options(), CancellationToken.None);

        var expected = NativeRequest.DeepClone();
        expected["model"] = modelId;
        AssertJson(expected, server.Calls[0].Json());
    }

    public static TheoryData<string, int[]> ProviderLimits =>
        new()
        {
            { "choice", [] },
            { "choice", [256] },
            { "score", [11] },
            { "score", [] },
            { "score", [-1] },
            { "score", [1, -1] },
        };

    [Theory]
    [MemberData(nameof(ProviderLimits))]
    public async Task Rejects_provider_limits_before_HTTP_for_type(string type, int[] shape)
    {
        var server = new TypeSafeServer();
        var question =
            type == "choice"
                ? DecisionQuestion.Choice(
                    shape.Length == 0 ? null : "Pick",
                    [
                        .. Enumerable
                            .Range(0, shape.Length == 0 ? 0 : shape[0])
                            .Select(index => new DecisionChoice(
                                index.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            )),
                    ]
                )
                : DecisionQuestion.Score(
                    shape is [11] ? "Rate" : null,
                    shape switch
                    {
                        [11] => [.. Enumerable.Range(0, 11).Select(index => (DecisionInput?)("Level " + index))],
                        [-1] => [null],
                        [1, -1] => ["Low", null],
                        _ => [],
                    }
                );

        await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            server
                .Model()
                .DoDecideAsync(
                    Options(
                        state: [new DecisionTextPart("test")],
                        questions: new Dictionary<string, DecisionQuestion> { ["question"] = question }
                    ),
                    CancellationToken.None
                )
        );
        Assert.Empty(server.Calls);
    }

    [Fact]
    public async Task Accepts_null_optional_metadata_without_inventing_usage()
    {
        var server = new TypeSafeServer();
        server.RespondJson(
            JsonNode.Parse(
                """{"model":null,"usage":null,"answers":{"topic":{"type":"choice","choice":"a","probabilities":{"a":1},"confidence":null}}}"""
            )!
        );

        var result = await server.Model().DoDecideAsync(Options(), CancellationToken.None);

        Assert.Equal(new DecisionUsage(null, null), result.Usage);
        Assert.Equal("jev-latest", result.ResponseModelId);
        Assert.Equal("""{"typesafe":{"confidence":{},"legend":{}}}""", result.ProviderMetadata!.Value.GetRawText());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accepts_a_nullish_score_legend(bool omitted)
    {
        var server = new TypeSafeServer();
        var response = NativeResponse.DeepClone();
        var severity = response["answers"]!["severity"]!.AsObject();
        severity.Remove("legend");
        if (!omitted)
        {
            severity["legend"] = null;
        }

        server.RespondJson(response);

        var result = await server.Model().DoDecideAsync(Options(), CancellationToken.None);

        Assert.Equal("{}", result.ProviderMetadata!.Value.GetProperty("typesafe").GetProperty("legend").GetRawText());
    }

    [Fact]
    public async Task Preserves_structured_score_legends_in_provider_metadata_and_the_raw_response()
    {
        var legend = JsonNode.Parse(
            """{"0":{"meaning":"Cosmetic","examples":["Color mismatch"]},"1":["Workaround exists",{"impact":"Limited"}],"2":"Blocking"}"""
        )!;
        var response = NativeResponse.DeepClone();
        response["answers"]!["severity"]!["legend"] = legend.DeepClone();
        var server = new TypeSafeServer();
        server.RespondJson(response);

        var result = await server.Model().DoDecideAsync(Options(), CancellationToken.None);

        AssertJson(
            new JsonObject { ["severity"] = legend },
            JsonNode.Parse(result.ProviderMetadata!.Value.GetProperty("typesafe").GetProperty("legend").GetRawText())!
        );
        AssertJson(response, JsonNode.Parse(result.ResponseBody!.Value.GetRawText())!);
    }

    [Fact]
    public async Task Forwards_cancellation_to_the_request()
    {
        var server = new TypeSafeServer();
        using var cancellation = new CancellationTokenSource();
        server.OnSend = token =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            server.Model().DoDecideAsync(Options(), cancellation.Token)
        );
        Assert.Single(server.Calls);
    }

    [Fact]
    public async Task Maps_text_and_JSON_parts_to_native_text_state()
    {
        var server = new TypeSafeServer();
        await server
            .Model()
            .DoDecideAsync(
                Options(state: [new DecisionTextPart("Inspect."), new DecisionJsonPart(Element("[1,null]"))]),
                CancellationToken.None
            );

        Assert.Equal("Inspect.\n[1,null]", server.Calls[0].Json()["state"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("""{"nested":{"amount":49},"history":["refund"]}""")]
    [InlineData("[1,null]")]
    public async Task Preserves_a_single_JSON_state_part_as_native_JSON(string value)
    {
        var server = new TypeSafeServer();
        await server
            .Model()
            .DoDecideAsync(Options(state: [new DecisionJsonPart(Element(value))]), CancellationToken.None);

        AssertJson(JsonNode.Parse(value)!, server.Calls[0].Json()["state"]!);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("42")]
    public async Task Serializes_a_primitive_JSON_state_part_as_text(string value)
    {
        var server = new TypeSafeServer();
        await server
            .Model()
            .DoDecideAsync(Options(state: [new DecisionJsonPart(Element(value))]), CancellationToken.None);

        Assert.Equal(value, server.Calls[0].Json()["state"]!.GetValue<string>());
    }

    [Fact]
    public async Task Preserves_a_single_JSON_string_state_without_adding_quotes()
    {
        var server = new TypeSafeServer();
        await server
            .Model()
            .DoDecideAsync(
                Options(state: [new DecisionJsonPart(Element("\"Customer requests a refund.\""))]),
                CancellationToken.None
            );

        Assert.Equal("Customer requests a refund.", server.Calls[0].Json()["state"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public async Task Supports_non_null_score_levels(int length)
    {
        var criteria = Enumerable.Range(0, length).Select(index => "Level " + index).ToList();
        var legend = new JsonObject();
        var probabilities = new JsonObject();
        for (var index = 0; index < length; index++)
        {
            legend[index.ToString(System.Globalization.CultureInfo.InvariantCulture)] = criteria[index];
            probabilities[index.ToString(System.Globalization.CultureInfo.InvariantCulture)] = index == 0 ? 1 : 0;
        }

        var response = NativeResponse.DeepClone();
        response["answers"] = new JsonObject
        {
            ["rating"] = new JsonObject
            {
                ["type"] = "score",
                ["score"] = 0,
                ["probabilities"] = probabilities,
                ["confidence"] = 1,
                ["legend"] = legend.DeepClone(),
            },
        };
        var server = new TypeSafeServer();
        server.RespondJson(response);

        var result = await server
            .Model()
            .DoDecideAsync(
                Options(
                    questions: new Dictionary<string, DecisionQuestion>
                    {
                        ["rating"] = DecisionQuestion.Score(null, [.. criteria.Select(level => (DecisionInput?)level)]),
                    }
                ),
                CancellationToken.None
            );

        var sent = server.Calls[0].Json()["questions"]!["rating"]!;
        Assert.Equal("score", sent["type"]!.GetValue<string>());
        AssertJson(new JsonArray([.. criteria.Select(level => (JsonNode?)level)]), sent["criteria"]!);
        Assert.Equal(("score", 0d), (result.Answers["rating"].Type, result.Answers["rating"].Score));
        AssertJson(
            new JsonObject { ["rating"] = legend },
            JsonNode.Parse(result.ProviderMetadata!.Value.GetProperty("typesafe").GetProperty("legend").GetRawText())!
        );
    }

    [Theory]
    [InlineData("omitted")]
    [InlineData("null")]
    public async Task Forwards_optional_box_instructions(string instructions)
    {
        var box = new DecisionBox(0.25, 0.82, 0.44, 0.84);
        var response = NativeResponse.DeepClone();
        response["answers"] = JsonNode.Parse(
            """{"target":{"type":"box","box":{"top":0.25,"bottom":0.82,"left":0.44,"right":0.84},"confidence":null}}"""
        );
        var server = new TypeSafeServer();
        server.RespondJson(response);
        var question = new DecisionQuestion
        {
            Type = "box",
            Instructions = instructions == "null" ? Input("null") : null,
        };

        var result = await server
            .Model()
            .DoDecideAsync(
                Options(questions: new Dictionary<string, DecisionQuestion> { ["target"] = question }),
                CancellationToken.None
            );

        var expected = new JsonObject { ["type"] = "box" };
        if (instructions == "null")
        {
            expected["instructions"] = null;
        }

        AssertJson(expected, server.Calls[0].Json()["questions"]!["target"]!);
        Assert.Equal("true", server.Calls[0].Headers["typesafe-danger-zone-images"]);
        Assert.Equal(("box", box), (result.Answers["target"].Type, result.Answers["target"].Box));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sends_box_questions_and_enables_the_images_header(bool withImage)
    {
        var box = new DecisionBox(0.25, 0.82, 0.44, 0.84);
        var response = NativeResponse.DeepClone();
        response["answers"]!.AsObject()["target"] = JsonNode.Parse(
            """{"type":"box","box":{"top":0.25,"bottom":0.82,"left":0.44,"right":0.84},"confidence":0.92}"""
        );
        var server = new TypeSafeServer();
        server.RespondJson(response);
        var model = server.Model();
        var questions = new Dictionary<string, DecisionQuestion>(Questions)
        {
            ["target"] = new DecisionQuestion { Type = "box", Instructions = "The mug" },
        };

        var result = await model.DoDecideAsync(
            Options(state: withImage ? [Png(data: [1, 2, 3])] : null, questions: questions),
            CancellationToken.None
        );

        Assert.Contains("box", model.SupportedQuestionTypes);
        Assert.Equal(("box", box), (result.Answers["target"].Type, result.Answers["target"].Box));
        Assert.Equal(
            0.92,
            result
                .ProviderMetadata!.Value.GetProperty("typesafe")
                .GetProperty("confidence")
                .GetProperty("target")
                .GetDouble()
        );
        Assert.Equal("true", server.Calls[0].Headers["typesafe-danger-zone-images"]);
        var body = withImage ? server.Calls[0].MultipartRequest() : server.Calls[0].Json();
        AssertJson(JsonNode.Parse("""{"type":"box","instructions":"The mug"}""")!, body["questions"]!["target"]!);
    }

    [Fact]
    public async Task Warns_about_unsupported_maxOutputTokens_without_sending_it()
    {
        var server = new TypeSafeServer();
        var call = new DecisionModelCall
        {
            State = Options().State,
            Questions = Questions,
            MaxOutputTokens = 512,
        };

        var result = await server.Model().DoDecideAsync(call, CancellationToken.None);

        Assert.Equal(["unsupported setting: maxOutputTokens"], result.Warnings);
        AssertJson(NativeRequest, server.Calls[0].Json());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("none")]
    [InlineData("provider-default")]
    [InlineData("high")]
    public async Task Warns_about_unsupported_reasoning_without_sending_it(string? reasoning)
    {
        var server = new TypeSafeServer();
        var call = new DecisionModelCall
        {
            State = Options().State,
            Questions = Questions,
            Reasoning = reasoning,
        };

        var result = await server.Model().DoDecideAsync(call, CancellationToken.None);

        Assert.Equal(reasoning is null ? [] : ["unsupported setting: reasoning"], result.Warnings);
        AssertJson(NativeRequest, server.Calls[0].Json());
    }
}
