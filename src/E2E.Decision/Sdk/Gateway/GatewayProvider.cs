// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>The AI SDK's <c>GatewayProviderSettings</c> (<c>gateway-provider.ts</c>) the decision models read.</summary>
public sealed class GatewayProviderSettings
{
    /// <summary>Settings for downloading JSON Lines batch results (upstream's <c>batchResultDownloads</c>).</summary>
    public GatewayBatchResultDownloads? BatchResultDownloads { get; init; }

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

    /// <summary>How frequently to refresh the metadata cache in milliseconds. Defaults to 5 minutes.</summary>
    public double? MetadataCacheRefreshMillis { get; init; }

    /// <summary>Upstream's <c>_internal.currentDate</c>, for testing purposes only.</summary>
    internal Func<DateTimeOffset>? CurrentDate { get; init; }
}

/// <summary>Upstream's <c>GatewayProviderSettings.batchResultDownloads</c>.</summary>
public sealed class GatewayBatchResultDownloads
{
    /// <summary>Maximum UTF-8 bytes per row, excluding LF. Defaults to 64 MiB.</summary>
    public int? MaxLineBytes { get; init; }
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
    private const string DefaultBaseUrl = "https://ai-gateway.vercel.sh/v4/ai";

    private readonly GatewayProviderSettings _settings;
    private readonly string _baseUrl;
    private readonly Lock _metadataLock = new();
    private Task<GatewayFetchMetadataResponse>? _pendingMetadata;
    private GatewayFetchMetadataResponse? _metadataCache;
    private DateTimeOffset _lastFetchTime = DateTimeOffset.MinValue;

    /// <summary>Upstream's <c>createGateway</c>.</summary>
    public GatewayProvider(GatewayProviderSettings? settings = null)
    {
        _settings = settings ?? new GatewayProviderSettings();
        _baseUrl = (_settings.BaseUrl ?? DefaultBaseUrl).TrimEnd('/');
    }

    /// <summary>
    /// Creates a language model (upstream's <c>languageModel</c>, <c>createLanguageModel</c>). It is also what calling the upstream provider
    /// as a function does: <c>gateway('anthropic/claude-sonnet-5')</c>.
    /// </summary>
    public GatewayLanguageModel LanguageModel(string modelId) =>
        new(
            modelId,
            new GatewayChatConfig
            {
                Provider = "gateway",
                BaseUrl = _baseUrl,
                Headers = GetHeadersAsync,
                O11yHeaders = GetO11yHeadersAsync,
                HttpClient = _settings.HttpClient,
            }
        );

    /// <summary>Creates a language model (upstream's <c>chat</c>, an alias of <c>languageModel</c>).</summary>
    public GatewayLanguageModel Chat(string modelId) => LanguageModel(modelId);

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

    /// <summary>Returns a batch service for processing batches with AI Gateway (upstream's <c>experimental_batch</c>).</summary>
    public GatewayBatch ExperimentalBatch() =>
        new(
            new GatewayBatchConfig
            {
                Provider = "gateway",
                MaxLineBytes = _settings.BatchResultDownloads?.MaxLineBytes,
                BaseUrl = _baseUrl,
                Headers = GetHeadersAsync,
                O11yHeaders = GetO11yHeadersAsync,
                HttpClient = _settings.HttpClient,
            }
        );

    /// <summary>Gateway-specific tools executed server-side (upstream's <c>tools</c>).</summary>
    public GatewayToolsApi Tools { get; } = new();

    /// <summary>The default provider (upstream's <c>export const gateway = createGateway()</c>).</summary>
    public static GatewayProvider Gateway { get; } = new();

    /// <summary>Returns available providers and models for use with the remote provider (upstream's <c>getAvailableModels</c>).</summary>
    public Task<GatewayFetchMetadataResponse> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        Task<GatewayFetchMetadataResponse> pending;
        GatewayFetchMetadataResponse? cache;
        lock (_metadataLock)
        {
            var now = _settings.CurrentDate?.Invoke() ?? DateTimeOffset.UtcNow;
            var refresh = TimeSpan.FromMilliseconds(_settings.MetadataCacheRefreshMillis ?? 1000 * 60 * 5);
            if (_pendingMetadata is null || now - _lastFetchTime > refresh)
            {
                _lastFetchTime = now;
                _pendingMetadata = FetchAvailableModelsAsync();
            }

            pending = _pendingMetadata;
            cache = _metadataCache;
        }

        return cache is not null ? Task.FromResult(cache) : pending.WaitAsync(cancellationToken);
    }

    /// <summary>Returns credit information for the authenticated user (upstream's <c>getCredits</c>).</summary>
    public Task<GatewayCreditsResponse> GetCreditsAsync(CancellationToken cancellationToken = default) =>
        WithAuthMethodAsync(
            (config, token) => new GatewayFetchMetadata(config).GetCreditsAsync(token),
            cancellationToken
        );

    /// <summary>
    /// Returns a spend report with cost, token, and request count data, aggregated by the specified dimension (upstream's
    /// <c>getSpendReport</c>).
    /// </summary>
    public Task<GatewaySpendReportResponse> GetSpendReportAsync(
        GatewaySpendReportParams parameters,
        CancellationToken cancellationToken = default
    ) =>
        WithAuthMethodAsync(
            (config, token) => new GatewaySpendReport(config).GetSpendReportAsync(parameters, token),
            cancellationToken
        );

    /// <summary>
    /// Returns detailed information about a specific generation by its ID, including cost, token usage, latency, and provider details
    /// (upstream's <c>getGenerationInfo</c>).
    /// </summary>
    public Task<GatewayGenerationInfo> GetGenerationInfoAsync(
        GatewayGenerationInfoParams parameters,
        CancellationToken cancellationToken = default
    ) =>
        WithAuthMethodAsync(
            (config, token) => new GatewayGenerationInfoFetcher(config).GetGenerationInfoAsync(parameters, token),
            cancellationToken
        );

    /// <summary>Creates a decision model (upstream's <c>decision</c>).</summary>
    public GatewayDecisionModel Decision(string modelId) => DecisionModel(modelId);

    /// <summary>Creates a model for extracting typed field values from shared state (upstream's <c>extractionModel</c>).</summary>
    public GatewayExtractionModel ExtractionModel(string modelId) =>
        new(
            modelId,
            new GatewayExtractionModelConfig
            {
                Provider = "gateway",
                BaseUrl = _baseUrl,
                Headers = GetHeadersAsync,
                O11yHeaders = GetO11yHeadersAsync,
                HttpClient = _settings.HttpClient,
            }
        );

    /// <summary>Creates a model for extracting typed field values from shared state (upstream's <c>extraction</c>).</summary>
    public GatewayExtractionModel Extraction(string modelId) => ExtractionModel(modelId);

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
    public static async Task<GatewayAuthToken> GetGatewayAuthTokenAsync(
        GatewayProviderSettings settings,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var apiKey = settings.ApiKey ?? System.Environment.GetEnvironmentVariable("AI_GATEWAY_API_KEY");
        if (!string.IsNullOrEmpty(apiKey))
        {
            return new GatewayAuthToken(apiKey, "api-key");
        }

        // getVercelOidcToken (@vercel/oidc), through vercel-environment.ts.
        var oidcToken = await VercelEnvironment.GetVercelOidcTokenAsync(cancellationToken).ConfigureAwait(false);
        return new GatewayAuthToken(oidcToken, "oidc");
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
            [GatewayHeaders.GatewayAuthMethodHeader] = auth.AuthMethod,
        };
        if (_settings.TeamIdOrSlug is not null)
        {
            headers[GatewayHeaders.VercelAiGatewayTeamHeader] = _settings.TeamIdOrSlug;
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
                ("ai-o11y-request-id", null),
                ("ai-o11y-project-id", "VERCEL_PROJECT_ID"),
            }
        )
        {
            var value = variable is null ? VercelEnvironment.GetVercelRequestId() : ModelHttp.Environment(variable);
            if (value is { Length: > 0 })
            {
                headers[header] = value;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, string?>>(headers);
    }

    // getAvailableModels: the fetch is shared by every caller until the cache is refreshed, so it takes no caller's token.
    private async Task<GatewayFetchMetadataResponse> FetchAvailableModelsAsync()
    {
        var metadata = await WithAuthMethodAsync(
                (config, token) => new GatewayFetchMetadata(config).GetAvailableModelsAsync(token),
                CancellationToken.None
            )
            .ConfigureAwait(false);
        lock (_metadataLock)
        {
            _metadataCache = metadata;
        }

        return metadata;
    }

    // The getCredits / getSpendReport / getGenerationInfo wrapper: a failure becomes a Gateway error that names the auth method the
    // request used.
    private async Task<T> WithAuthMethodAsync<T>(
        Func<GatewayConfig, CancellationToken, Task<T>> call,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyDictionary<string, string?>? resolvedHeaders = null;
        var config = new GatewayConfig
        {
            BaseUrl = _baseUrl,
            HttpClient = _settings.HttpClient,
            Headers = async token =>
            {
                var headers = await GetHeadersAsync(token).ConfigureAwait(false);
                resolvedHeaders = headers;
                return headers;
            },
        };
        try
        {
            return await call(config, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            throw GatewayErrors.AsGatewayError(error, GatewayErrors.ParseAuthMethod(resolvedHeaders));
        }
    }
}
