// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Decision.Sdk.TypeSafeAi;

/// <summary>The AI SDK's <c>TypeSafeAiProviderSettings</c> (<c>typesafe-ai-provider.ts</c>).</summary>
public sealed class TypeSafeAiProviderSettings
{
    /// <summary>The base URL prefix for API calls. Defaults to the <c>TYPESAFE_AI_BASE_URL</c> environment variable, then <c>https://api.typesafe.ai/v1</c>.</summary>
    public string? BaseUrl { get; init; }

    /// <summary>The API key, sent as the bearer. Defaults to the <c>TYPESAFE_AI_API_KEY</c> environment variable, read at each call.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Headers added to every request.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>The client that sends the requests.</summary>
    public HttpClient? HttpClient { get; init; }
}

/// <summary>
/// The AI SDK's <c>createTypeSafeAi</c> and the <c>typeSafeAi</c> provider (<c>typesafe-ai-provider.ts</c>), for TypeSafe's experimental
/// decision capability: <c>typeSafeAi.decisionModel('jev-latest')</c>.
/// </summary>
public sealed class TypeSafeAiProvider
{
    private const string DefaultBaseUrl = "https://api.typesafe.ai/v1";

    private readonly TypeSafeAiProviderSettings _settings;
    private readonly string _baseUrl;

    /// <summary>Upstream's <c>createTypeSafeAi</c>.</summary>
    public TypeSafeAiProvider(TypeSafeAiProviderSettings? settings = null)
    {
        _settings = settings ?? new TypeSafeAiProviderSettings();
        _baseUrl = (
            _settings.BaseUrl ?? System.Environment.GetEnvironmentVariable("TYPESAFE_AI_BASE_URL") ?? DefaultBaseUrl
        ).TrimEnd('/');
    }

    /// <summary>Creates a decision model (upstream's <c>decisionModel</c>).</summary>
    public DecisionTypeSafeAiModel DecisionModel(string modelId) =>
        new(
            modelId,
            new TypeSafeAiDecisionModelConfig
            {
                Provider = "typesafe.decision",
                BaseUrl = _baseUrl,
                Headers = GetHeadersAsync,
                HttpClient = _settings.HttpClient,
            }
        );

    /// <summary>Deprecated: use <see cref="DecisionModel"/>.</summary>
    public DecisionTypeSafeAiModel EvaluationModel(string modelId) => DecisionModel(modelId);

    // The key is loaded when a call is made, not when the provider is created.
    private Task<IReadOnlyDictionary<string, string?>> GetHeadersAsync(CancellationToken cancellationToken)
    {
        var apiKey = !string.IsNullOrWhiteSpace(_settings.ApiKey)
            ? _settings.ApiKey
            : ModelHttp.Environment("TYPESAFE_AI_API_KEY")
                ?? throw new LoadApiKeyException(
                    "TypeSafe API key is missing. Pass it using the 'apiKey' parameter or the TYPESAFE_AI_API_KEY environment variable."
                );
        var headers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Authorization"] = "Bearer " + apiKey,
        };
        foreach (var (name, value) in _settings.Headers ?? new Dictionary<string, string>())
        {
            headers[name] = value;
        }

        headers["User-Agent"] = ModelHttp.RequestHeaders["User-Agent"];
        return Task.FromResult<IReadOnlyDictionary<string, string?>>(headers);
    }
}
