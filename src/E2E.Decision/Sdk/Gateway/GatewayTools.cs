// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk.Gateway;

/// <summary>Upstream's <c>gatewayTools</c> (<c>gateway-tools.ts</c>): Gateway-specific provider-defined tools.</summary>
public static class GatewayTools
{
    /// <summary>
    /// Fetch page content using Browserbase's lightweight Fetch API. Supports raw, Markdown, and schema-driven JSON output as well as
    /// redirects, proxy routing, and TLS controls.
    /// </summary>
    public static ProviderExecutedTool<BrowserbaseFetchInput, IBrowserbaseFetchOutput> BrowserbaseFetch(
        BrowserbaseFetchConfig? config = null
    ) => BrowserbaseFetchTool.BrowserbaseFetch(config);

    /// <summary>
    /// Search the web using Browserbase's Search API for fast, structured results. Returns titles, URLs, and available publication
    /// metadata without requiring a browser session.
    /// </summary>
    public static ProviderExecutedTool<BrowserbaseSearchInput, IBrowserbaseSearchOutput> BrowserbaseSearch(
        BrowserbaseSearchConfig? config = null
    ) => BrowserbaseSearchTool.BrowserbaseSearch(config);

    /// <summary>
    /// Fetch a page as Markdown through DeepSeek's standalone Fetch API. Executed by AI Gateway for any model, separate from native model
    /// web search.
    /// </summary>
    public static ProviderExecutedTool<DeepSeekFetchInput, IDeepSeekFetchOutput> DeepseekFetch(
        DeepSeekFetchConfig? config = null
    ) => DeepseekFetchTool.DeepseekFetch(config);

    /// <summary>
    /// Search the web through DeepSeek's standalone Search API. Executed by AI Gateway for any model, separate from native model web
    /// search.
    /// </summary>
    public static ProviderExecutedTool<DeepSeekSearchInput, IDeepSeekSearchOutput> DeepseekSearch(
        DeepSeekSearchConfig? config = null
    ) => DeepseekSearchTool.DeepseekSearch(config);

    /// <summary>
    /// Search the web using Exa for current information and token-efficient excerpts optimized for agent workflows. Supports search
    /// type, category, domain, date, location, and content extraction controls.
    /// </summary>
    public static ProviderExecutedTool<ExaSearchInput, IExaSearchOutput> ExaSearch(ExaSearchConfig? config = null) =>
        ExaSearchTool.ExaSearch(config);

    /// <summary>
    /// Search the web using Parallel AI's Search API for LLM-optimized excerpts. Takes a natural language objective and returns relevant
    /// excerpts, replacing multiple keyword searches with a single call for broad or complex queries. Supports different search types
    /// for depth vs breadth tradeoffs.
    /// </summary>
    public static ProviderExecutedTool<ParallelSearchInput, IParallelSearchOutput> ParallelSearch(
        ParallelSearchConfig? config = null
    ) => ParallelSearchTool.ParallelSearch(config);

    /// <summary>
    /// Search the web using Perplexity's Search API for real-time information, news, research papers, and articles. Provides ranked
    /// search results with advanced filtering options including domain, language, date range, and recency filters.
    /// </summary>
    public static ProviderExecutedTool<PerplexitySearchInput, IPerplexitySearchOutput> PerplexitySearch(
        PerplexitySearchConfig? config = null
    ) => PerplexitySearchTool.PerplexitySearch(config);

    /// <summary>
    /// Search the web and Tako's curated knowledge graph in one call for token-efficient web excerpts and structured data results
    /// grounded in premium sources, each with an embed-ready visualization. Supports effort, per-source web and data controls,
    /// localization, and inline contents for agents that need to reason over underlying data.
    /// </summary>
    public static ProviderExecutedTool<TakoSearchInput, ITakoSearchOutput> TakoSearch(
        TakoSearchConfig? config = null
    ) => TakoSearchTool.TakoSearch(config);
}

/// <summary>
/// .NET-only: the value of <c>gateway.tools</c> (<c>tools: typeof gatewayTools</c> upstream). C# cannot hold a static class in a
/// property, so <see cref="GatewayProvider.Tools"/> returns this object, which has the members of <see cref="GatewayTools"/>.
/// </summary>
public sealed class GatewayToolsApi
{
    internal GatewayToolsApi() { }

    /// <inheritdoc cref="GatewayTools.BrowserbaseFetch"/>
    public ProviderExecutedTool<BrowserbaseFetchInput, IBrowserbaseFetchOutput> BrowserbaseFetch(
        BrowserbaseFetchConfig? config = null
    ) => GatewayTools.BrowserbaseFetch(config);

    /// <inheritdoc cref="GatewayTools.BrowserbaseSearch"/>
    public ProviderExecutedTool<BrowserbaseSearchInput, IBrowserbaseSearchOutput> BrowserbaseSearch(
        BrowserbaseSearchConfig? config = null
    ) => GatewayTools.BrowserbaseSearch(config);

    /// <inheritdoc cref="GatewayTools.DeepseekFetch"/>
    public ProviderExecutedTool<DeepSeekFetchInput, IDeepSeekFetchOutput> DeepseekFetch(
        DeepSeekFetchConfig? config = null
    ) => GatewayTools.DeepseekFetch(config);

    /// <inheritdoc cref="GatewayTools.DeepseekSearch"/>
    public ProviderExecutedTool<DeepSeekSearchInput, IDeepSeekSearchOutput> DeepseekSearch(
        DeepSeekSearchConfig? config = null
    ) => GatewayTools.DeepseekSearch(config);

    /// <inheritdoc cref="GatewayTools.ExaSearch"/>
    public ProviderExecutedTool<ExaSearchInput, IExaSearchOutput> ExaSearch(ExaSearchConfig? config = null) =>
        GatewayTools.ExaSearch(config);

    /// <inheritdoc cref="GatewayTools.ParallelSearch"/>
    public ProviderExecutedTool<ParallelSearchInput, IParallelSearchOutput> ParallelSearch(
        ParallelSearchConfig? config = null
    ) => GatewayTools.ParallelSearch(config);

    /// <inheritdoc cref="GatewayTools.PerplexitySearch"/>
    public ProviderExecutedTool<PerplexitySearchInput, IPerplexitySearchOutput> PerplexitySearch(
        PerplexitySearchConfig? config = null
    ) => GatewayTools.PerplexitySearch(config);

    /// <inheritdoc cref="GatewayTools.TakoSearch"/>
    public ProviderExecutedTool<TakoSearchInput, ITakoSearchOutput> TakoSearch(TakoSearchConfig? config = null) =>
        GatewayTools.TakoSearch(config);
}
