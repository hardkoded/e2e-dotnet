// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.TypeSafeAi;
using static E2E.Decision.Tests.TypeSafeAiDecisionModel.TypeSafeAiDecisionModelFixture;
using Provider = E2E.Decision.Sdk.TypeSafeAi.TypeSafeAiProvider;

namespace E2E.Decision.Tests.TypeSafeAiDecisionModel;

/// <summary>The <c>it</c>s outside any <c>describe</c> of upstream's <c>typesafe-ai-decision-model.test.ts</c>.</summary>
[Collection("TypeSafeAiEnvironment")]
public sealed class TypeSafeAiDecisionModelTests
{
    [Fact]
    public async Task Sends_all_three_question_types_and_structured_rubrics_in_one_request()
    {
        var (server, model) = Create();

        await model.DoDecideAsync(Options(), CancellationToken.None);

        Assert.Single(server.Calls);
        AssertJsonEqual(ExpectedRequest, server.Calls[0].RequestBodyJson);
        Assert.Equal("boolean", NativeQuestions["requestsRefund"].Type);
        Assert.Equal("Bearer test-api-key", server.Calls[0].RequestHeaders["authorization"]);
        Assert.StartsWith("application/json", server.Calls[0].RequestHeaders["content-type"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preserves_native_scores_probabilities_confidence_metadata_and_usage()
    {
        var (_, model) = Create();

        var result = await model.DoDecideAsync(Options(), CancellationToken.None);

        Assert.Equal(["department", "severity", "requestsRefund"], result.Answers.Keys);
        Assert.Equal("choice", result.Answers["department"].Type);
        Assert.Equal("billing", result.Answers["department"].Choice);
        Assert.Equal(
            new Dictionary<string, double>
            {
                ["technical"] = 0,
                ["other"] = 0,
                ["billing"] = 1,
            },
            result.Answers["department"].Probabilities
        );
        Assert.Equal("score", result.Answers["severity"].Type);
        Assert.Equal(0.97, result.Answers["severity"].Score);
        Assert.Equal(
            new Dictionary<string, double>
            {
                ["0"] = 0.13,
                ["1"] = 0.76,
                ["2"] = 0.11,
            },
            result.Answers["severity"].Probabilities
        );
        Assert.Equal("boolean", result.Answers["requestsRefund"].Type);
        Assert.Equal(0.99, result.Answers["requestsRefund"].Probability);
        Assert.Equal(new DecisionRounding(2, 2), result.Rounding);
        var expectedMetadata = new JsonObject
        {
            ["typesafe"] = new JsonObject
            {
                ["confidence"] = new JsonObject { ["department"] = 1, ["severity"] = 0.64 },
                ["legend"] = new JsonObject
                {
                    ["severity"] = NativeResponse["answers"]!["severity"]!["legend"]!.DeepClone(),
                },
            },
        };
        AssertJsonEqual(expectedMetadata, JsonNode.Parse(result.ProviderMetadata!.Value.GetRawText()));
        Assert.Equal(new DecisionUsage(471, 71), result.Usage);
        Assert.Equal("jev-1.13.0", result.ResponseModelId);
        AssertJsonEqual(NativeResponse, JsonNode.Parse(result.ResponseBody!.Value.GetRawText()));
    }

    [Fact]
    public async Task Passes_custom_fetch_headers_base_URL_and_future_model_IDs()
    {
        var server = new TestServer();
        server.RespondJson(
            "https://custom.example/v1/systemone",
            NativeResponse.ToJsonString(),
            new Dictionary<string, string> { ["x-request-id"] = "test-id" }
        );
        var custom = new Provider(
            new TypeSafeAiProviderSettings
            {
                ApiKey = "custom-key",
                BaseUrl = "https://custom.example/v1/",
                HttpClient = server.Client,
                Headers = new Dictionary<string, string> { ["custom"] = "provider", ["shared"] = "provider" },
            }
        );

        var result = await custom
            .DecisionModel("jev-future")
            .DoDecideAsync(
                Options(headers: new Dictionary<string, string> { ["shared"] = "call" }),
                CancellationToken.None
            );

        var call = Assert.Single(server.Calls);
        Assert.Equal("https://custom.example/v1/systemone", call.RequestUrl);
        Assert.Equal("Bearer custom-key", call.RequestHeaders["authorization"]);
        Assert.Equal("provider", call.RequestHeaders["custom"]);
        Assert.Equal("call", call.RequestHeaders["shared"]);
        // The port names itself in the user agent (never another client's name), so the upstream package suffix is not sent.
        Assert.StartsWith("e2e-dotnet/", call.RequestHeaders["user-agent"], StringComparison.Ordinal);
        Assert.Equal("jev-future", (string?)call.RequestBodyJson!["model"]);
        Assert.Equal("test-id", result.ResponseHeaders!["x-request-id"]);
    }

    [Fact]
    public async Task Loads_TYPESAFE_AI_API_KEY_lazily_from_the_environment()
    {
        var server = new TestServer();
        server.RespondJson(Url, NativeResponse.ToJsonString());
        var environmentModel = new Provider(
            new TypeSafeAiProviderSettings { HttpClient = server.Client }
        ).DecisionModel("jev-latest");
        var previous = Environment.GetEnvironmentVariable("TYPESAFE_AI_API_KEY");
        Environment.SetEnvironmentVariable("TYPESAFE_AI_API_KEY", "environment-key");
        try
        {
            await environmentModel.DoDecideAsync(Options(), CancellationToken.None);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TYPESAFE_AI_API_KEY", previous);
        }

        Assert.Equal("Bearer environment-key", server.Calls[0].RequestHeaders["authorization"]);
    }

    [Fact]
    public async Task Reports_unsupported_provider_options_without_passing_them_through()
    {
        var (server, model) = Create();

        var result = await model.DoDecideAsync(
            Options(providerOptions: """{"typesafe":{"temperature":0}}"""),
            CancellationToken.None
        );

        Assert.Equal(["unsupported setting: providerOptions.typesafe.temperature"], result.Warnings);
        AssertJsonEqual(ExpectedRequest, server.Calls[0].RequestBodyJson);
    }

    [Theory]
    [InlineData("jev-1.13.0")]
    [InlineData("jev-1.14.0")]
    [InlineData("jev-latest")]
    [InlineData("jev-preview")]
    public async Task Sends_the_model_ID_or_alias_unchanged(string modelId)
    {
        var (server, model) = Create(modelId);

        await model.DoDecideAsync(Options(), CancellationToken.None);

        var expected = ExpectedRequest;
        expected["model"] = modelId;
        AssertJsonEqual(expected, server.Calls[0].RequestBodyJson);
    }

    public static TheoryData<string, string> ProviderLimits =>
        new()
        {
            { "choice", "empty" },
            { "choice", "256 options" },
            { "score", "11 levels" },
            { "score", "no levels" },
            { "score", "a null level" },
            { "score", "a null level after a level" },
        };

    [Theory]
    [MemberData(nameof(ProviderLimits))]
    public async Task Rejects_provider_limits_before_HTTP_for_type(string type, string limit)
    {
        var (server, model) = Create();
        DecisionQuestion question = limit switch
        {
            "empty" => DecisionQuestion.Choice(null, []),
            "256 options" => DecisionQuestion.Choice(
                "Pick",
                Enumerable.Range(0, 256).Select(index => new DecisionChoice(index.ToString())).ToList()
            ),
            "11 levels" => DecisionQuestion.Score(
                "Rate",
                Enumerable.Range(0, 11).Select(index => (DecisionInput?)("Level " + index)).ToList()
            ),
            "no levels" => DecisionQuestion.Score(null, []),
            "a null level" => DecisionQuestion.Score(null, [null]),
            _ => DecisionQuestion.Score(null, ["Low", null]),
        };
        Assert.Equal(type, question.Type);

        await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            model.DoDecideAsync(
                Options(
                    state: [new DecisionTextPart("test")],
                    questions: new Dictionary<string, DecisionQuestion> { ["question"] = question }
                ),
                CancellationToken.None
            )
        );
        Assert.Empty(server.Calls);
    }

    // C# has no undefined: a missing score level is the null the cases above send, so both undefined cases are rejected as null levels.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rejects_undefined_score_levels_before_HTTP(bool afterALevel)
    {
        var (server, model) = Create();
        DecisionInput?[] levels = afterALevel ? ["Low", null] : [null];

        await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            model.DoDecideAsync(
                Options(
                    state: [new DecisionTextPart("test")],
                    questions: new Dictionary<string, DecisionQuestion>
                    {
                        ["rating"] = DecisionQuestion.Score(null, levels),
                    }
                ),
                CancellationToken.None
            )
        );
        Assert.Empty(server.Calls);
    }

    [Fact]
    public async Task Accepts_null_optional_metadata_without_inventing_usage()
    {
        var (server, model) = Create();
        server.RespondJson(
            Url,
            """
            {"model":null,"usage":null,"answers":{"topic":{"type":"choice","choice":"a","probabilities":{"a":1},"confidence":null}}}
            """
        );

        var result = await model.DoDecideAsync(Options(), CancellationToken.None);

        Assert.Equal(new DecisionUsage(null, null), result.Usage);
        Assert.Equal("jev-latest", result.ResponseModelId);
        AssertJsonEqual(
            JsonNode.Parse("""{"typesafe":{"confidence":{},"legend":{}}}"""),
            JsonNode.Parse(result.ProviderMetadata!.Value.GetRawText())
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accepts_a_nullish_score_legend(bool explicitNull)
    {
        var (server, model) = Create();
        var response = NativeResponse.DeepClone();
        var severity = response["answers"]!["severity"]!.AsObject();
        if (explicitNull)
        {
            severity["legend"] = null;
        }
        else
        {
            severity.Remove("legend");
        }

        server.RespondJson(Url, response.ToJsonString());

        var result = await model.DoDecideAsync(Options(), CancellationToken.None);

        AssertJsonEqual(
            new JsonObject(),
            JsonNode.Parse(result.ProviderMetadata!.Value.GetRawText())!["typesafe"]!["legend"]
        );
    }

    [Fact]
    public async Task Preserves_structured_score_legends_in_provider_metadata_and_the_raw_response()
    {
        var (server, model) = Create();
        var legend = JsonNode.Parse(
            """
            {"0":{"meaning":"Cosmetic","examples":["Color mismatch"]},"1":["Workaround exists",{"impact":"Limited"}],"2":"Blocking"}
            """
        )!;
        var response = NativeResponse.DeepClone();
        response["answers"]!["severity"]!["legend"] = legend.DeepClone();
        server.RespondJson(Url, response.ToJsonString());

        var result = await model.DoDecideAsync(Options(), CancellationToken.None);

        AssertJsonEqual(
            new JsonObject { ["severity"] = legend.DeepClone() },
            JsonNode.Parse(result.ProviderMetadata!.Value.GetRawText())!["typesafe"]!["legend"]
        );
        AssertJsonEqual(response, JsonNode.Parse(result.ResponseBody!.Value.GetRawText()));
    }

    [Fact]
    public async Task Forwards_cancellation_to_fetch()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new CancellingHandler(cancellation);
        var model = new Provider(
            new TypeSafeAiProviderSettings { ApiKey = "test", HttpClient = new HttpClient(handler) }
        ).DecisionModel("jev-latest");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            model.DoDecideAsync(Options(), cancellation.Token)
        );

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Maps_text_and_JSON_parts_to_native_text_state()
    {
        var (server, model) = Create();

        await model.DoDecideAsync(
            Options(
                state:
                [
                    new DecisionTextPart("Inspect."),
                    new DecisionJsonPart(JsonDocument.Parse("[1,null]").RootElement),
                ]
            ),
            CancellationToken.None
        );

        Assert.Equal("Inspect.\n[1,null]", (string?)server.Calls[0].RequestBodyJson!["state"]);
    }

    [Theory]
    [InlineData("""{"nested":{"amount":49},"history":["refund"]}""")]
    [InlineData("[1,null]")]
    public async Task Preserves_a_single_JSON_state_part_as_native_JSON(string value)
    {
        var (server, model) = Create();

        await model.DoDecideAsync(
            Options(state: [new DecisionJsonPart(JsonDocument.Parse(value).RootElement)]),
            CancellationToken.None
        );

        AssertJsonEqual(JsonNode.Parse(value), server.Calls[0].RequestBodyJson!["state"]);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("42")]
    public async Task Serializes_a_primitive_JSON_state_part_as_text(string value)
    {
        var (server, model) = Create();

        await model.DoDecideAsync(
            Options(state: [new DecisionJsonPart(JsonDocument.Parse(value).RootElement)]),
            CancellationToken.None
        );

        Assert.Equal(value, (string?)server.Calls[0].RequestBodyJson!["state"]);
    }

    [Fact]
    public async Task Preserves_a_single_JSON_string_state_without_adding_quotes()
    {
        var (server, model) = Create();

        await model.DoDecideAsync(
            Options(state: [new DecisionJsonPart(JsonDocument.Parse("\"Customer requests a refund.\"").RootElement)]),
            CancellationToken.None
        );

        Assert.Equal("Customer requests a refund.", (string?)server.Calls[0].RequestBodyJson!["state"]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public async Task Supports_non_null_score_levels(int length)
    {
        var (server, model) = Create();
        var criteria = Enumerable.Range(0, length).Select(index => "Level " + index).ToList();
        var probabilities = new JsonObject();
        var legend = new JsonObject();
        for (var index = 0; index < length; index++)
        {
            probabilities[index.ToString()] = index == 0 ? 1 : 0;
            legend[index.ToString()] = criteria[index];
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
        server.RespondJson(Url, response.ToJsonString());

        var result = await model.DoDecideAsync(
            Options(
                questions: new Dictionary<string, DecisionQuestion>
                {
                    ["rating"] = DecisionQuestion.Score(null, criteria.Select(level => (DecisionInput?)level).ToList()),
                }
            ),
            CancellationToken.None
        );

        var sent = server.Calls[0].RequestBodyJson!["questions"]!["rating"]!;
        Assert.Equal("score", (string?)sent["type"]);
        AssertJsonEqual(JsonSerializer.SerializeToNode(criteria), sent["criteria"]);
        Assert.Equal("score", result.Answers["rating"].Type);
        Assert.Equal(0, result.Answers["rating"].Score);
        AssertJsonEqual(
            new JsonObject { ["rating"] = legend },
            JsonNode.Parse(result.ProviderMetadata!.Value.GetRawText())!["typesafe"]!["legend"]
        );
    }

    [Fact]
    public async Task Forwards_null_boolean_criteria()
    {
        var (server, model) = Create();
        var questions = new Dictionary<string, DecisionQuestion>(NativeQuestions)
        {
            ["requestsRefund"] = new DecisionQuestion
            {
                Type = "boolean",
                Instructions = "Refund?",
                NullCriteria = true,
            },
        };

        await model.DoDecideAsync(Options(questions: questions), CancellationToken.None);

        AssertJsonEqual(
            JsonNode.Parse("""{"type":"noul","instructions":"Refund?","criteria":null}"""),
            server.Calls[0].RequestBodyJson!["questions"]!["requestsRefund"]
        );
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{ instructions: undefined }")]
    [InlineData("{ instructions: null }")]
    public async Task Forwards_optional_box_instructions(string question)
    {
        var (server, model) = Create();
        server.RespondJson(
            Url,
            """
            {"model":"jev-1.13.0","answers":{"target":{"type":"box","box":{"top":0.25,"bottom":0.82,"left":0.44,"right":0.84},"confidence":null}}}
            """
        );

        var result = await model.DoDecideAsync(
            Options(
                questions: new Dictionary<string, DecisionQuestion>
                {
                    ["target"] = DecisionQuestion.Box(
                        question.Contains("null", StringComparison.Ordinal) ? DecisionInput.Null : null
                    ),
                }
            ),
            CancellationToken.None
        );

        AssertJsonEqual(
            question.Contains("null", StringComparison.Ordinal)
                ? JsonNode.Parse("""{"type":"box","instructions":null}""")
                : JsonNode.Parse("""{"type":"box"}"""),
            server.Calls[0].RequestBodyJson!["questions"]!["target"]
        );
        Assert.Equal("true", server.Calls[0].RequestHeaders["typesafe-danger-zone-images"]);
        Assert.Equal("box", result.Answers["target"].Type);
        Assert.Equal(new DecisionBox(0.25, 0.82, 0.44, 0.84), result.Answers["target"].Box);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sends_box_questions_and_enables_the_images_header(bool withImage)
    {
        var (server, model) = Create();
        var response = NativeResponse.DeepClone();
        response["answers"]!["target"] = JsonNode.Parse(
            """{"type":"box","box":{"top":0.25,"bottom":0.82,"left":0.44,"right":0.84},"confidence":0.92}"""
        );
        server.RespondJson(Url, response.ToJsonString());
        var questions = new Dictionary<string, DecisionQuestion>(NativeQuestions)
        {
            ["target"] = DecisionQuestion.Box("The mug"),
        };

        var result = await model.DoDecideAsync(
            Options(state: withImage ? [new DecisionFilePart("image/png", [1, 2, 3])] : null, questions: questions),
            CancellationToken.None
        );

        Assert.Contains("box", model.SupportedQuestionTypes);
        Assert.Equal("box", result.Answers["target"].Type);
        Assert.Equal(new DecisionBox(0.25, 0.82, 0.44, 0.84), result.Answers["target"].Box);
        Assert.Equal(
            0.92,
            (double?)JsonNode.Parse(result.ProviderMetadata!.Value.GetRawText())!["typesafe"]!["confidence"]!["target"]
        );
        Assert.Equal("true", server.Calls[0].RequestHeaders["typesafe-danger-zone-images"]);
        AssertJsonEqual(
            JsonNode.Parse("""{"type":"box","instructions":"The mug"}"""),
            RequestJson(server.Calls[0])["questions"]!["target"]
        );
    }

    [Fact]
    public async Task Warns_about_unsupported_maxOutputTokens_without_sending_it()
    {
        var (server, model) = Create();

        var result = await model.DoDecideAsync(Options(maxOutputTokens: 512), CancellationToken.None);

        Assert.Equal(["unsupported setting: maxOutputTokens"], result.Warnings);
        AssertJsonEqual(ExpectedRequest, server.Calls[0].RequestBodyJson);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("none")]
    [InlineData("provider-default")]
    [InlineData("high")]
    public async Task Warns_about_unsupported_reasoning_without_sending_it(string? reasoning)
    {
        var (server, model) = Create();

        var result = await model.DoDecideAsync(Options(reasoning: reasoning), CancellationToken.None);

        Assert.Equal(reasoning is null ? [] : new[] { "unsupported setting: reasoning" }, result.Warnings);
        AssertJsonEqual(ExpectedRequest, server.Calls[0].RequestBodyJson);
    }

    private sealed class CancellingHandler(CancellationTokenSource cancellation) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Calls++;
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("The token handed to the request was not cancelled.");
        }
    }
}
