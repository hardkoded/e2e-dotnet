// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Decision.Sdk;

/// <summary>
/// The questions of a decision call as the AI SDK's <c>DecisionModelV4Question</c> serializes them (what <c>JSON.stringify</c> writes for a
/// question): the wire shape the Gateway takes as is, and TypeSafe takes with <c>boolean</c> renamed to <c>noul</c>.
/// </summary>
internal static class DecisionQuestionJson
{
    public static JsonObject ToJson(IReadOnlyDictionary<string, DecisionQuestion> questions)
    {
        var result = new JsonObject();
        foreach (var (id, question) in questions)
        {
            result[id] = ToJson(question);
        }

        return result;
    }

    public static JsonObject ToJson(DecisionQuestion question)
    {
        var json = new JsonObject { ["type"] = question.Type };
        if (question.Instructions is not null)
        {
            json["instructions"] = Node(question.Instructions);
        }

        switch (question.Type)
        {
            case "boolean":
                if (question.NullCriteria)
                {
                    json["criteria"] = null;
                }
                else if (question.TrueCriteria is not null || question.FalseCriteria is not null)
                {
                    var criteria = new JsonObject();
                    if (question.TrueCriteria is not null)
                    {
                        criteria["true"] = Node(question.TrueCriteria);
                    }

                    if (question.FalseCriteria is not null)
                    {
                        criteria["false"] = Node(question.FalseCriteria);
                    }

                    json["criteria"] = criteria;
                }

                break;
            case "choice":
                var options = new JsonObject();
                foreach (var choice in question.Choices ?? [])
                {
                    options[choice.Value] = Node(choice.Description);
                }

                json["criteria"] = options;
                break;
            case "score":
                var levels = new JsonArray();
                foreach (var level in question.Levels ?? [])
                {
                    levels.Add(Node(level));
                }

                json["criteria"] = levels;
                break;
        }

        return json;
    }

    private static JsonNode? Node(DecisionInput? input) =>
        input is null ? null
        : input.Text is { } text ? JsonValue.Create(text)
        : JsonNode.Parse(input.Json!.Value.GetRawText());
}
