// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json;
using E2E.Decision.Sdk;

namespace E2E.Decision.Tests.DecisionModels;

/// <summary>
/// .NET-only: the OpenAI Decisions API provider (<c>OpenAIDecisionModel</c>) checked directly: the wire shape of a request over a boolean
/// question, the screenshot part, an unanswered question, and a missing key. Upstream's <c>openai.test.ts</c> covers the provider through the
/// decision executor, and is ported in <c>Openai/</c>.
/// </summary>
public sealed class OpenAIDecisionModelTests
{
    private static readonly DecisionModelCall Call = new()
    {
        State = [new DecisionJsonPart(JsonDocument.Parse("""{"goal":"Do the thing"}""").RootElement)],
        Questions = new Dictionary<string, DecisionQuestion>
        {
            ["operation"] = DecisionQuestion.Choice(
                "Pick one",
                [new DecisionChoice("tap", "Tap a control"), new DecisionChoice("done")]
            ),
            ["verdict"] = DecisionQuestion.Boolean("Does it hold?", trueCriteria: "It holds."),
        },
    };

    [Fact]
    public async Task Sends_one_decisions_request_in_the_wire_shape_and_reads_the_answers()
    {
        var handler = new ScriptedHandler(
            """
            {"model":"gpt-6-luna-2026-09-01","usage":{"input_tokens":321,"output_tokens":0},"answers":[
              {"type":"choice","name":"operation","choice":"tap","confidence":0.93,"probabilities":[{"value":"tap","probability":0.95},{"value":"done","probability":0.05}]},
              {"type":"predicate","name":"verdict","probability":0.8}]}
            """
        );
        using var model = Model(handler);

        var result = await model.DoDecideAsync(Call, CancellationToken.None);

        var request = JsonDocument.Parse(handler.Body!).RootElement;
        Assert.Equal("https://api.test/v1/decisions", handler.Url);
        Assert.Equal("Bearer sk-test", handler.Authorization);
        Assert.Equal("gpt-6-luna", request.GetProperty("model").GetString());
        var content = request.GetProperty("input")[0].GetProperty("content")[0];
        Assert.Equal("input_json", content.GetProperty("type").GetString());
        Assert.Equal("Do the thing", content.GetProperty("data").GetProperty("goal").GetString());
        var questions = request.GetProperty("questions");
        Assert.Equal("choice", questions[0].GetProperty("type").GetString());
        Assert.Equal("Tap a control", questions[0].GetProperty("choices")[0].GetProperty("description").GetString());
        Assert.Equal("predicate", questions[1].GetProperty("type").GetString());
        Assert.Equal(
            "Does it hold?\n\nCriteria for true:\nIt holds.",
            questions[1].GetProperty("instructions").GetString()
        );

        Assert.Equal("tap", result.Answers["operation"].Choice);
        Assert.Equal(0.95, result.Answers["operation"].Probabilities!["tap"]);
        Assert.Equal(0.8, result.Answers["verdict"].Probability);
        Assert.Equal(321, result.Usage!.InputTokens);
        Assert.Equal("gpt-6-luna-2026-09-01", result.ResponseModelId);
        Assert.Equal(
            0.93,
            result
                .ProviderMetadata!.Value.GetProperty("openai")
                .GetProperty("confidence")
                .GetProperty("operation")
                .GetDouble()
        );
    }

    [Fact]
    public async Task Sends_a_screenshot_as_an_input_image_beside_the_json_state()
    {
        var handler = new ScriptedHandler(
            """{"answers":[{"type":"refusal","name":"operation"},{"type":"refusal","name":"verdict"}]}"""
        );
        using var model = Model(handler);
        var call = new DecisionModelCall
        {
            State = [.. Call.State, new DecisionFilePart("image/png", [1, 2, 3])],
            Questions = Call.Questions,
        };

        await model.DoDecideAsync(call, CancellationToken.None);

        var content = JsonDocument.Parse(handler.Body!).RootElement.GetProperty("input")[0].GetProperty("content");
        Assert.Equal(
            ["input_json", "input_image"],
            content.EnumerateArray().Select(part => part.GetProperty("type").GetString())
        );
        Assert.Equal("data:image/png;base64,AQID", content[1].GetProperty("image_url").GetString());
    }

    [Fact]
    public async Task Fails_a_question_the_response_leaves_unanswered()
    {
        using var model = Model(
            new ScriptedHandler("""{"answers":[{"type":"predicate","name":"verdict","probability":1}]}""")
        );

        await Assert.ThrowsAsync<InvalidResponseDataException>(() => model.DoDecideAsync(Call, CancellationToken.None));
    }

    [Fact]
    public async Task Names_the_environment_variable_when_there_is_no_api_key()
    {
        using var model = new OpenAIDecisionModel(
            "gpt-6-luna",
            new OpenAIDecisionModelOptions { ApiKeyEnv = "E2E_DECISION_TEST_UNSET_KEY" },
            new HttpClient(new ScriptedHandler("{}"))
        );

        var error = await Assert.ThrowsAsync<LoadApiKeyException>(() =>
            model.DoDecideAsync(Call, CancellationToken.None)
        );

        Assert.Contains(
            "the E2E_DECISION_TEST_UNSET_KEY environment variable",
            error.Message,
            StringComparison.Ordinal
        );
    }

    private static OpenAIDecisionModel Model(ScriptedHandler handler) =>
        new(
            "gpt-6-luna",
            new OpenAIDecisionModelOptions { ApiKey = "sk-test", BaseUrl = "https://api.test/v1" },
            new HttpClient(handler)
        );

    private sealed class ScriptedHandler(string response) : HttpMessageHandler
    {
        public string? Url { get; private set; }

        public string? Body { get; private set; }

        public string? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Url = request.RequestUri!.ToString();
            Authorization = request.Headers.Authorization?.ToString();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
            };
        }
    }
}
