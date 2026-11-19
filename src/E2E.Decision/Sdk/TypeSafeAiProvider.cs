// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>TypeSafeAiProviderSettings</c>.</summary>
public sealed class TypeSafeAiProviderSettings
{
    /// <summary>API key. Defaults to the <c>TYPESAFE_AI_API_KEY</c> environment variable.</summary>
    public string? ApiKey { get; init; }

    /// <summary>API base URL. Defaults to the <c>TYPESAFE_AI_BASE_URL</c> environment variable, then <c>https://api.typesafe.ai/v1</c>.</summary>
    public string? BaseUrl { get; init; }

    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>The client the requests go through. Defaults to a client with the model request timeout.</summary>
    public HttpClient? HttpClient { get; init; }
}

/// <summary>TypeSafe's decision capability (the AI SDK's <c>TypeSafeAiProvider</c>).</summary>
public sealed class TypeSafeAiProvider
{
    internal TypeSafeAiProvider(TypeSafeAiProviderSettings settings)
    {
        Settings = settings;
        BaseUrl = (
            string.IsNullOrWhiteSpace(settings.BaseUrl)
                ? ModelHttp.Environment("TYPESAFE_AI_BASE_URL")
                : settings.BaseUrl
        )?.TrimEnd('/') ?? "https://api.typesafe.ai/v1";
    }

    private TypeSafeAiProviderSettings Settings { get; }

    private string BaseUrl { get; }

    /// <summary>The AI SDK's <c>decisionModel</c>.</summary>
    public DecisionTypeSafeAiModel DecisionModel(string modelId) =>
        new(
            modelId,
            new TypeSafeAiDecisionModelConfig
            {
                Provider = "typesafe.decision",
                BaseUrl = BaseUrl,
                Headers = Headers,
                HttpClient = Settings.HttpClient,
            }
        );

    /// <summary>The AI SDK's deprecated <c>evaluationModel</c>: <see cref="DecisionModel"/> under its old name.</summary>
    [Obsolete("Use DecisionModel instead.")]
    public DecisionTypeSafeAiModel EvaluationModel(string modelId) => DecisionModel(modelId);

    // Resolved on each call, so the key is read from the environment when the model decides, not when it is made.
    private IReadOnlyDictionary<string, string> Headers()
    {
        var key = DecisionTypeSafeAiModel.LoadApiKey(Settings.ApiKey);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Authorization"] = "Bearer " + key,
        };
        if (Settings.Headers is not null)
        {
            foreach (var (name, value) in Settings.Headers)
            {
                headers[name] = value;
            }
        }

        headers["User-Agent"] = DecisionTypeSafeAiModel.UserAgent;
        return headers;
    }
}

/// <summary>The AI SDK's <c>createTypeSafeAi</c> and <c>typeSafeAi</c>.</summary>
public static class TypeSafeAi
{
    /// <summary>The provider <c>typeSafeAi</c>: the environment's key and base URL.</summary>
    public static TypeSafeAiProvider Default { get; } = Create();

    /// <summary>The AI SDK's <c>createTypeSafeAi</c>.</summary>
    public static TypeSafeAiProvider Create(TypeSafeAiProviderSettings? settings = null) =>
        new(settings ?? new TypeSafeAiProviderSettings());
}
