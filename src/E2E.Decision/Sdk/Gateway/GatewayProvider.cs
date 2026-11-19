// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>The AI SDK's <c>GatewayProviderSettings</c> (<c>gateway-provider.ts</c>) the decision models read.</summary>
public sealed class GatewayProviderSettings
{
    /// <summary>The base URL prefix for API calls. Defaults to <c>https://ai-gateway.vercel.sh/v4/ai</c>.</summary>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// The API key or Vercel access token sent as the <c>Authorization</c> bearer. Defaults to the <c>AI_GATEWAY_API_KEY</c> environment variable.
    /// With neither set, the provider authenticates with the Vercel OIDC token of the <c>VERCEL_OIDC_TOKEN</c> environment variable.
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>Headers added to every request.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>The Vercel team the requests are attributed to.</summary>
    public string? TeamIdOrSlug { get; init; }

    /// <summary>The client that sends the requests.</summary>
    public HttpClient? HttpClient { get; init; }
}

/// <summary>The token and method <see cref="GatewayProvider.GetGatewayAuthTokenAsync"/> gives.</summary>
public sealed record GatewayAuthToken(string Token, string AuthMethod);

/// <summary>
/// The AI SDK's <c>createGateway</c> and the <c>gateway</c> provider (<c>gateway-provider.ts</c>), for its decision models:
/// <c>gateway.decisionModel('typesafe-ai/jev')</c>.
/// </summary>
public sealed class GatewayProvider : IDecisionProvider
{
    private const string ProtocolVersion = "0.0.1";
    private const string AuthMethodHeader = "ai-gateway-auth-method";
    private const string TeamHeader = "x-vercel-ai-gateway-team";
    private const string DefaultBaseUrl = "https://ai-gateway.vercel.sh/v4/ai";

    private readonly GatewayProviderSettings _settings;
    private readonly string _baseUrl;

    /// <summary>Upstream's <c>createGateway</c>.</summary>
    public GatewayProvider(GatewayProviderSettings? settings = null)
    {
        _settings = settings ?? new GatewayProviderSettings();
        _baseUrl = (_settings.BaseUrl ?? DefaultBaseUrl).TrimEnd('/');
    }

    /// <summary>Creates a decision model (upstream's <c>decisionModel</c>).</summary>
    public GatewayDecisionModel DecisionModel(string modelId) =>
        new(
            modelId,
            new GatewayDecisionModelConfig
            {
                Provider = "gateway",
                BaseUrl = _baseUrl,
                Headers = GetHeadersAsync,
                O11yHeaders = GetO11yHeadersAsync,
                HttpClient = _settings.HttpClient,
            }
        );

    /// <summary>Creates a decision model (upstream's <c>decision</c>).</summary>
    public GatewayDecisionModel Decision(string modelId) => DecisionModel(modelId);

    /// <summary>Deprecated: use <see cref="DecisionModel"/>.</summary>
    public GatewayDecisionModel EvaluationModel(string modelId) => DecisionModel(modelId);

    /// <summary>Deprecated: use <see cref="Decision"/>.</summary>
    public GatewayDecisionModel Evaluation(string modelId) => DecisionModel(modelId);

    IDecisionModel? IDecisionProvider.DecisionModel(string modelId) => DecisionModel(modelId);

    IEvaluationModel? IDecisionProvider.EvaluationModel(string modelId) => EvaluationModel(modelId);

    /// <summary>
    /// Upstream's <c>getGatewayAuthToken</c>: a non-empty API key (the option, else <c>AI_GATEWAY_API_KEY</c>) is used as is, with no trimming;
    /// otherwise the Vercel OIDC token. Callers wrap a failure here in a <see cref="GatewayAuthenticationException"/> ("No authentication provided").
    /// </summary>
    public static Task<GatewayAuthToken> GetGatewayAuthTokenAsync(
        GatewayProviderSettings settings,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var apiKey = settings.ApiKey ?? System.Environment.GetEnvironmentVariable("AI_GATEWAY_API_KEY");
        if (!string.IsNullOrEmpty(apiKey))
        {
            return Task.FromResult(new GatewayAuthToken(apiKey, "api-key"));
        }

        return Task.FromResult(new GatewayAuthToken(GetVercelOidcToken(), "oidc"));
    }

    // vercel-environment.ts getVercelOidcToken (@vercel/oidc): the token of the VERCEL_OIDC_TOKEN environment variable.
    private static string GetVercelOidcToken()
    {
        var token = System.Environment.GetEnvironmentVariable("VERCEL_OIDC_TOKEN");
        return !string.IsNullOrEmpty(token)
            ? token
            : throw new InvalidOperationException(
                "Could not get the Vercel OIDC token: the 'VERCEL_OIDC_TOKEN' environment variable is not set."
            );
    }

    // getHeaders: a failure to authenticate is wrapped in a GatewayAuthenticationException with the failure as its cause.
    private async Task<IReadOnlyDictionary<string, string?>> GetHeadersAsync(CancellationToken cancellationToken)
    {
        GatewayAuthToken auth;
        try
        {
            auth = await GetGatewayAuthTokenAsync(_settings, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            throw GatewayAuthenticationException.CreateContextualError(false, false, 401, error);
        }

        var headers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Authorization"] = "Bearer " + auth.Token,
            ["ai-gateway-protocol-version"] = ProtocolVersion,
            [AuthMethodHeader] = auth.AuthMethod,
        };
        if (_settings.TeamIdOrSlug is not null)
        {
            headers[TeamHeader] = _settings.TeamIdOrSlug;
        }

        foreach (var (name, value) in _settings.Headers ?? new Dictionary<string, string>())
        {
            headers[name] = value;
        }

        // withUserAgentSuffix: the port's agent follows a User-Agent the caller set.
        var agent = ModelHttp.RequestHeaders["User-Agent"];
        headers["User-Agent"] =
            headers.TryGetValue("User-Agent", out var userAgent) && !string.IsNullOrEmpty(userAgent)
                ? userAgent + " " + agent
                : agent;
        return headers;
    }

    // createO11yHeaders: the Vercel deployment the process runs in, when it runs in one.
    private static Task<IReadOnlyDictionary<string, string?>> GetO11yHeadersAsync(CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (
            var (header, variable) in new[]
            {
                ("ai-o11y-deployment-id", "VERCEL_DEPLOYMENT_ID"),
                ("ai-o11y-environment", "VERCEL_ENV"),
                ("ai-o11y-region", "VERCEL_REGION"),
                ("ai-o11y-project-id", "VERCEL_PROJECT_ID"),
            }
        )
        {
            if (ModelHttp.Environment(variable) is { Length: > 0 } value)
            {
                headers[header] = value;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, string?>>(headers);
    }
}
