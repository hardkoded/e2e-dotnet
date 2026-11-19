// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Decision.Sdk;

namespace E2E.Decision.Tests.Openai;

/// <summary>
/// Upstream's <c>openaiDecision</c> and its wire helpers (<c>tests/openai.test.ts</c>): the real OpenAI decision model over a scripted
/// <c>/decisions</c> endpoint. The resolver sees each request body and returns the wire answers.
/// </summary>
internal static class OpenAiDecision
{
    /// <summary>The wire answers a resolver returns for a request body and the number of the call from 0.</summary>
    public delegate JsonArray Resolve(JsonElement request, int call);

    public static (OpenAIDecisionModel Model, List<JsonElement> Requests) Create(Resolve resolve)
    {
        var requests = new List<JsonElement>();
        var model = new OpenAIDecisionModel(
            "gpt-6-luna",
            new OpenAIDecisionModelOptions { ApiKey = "sk-test", BaseUrl = "https://api.test/v1" },
            new HttpClient(new Endpoint(resolve, requests))
        );
        return (model, requests);
    }

    /// <summary>Upstream's <c>unanimous</c>: a unanimous distribution for the choice over the request's options.</summary>
    public static JsonObject Unanimous(JsonElement question, string choice, double? confidence = null)
    {
        var probabilities = new JsonArray();
        foreach (var option in Choices(question))
        {
            probabilities.Add(
                new JsonObject
                {
                    ["value"] = option.GetProperty("value").GetString(),
                    ["probability"] = option.GetProperty("value").GetString() == choice ? 1 : 0,
                }
            );
        }

        var answer = new JsonObject
        {
            ["type"] = "choice",
            ["name"] = question.GetProperty("name").GetString(),
            ["choice"] = choice,
            ["probabilities"] = probabilities,
        };
        if (confidence is { } reported)
        {
            answer["confidence"] = reported;
        }

        return answer;
    }

    public static IEnumerable<JsonElement> Choices(JsonElement question) =>
        question.TryGetProperty("choices", out var choices) ? choices.EnumerateArray() : [];

    public static string FirstChoice(JsonElement question) =>
        Choices(question).Select(option => option.GetProperty("value").GetString()!).FirstOrDefault() ?? "";

    private sealed class Endpoint(Resolve resolve, List<JsonElement> requests) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var body = document.RootElement.Clone();
            var answers = resolve(body, requests.Count);
            requests.Add(body);
            var response = new JsonObject
            {
                ["model"] = "gpt-6-luna-2026-09-01",
                ["usage"] = new JsonObject { ["input_tokens"] = 321, ["output_tokens"] = 0 },
                ["answers"] = answers,
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response.ToJsonString(), Encoding.UTF8, "application/json"),
            };
        }
    }
}
