// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>The <c>error</c> values of a <see cref="PerplexitySearchError"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PerplexitySearchErrorCode>))]
public enum PerplexitySearchErrorCode
{
    /// <summary><c>api_error</c>.</summary>
    [JsonStringEnumMemberName("api_error")]
    ApiError,

    /// <summary><c>rate_limit</c>.</summary>
    [JsonStringEnumMemberName("rate_limit")]
    RateLimit,

    /// <summary><c>timeout</c>.</summary>
    [JsonStringEnumMemberName("timeout")]
    Timeout,

    /// <summary><c>invalid_input</c>.</summary>
    [JsonStringEnumMemberName("invalid_input")]
    InvalidInput,

    /// <summary><c>unknown</c>.</summary>
    [JsonStringEnumMemberName("unknown")]
    Unknown,
}

/// <summary>The <c>'day' | 'week' | 'month' | 'year'</c> recency filter of the Perplexity Search tool.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PerplexitySearchRecencyFilter>))]
public enum PerplexitySearchRecencyFilter
{
    /// <summary><c>day</c>.</summary>
    [JsonStringEnumMemberName("day")]
    Day,

    /// <summary><c>week</c>.</summary>
    [JsonStringEnumMemberName("week")]
    Week,

    /// <summary><c>month</c>.</summary>
    [JsonStringEnumMemberName("month")]
    Month,

    /// <summary><c>year</c>.</summary>
    [JsonStringEnumMemberName("year")]
    Year,
}

/// <summary>Upstream's <c>PerplexitySearchConfig</c>.</summary>
public sealed class PerplexitySearchConfig
{
    /// <summary>Default maximum number of search results to return (1-20, default: 10).</summary>
    public double? MaxResults { get; init; }

    /// <summary>Default maximum tokens to extract per search result page (256-2048, default: 2048).</summary>
    public double? MaxTokensPerPage { get; init; }

    /// <summary>Default maximum total tokens across all search results (default: 25000, max: 1000000).</summary>
    public double? MaxTokens { get; init; }

    /// <summary>Default two-letter ISO 3166-1 alpha-2 country code for regional search results. Examples: <c>US</c>, <c>GB</c>, <c>FR</c>.</summary>
    public string? Country { get; init; }

    /// <summary>Default list of domains to include or exclude from search results (max 20). To exclude: <c>['-example.com']</c>.</summary>
    public List<string>? SearchDomainFilter { get; init; }

    /// <summary>Default list of ISO 639-1 language codes to filter results (max 10, lowercase).</summary>
    public List<string>? SearchLanguageFilter { get; init; }

    /// <summary>Default recency filter for results. Cannot be combined with after/before dates at runtime.</summary>
    public PerplexitySearchRecencyFilter? SearchRecencyFilter { get; init; }
}

/// <summary>Upstream's <c>PerplexitySearchResult</c>.</summary>
public sealed class PerplexitySearchResult
{
    /// <summary>Title of the search result.</summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>URL of the search result.</summary>
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    /// <summary>Text snippet/preview of the content.</summary>
    [JsonPropertyName("snippet")]
    public required string Snippet { get; init; }

    /// <summary>Publication date of the content.</summary>
    [JsonPropertyName("date")]
    public string? Date { get; init; }

    /// <summary>Last updated date of the content.</summary>
    [JsonPropertyName("lastUpdated")]
    public string? LastUpdated { get; init; }
}

/// <summary>Upstream's <c>PerplexitySearchOutput</c>: a <see cref="PerplexitySearchResponse"/> or a <see cref="PerplexitySearchError"/>.</summary>
public interface IPerplexitySearchOutput { }

/// <summary>Upstream's <c>PerplexitySearchResponse</c>.</summary>
public sealed class PerplexitySearchResponse : IPerplexitySearchOutput
{
    /// <summary>Array of search results.</summary>
    [JsonPropertyName("results")]
    public required List<PerplexitySearchResult> Results { get; init; }

    /// <summary>Unique identifier for this search request.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }
}

/// <summary>Upstream's <c>PerplexitySearchError</c>.</summary>
public sealed class PerplexitySearchError : IPerplexitySearchOutput
{
    /// <summary>Error type.</summary>
    [JsonPropertyName("error")]
    public required PerplexitySearchErrorCode Error { get; init; }

    /// <summary>HTTP status code if applicable.</summary>
    [JsonPropertyName("statusCode")]
    public double? StatusCode { get; init; }

    /// <summary>Human-readable error message.</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

/// <summary>Upstream's <c>PerplexitySearchInput</c>.</summary>
public sealed class PerplexitySearchInput
{
    /// <summary>
    /// Search query (string) or multiple queries (array of up to 5 strings). Multi-query searches return combined results from all
    /// queries.
    /// </summary>
    [JsonPropertyName("query")]
    public required StringOrStrings Query { get; init; }

    /// <summary>Maximum number of search results to return (1-20, default: 10).</summary>
    [JsonPropertyName("max_results")]
    public double? MaxResults { get; init; }

    /// <summary>Maximum number of tokens to extract per search result page (256-2048, default: 2048).</summary>
    [JsonPropertyName("max_tokens_per_page")]
    public double? MaxTokensPerPage { get; init; }

    /// <summary>Maximum total tokens across all search results (default: 25000, max: 1000000).</summary>
    [JsonPropertyName("max_tokens")]
    public double? MaxTokens { get; init; }

    /// <summary>Two-letter ISO 3166-1 alpha-2 country code for regional search results.</summary>
    [JsonPropertyName("country")]
    public string? Country { get; init; }

    /// <summary>List of domains to include or exclude from search results (max 20).</summary>
    [JsonPropertyName("search_domain_filter")]
    public List<string>? SearchDomainFilter { get; init; }

    /// <summary>List of ISO 639-1 language codes to filter results (max 10, lowercase).</summary>
    [JsonPropertyName("search_language_filter")]
    public List<string>? SearchLanguageFilter { get; init; }

    /// <summary>Include only results published after this date. Format: <c>MM/DD/YYYY</c>. Cannot be used with the recency filter.</summary>
    [JsonPropertyName("search_after_date")]
    public string? SearchAfterDate { get; init; }

    /// <summary>Include only results published before this date. Format: <c>MM/DD/YYYY</c>. Cannot be used with the recency filter.</summary>
    [JsonPropertyName("search_before_date")]
    public string? SearchBeforeDate { get; init; }

    /// <summary>Include only results last updated after this date. Format: <c>MM/DD/YYYY</c>. Cannot be used with the recency filter.</summary>
    [JsonPropertyName("last_updated_after_filter")]
    public string? LastUpdatedAfterFilter { get; init; }

    /// <summary>Include only results last updated before this date. Format: <c>MM/DD/YYYY</c>. Cannot be used with the recency filter.</summary>
    [JsonPropertyName("last_updated_before_filter")]
    public string? LastUpdatedBeforeFilter { get; init; }

    /// <summary>Filter results by relative time period. Cannot be used with the after/before dates.</summary>
    [JsonPropertyName("search_recency_filter")]
    public PerplexitySearchRecencyFilter? SearchRecencyFilter { get; init; }
}

/// <summary>
/// The tool of <c>tool/perplexity-search.ts</c>. The class name is .NET-only: C# has no module-level functions, so
/// <c>perplexitySearch</c> and <c>perplexitySearchToolFactory</c> are members of it.
/// </summary>
public static class PerplexitySearchTool
{
    private static readonly LazySchema s_inputSchema = SchemaUtils.LazySchema(() =>
        SchemaUtils.ZodSchema(
            Zod.Object(
                (
                    "query",
                    Zod.Describe(
                        Zod.Union(Zod.String(), Zod.Array(Zod.String())),
                        "Search query (string) or multiple queries (array of up to 5 strings). Multi-query searches return combined results from all queries."
                    )
                ),
                (
                    "max_results",
                    Zod.Describe(
                        Zod.Optional(Zod.Number()),
                        "Maximum number of search results to return (1-20, default: 10)"
                    )
                ),
                (
                    "max_tokens_per_page",
                    Zod.Describe(
                        Zod.Optional(Zod.Number()),
                        "Maximum number of tokens to extract per search result page (256-2048, default: 2048)"
                    )
                ),
                (
                    "max_tokens",
                    Zod.Describe(
                        Zod.Optional(Zod.Number()),
                        "Maximum total tokens across all search results (default: 25000, max: 1000000)"
                    )
                ),
                (
                    "country",
                    Zod.Describe(
                        Zod.Optional(Zod.String()),
                        "Two-letter ISO 3166-1 alpha-2 country code for regional search results (e.g., 'US', 'GB', 'FR')"
                    )
                ),
                (
                    "search_domain_filter",
                    Zod.Describe(
                        Zod.Optional(Zod.Array(Zod.String())),
                        "List of domains to include or exclude from search results (max 20). To include: ['nature.com', 'science.org']. To exclude: ['-example.com', '-spam.net']"
                    )
                ),
                (
                    "search_language_filter",
                    Zod.Describe(
                        Zod.Optional(Zod.Array(Zod.String())),
                        "List of ISO 639-1 language codes to filter results (max 10, lowercase). Examples: ['en', 'fr', 'de']"
                    )
                ),
                (
                    "search_after_date",
                    Zod.Describe(
                        Zod.Optional(Zod.String()),
                        "Include only results published after this date. Format: 'MM/DD/YYYY' (e.g., '3/1/2025'). Cannot be used with search_recency_filter."
                    )
                ),
                (
                    "search_before_date",
                    Zod.Describe(
                        Zod.Optional(Zod.String()),
                        "Include only results published before this date. Format: 'MM/DD/YYYY' (e.g., '3/15/2025'). Cannot be used with search_recency_filter."
                    )
                ),
                (
                    "last_updated_after_filter",
                    Zod.Describe(
                        Zod.Optional(Zod.String()),
                        "Include only results last updated after this date. Format: 'MM/DD/YYYY' (e.g., '3/1/2025'). Cannot be used with search_recency_filter."
                    )
                ),
                (
                    "last_updated_before_filter",
                    Zod.Describe(
                        Zod.Optional(Zod.String()),
                        "Include only results last updated before this date. Format: 'MM/DD/YYYY' (e.g., '3/15/2025'). Cannot be used with search_recency_filter."
                    )
                ),
                (
                    "search_recency_filter",
                    Zod.Describe(
                        Zod.Optional(Zod.Enum("day", "week", "month", "year")),
                        "Filter results by relative time period. Cannot be used with search_after_date or search_before_date."
                    )
                )
            )
        )
    );

    private static readonly LazySchema s_outputSchema = SchemaUtils.LazySchema(() =>
        SchemaUtils.ZodSchema(
            Zod.Union(
                // Success response
                Zod.Object(
                    (
                        "results",
                        Zod.Array(
                            Zod.Object(
                                ("title", Zod.String()),
                                ("url", Zod.String()),
                                ("snippet", Zod.String()),
                                ("date", Zod.Optional(Zod.String())),
                                ("lastUpdated", Zod.Optional(Zod.String()))
                            )
                        )
                    ),
                    ("id", Zod.String())
                ),
                // Error response
                Zod.Object(
                    ("error", Zod.Enum("api_error", "rate_limit", "timeout", "invalid_input", "unknown")),
                    ("statusCode", Zod.Optional(Zod.Number())),
                    ("message", Zod.String())
                )
            )
        )
    );

    /// <summary>Upstream's <c>perplexitySearchToolFactory</c>.</summary>
    public static Func<
        PerplexitySearchConfig,
        ProviderExecutedTool<PerplexitySearchInput, IPerplexitySearchOutput>
    > PerplexitySearchToolFactory { get; } =
        ProviderExecutedToolFactory.CreateProviderExecutedToolFactory<
            PerplexitySearchInput,
            IPerplexitySearchOutput,
            PerplexitySearchConfig
        >("gateway.perplexity_search", s_inputSchema, s_outputSchema);

    /// <summary>
    /// Upstream's <c>perplexitySearch</c>: searches the web using Perplexity's Search API for real-time information, news, research
    /// papers, and articles. Provides ranked search results with advanced filtering options including domain, language, date range, and
    /// recency filters.
    /// </summary>
    public static ProviderExecutedTool<PerplexitySearchInput, IPerplexitySearchOutput> PerplexitySearch(
        PerplexitySearchConfig? config = null
    ) => PerplexitySearchToolFactory(config ?? new PerplexitySearchConfig());
}
