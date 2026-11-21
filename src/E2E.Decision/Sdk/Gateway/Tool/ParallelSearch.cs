// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>
/// Upstream's <c>ParallelSearchMode</c>. <c>turbo</c> and <c>fast</c> trade excerpt depth for latency (<c>turbo</c> is English and
/// Japanese only), <c>basic</c> is balanced, <c>advanced</c> is the most thorough. Deprecated: <c>one-shot</c> (runs as <c>basic</c>)
/// and <c>agentic</c> (runs as <c>advanced</c>).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ParallelSearchMode>))]
public enum ParallelSearchMode
{
    /// <summary><c>turbo</c>.</summary>
    [JsonStringEnumMemberName("turbo")]
    Turbo,

    /// <summary><c>fast</c>.</summary>
    [JsonStringEnumMemberName("fast")]
    Fast,

    /// <summary><c>basic</c>.</summary>
    [JsonStringEnumMemberName("basic")]
    Basic,

    /// <summary><c>advanced</c>.</summary>
    [JsonStringEnumMemberName("advanced")]
    Advanced,

    /// <summary><c>one-shot</c>.</summary>
    [JsonStringEnumMemberName("one-shot")]
    OneShot,

    /// <summary><c>agentic</c>.</summary>
    [JsonStringEnumMemberName("agentic")]
    Agentic,
}

/// <summary>Upstream's <c>ParallelSearchSourcePolicy</c>.</summary>
public sealed class ParallelSearchSourcePolicy
{
    /// <summary>
    /// Domains or domain/path prefixes to restrict results to. When set, <see cref="ExcludeDomains"/> is ignored. Example:
    /// <c>['wikipedia.org', 'docs.python.org/3']</c>.
    /// </summary>
    public List<string>? IncludeDomains { get; init; }

    /// <summary>Domains or domain/path prefixes to exclude from results. Example: <c>['reddit.com']</c>.</summary>
    public List<string>? ExcludeDomains { get; init; }

    /// <summary>Only include results published on or after this date (YYYY-MM-DD). Example: <c>2024-01-01</c>.</summary>
    public string? AfterDate { get; init; }
}

/// <summary>Upstream's <c>ParallelSearchExcerpts</c>.</summary>
public sealed class ParallelSearchExcerpts
{
    /// <summary>Maximum characters per result.</summary>
    public double? MaxCharsPerResult { get; init; }

    /// <summary>Maximum total characters across all results. Deprecated: use the top-level <c>MaxCharsTotal</c> option instead.</summary>
    public double? MaxCharsTotal { get; init; }
}

/// <summary>Upstream's <c>ParallelSearchFetchPolicy</c>.</summary>
public sealed class ParallelSearchFetchPolicy
{
    /// <summary>Maximum age in seconds of cached content before a live fetch is attempted. Minimum 600; lower values are raised to 600.</summary>
    public double? MaxAgeSeconds { get; init; }

    /// <summary>Timeout in seconds for a live fetch when content is not cached. The gateway caps it to stay within its own tool timeout.</summary>
    public double? TimeoutSeconds { get; init; }

    /// <summary>Return an error instead of falling back to older cached content when a live fetch fails or times out. Defaults to false.</summary>
    public bool? DisableCacheFallback { get; init; }
}

/// <summary>Upstream's <c>ParallelSearchConfig</c>.</summary>
public sealed class ParallelSearchConfig
{
    /// <summary>Search mode. Defaults to <c>basic</c>.</summary>
    public ParallelSearchMode? Mode { get; init; }

    /// <summary>Default maximum number of results to return (1-20). Defaults to 10 if not specified.</summary>
    public double? MaxResults { get; init; }

    /// <summary>Default source policy for controlling which domains to include/exclude.</summary>
    public ParallelSearchSourcePolicy? SourcePolicy { get; init; }

    /// <summary>Default excerpt configuration for controlling result length.</summary>
    public ParallelSearchExcerpts? Excerpts { get; init; }

    /// <summary>Maximum total characters across all result excerpts.</summary>
    public double? MaxCharsTotal { get; init; }

    /// <summary>Default fetch policy for controlling content freshness.</summary>
    public ParallelSearchFetchPolicy? FetchPolicy { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code to localize results, e.g. <c>us</c>. Invalid codes are ignored with a warning.</summary>
    public string? Location { get; init; }
}

/// <summary>Upstream's <c>ParallelSearchResult</c>.</summary>
public sealed class ParallelSearchResult
{
    /// <summary>URL of the search result.</summary>
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    /// <summary>Title of the page, if available.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Publication date of the page (YYYY-MM-DD), if available.</summary>
    [JsonPropertyName("publish_date")]
    public string? PublishDate { get; init; }

    /// <summary>Relevant excerpts from the page, formatted as markdown.</summary>
    [JsonPropertyName("excerpts")]
    public required List<string> Excerpts { get; init; }
}

/// <summary>Upstream's <c>ParallelSearchWarning</c>.</summary>
public sealed class ParallelSearchWarning
{
    /// <summary>Warning type, e.g. <c>input_validation_warning</c>. New types may be added.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Human-readable message.</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>Optional detail supporting the warning.</summary>
    [JsonPropertyName("detail")]
    public Dictionary<string, JsonNode?>? Detail { get; init; }
}

/// <summary>Upstream's <c>ParallelSearchUsage</c>.</summary>
public sealed class ParallelSearchUsage
{
    /// <summary>Usage item name, e.g. <c>sku_search</c>.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Count for the usage item.</summary>
    [JsonPropertyName("count")]
    public double? Count { get; init; }
}

/// <summary>Upstream's <c>ParallelSearchOutput</c>: a <see cref="ParallelSearchResponse"/> or a <see cref="GatewayToolError"/> (<c>ParallelSearchError</c>).</summary>
public interface IParallelSearchOutput { }

/// <summary>Upstream's <c>ParallelSearchResponse</c>.</summary>
public sealed class ParallelSearchResponse : IParallelSearchOutput
{
    /// <summary>Unique identifier for this search request.</summary>
    [JsonPropertyName("search_id")]
    public required string SearchId { get; init; }

    /// <summary>Search results, ordered by decreasing relevance.</summary>
    [JsonPropertyName("results")]
    public required List<ParallelSearchResult> Results { get; init; }

    /// <summary>Non-fatal issues with the request, such as an ignored <c>location</c>.</summary>
    [JsonPropertyName("warnings")]
    public List<ParallelSearchWarning>? Warnings { get; init; }

    /// <summary>Usage reported by Parallel for this search.</summary>
    [JsonPropertyName("usage")]
    public List<ParallelSearchUsage>? Usage { get; init; }

    /// <summary>Session identifier shared by the searches made during one request.</summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }
}

/// <summary>Upstream's <c>ParallelSearchInput.source_policy</c>.</summary>
public sealed class ParallelSearchInputSourcePolicy
{
    /// <summary>Domains or domain/path prefixes to restrict results to.</summary>
    [JsonPropertyName("include_domains")]
    public List<string>? IncludeDomains { get; init; }

    /// <summary>Domains or domain/path prefixes to exclude.</summary>
    [JsonPropertyName("exclude_domains")]
    public List<string>? ExcludeDomains { get; init; }

    /// <summary>Only include results published on or after this date.</summary>
    [JsonPropertyName("after_date")]
    public string? AfterDate { get; init; }
}

/// <summary>Upstream's <c>ParallelSearchInput.excerpts</c>.</summary>
public sealed class ParallelSearchInputExcerpts
{
    /// <summary>Maximum characters per result.</summary>
    [JsonPropertyName("max_chars_per_result")]
    public double? MaxCharsPerResult { get; init; }

    /// <summary>Deprecated: use the top-level <c>max_chars_total</c>.</summary>
    [JsonPropertyName("max_chars_total")]
    public double? MaxCharsTotal { get; init; }
}

/// <summary>Upstream's <c>ParallelSearchInput.fetch_policy</c>.</summary>
public sealed class ParallelSearchInputFetchPolicy
{
    /// <summary>Maximum age of cached content in seconds before a live fetch.</summary>
    [JsonPropertyName("max_age_seconds")]
    public double? MaxAgeSeconds { get; init; }

    /// <summary>Timeout in seconds for a live fetch when content is not cached.</summary>
    [JsonPropertyName("timeout_seconds")]
    public double? TimeoutSeconds { get; init; }

    /// <summary>Return an error instead of older cached content when a live fetch fails.</summary>
    [JsonPropertyName("disable_cache_fallback")]
    public bool? DisableCacheFallback { get; init; }
}

/// <summary>Upstream's <c>ParallelSearchInput</c>.</summary>
public sealed class ParallelSearchInput
{
    /// <summary>
    /// Natural-language description of the web research goal. Include source or freshness guidance and broader context from the task.
    /// Maximum 5000 characters.
    /// </summary>
    [JsonPropertyName("objective")]
    public required string Objective { get; init; }

    /// <summary>2-3 concise keyword queries, 3-6 words each (max 5, 200 characters each).</summary>
    [JsonPropertyName("search_queries")]
    public List<string>? SearchQueries { get; init; }

    /// <summary>Search mode. Defaults to <c>basic</c>.</summary>
    [JsonPropertyName("mode")]
    public ParallelSearchMode? Mode { get; init; }

    /// <summary>Maximum number of results to return (1-20). Defaults to 10 if not specified.</summary>
    [JsonPropertyName("max_results")]
    public double? MaxResults { get; init; }

    /// <summary>Source policy for controlling which domains to include/exclude.</summary>
    [JsonPropertyName("source_policy")]
    public ParallelSearchInputSourcePolicy? SourcePolicy { get; init; }

    /// <summary>Excerpt configuration for controlling result length.</summary>
    [JsonPropertyName("excerpts")]
    public ParallelSearchInputExcerpts? Excerpts { get; init; }

    /// <summary>Maximum total characters across all result excerpts.</summary>
    [JsonPropertyName("max_chars_total")]
    public double? MaxCharsTotal { get; init; }

    /// <summary>Fetch policy for controlling content freshness.</summary>
    [JsonPropertyName("fetch_policy")]
    public ParallelSearchInputFetchPolicy? FetchPolicy { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code to localize results.</summary>
    [JsonPropertyName("location")]
    public string? Location { get; init; }
}

/// <summary>
/// The tool of <c>tool/parallel-search.ts</c>. The class name is .NET-only: C# has no module-level functions, so <c>parallelSearch</c>
/// and <c>parallelSearchToolFactory</c> are members of it.
/// </summary>
public static class ParallelSearchTool
{
    private static readonly string[] s_parallelSearchModes =
    [
        "turbo",
        "fast",
        "basic",
        "advanced",
        "one-shot",
        "agentic",
    ];

    private static readonly LazySchema s_inputSchema = SchemaUtils.LazySchema(() =>
        SchemaUtils.ZodSchema(
            Zod.Object(
                (
                    "objective",
                    Zod.Describe(
                        Zod.String(),
                        "Natural-language description of the web research goal, including source or freshness guidance and broader context from the task. Maximum 5000 characters."
                    )
                ),
                (
                    "search_queries",
                    Zod.Describe(
                        Zod.Optional(Zod.Array(Zod.String())),
                        "Provide 2-3 concise keyword queries, 3-6 words each (max 5, 200 chars each). Strongly recommended; the search runs on these queries."
                    )
                ),
                (
                    "mode",
                    Zod.Describe(
                        Zod.Optional(Zod.Enum(s_parallelSearchModes)),
                        "Search mode: \"turbo\" and \"fast\" return quickly with shorter excerpts, \"basic\" is balanced (default), \"advanced\" is the most thorough. \"turbo\" supports English and Japanese only. \"one-shot\" and \"agentic\" are deprecated aliases of \"basic\" and \"advanced\"."
                    )
                ),
                (
                    "max_results",
                    Zod.Describe(
                        Zod.Optional(Zod.Number()),
                        "Maximum number of results to return (1-20). Defaults to 10 if not specified."
                    )
                ),
                (
                    "source_policy",
                    Zod.Describe(
                        Zod.Optional(
                            Zod.Object(
                                (
                                    "include_domains",
                                    Zod.Describe(
                                        Zod.Optional(Zod.Array(Zod.String())),
                                        "Domains or domain/path prefixes to restrict results to, without scheme or port. Example: ['wikipedia.org', 'docs.python.org/3']."
                                    )
                                ),
                                (
                                    "exclude_domains",
                                    Zod.Describe(
                                        Zod.Optional(Zod.Array(Zod.String())),
                                        "Domains or domain/path prefixes to exclude, without scheme or port. Example: ['reddit.com']."
                                    )
                                ),
                                (
                                    "after_date",
                                    Zod.Describe(
                                        Zod.Optional(Zod.String()),
                                        "Only include results published on or after this date. Use an ISO 8601 calendar date formatted YYYY-MM-DD (e.g. 2025-01-01); do not include a time."
                                    )
                                )
                            )
                        ),
                        "Source policy for controlling which domains to include/exclude and freshness."
                    )
                ),
                (
                    "excerpts",
                    Zod.Describe(
                        Zod.Optional(
                            Zod.Object(
                                (
                                    "max_chars_per_result",
                                    Zod.Describe(Zod.Optional(Zod.Number()), "Maximum characters per result.")
                                ),
                                (
                                    "max_chars_total",
                                    Zod.Describe(
                                        Zod.Optional(Zod.Number()),
                                        "Deprecated: use the top-level max_chars_total."
                                    )
                                )
                            )
                        ),
                        "Excerpt configuration for controlling result length."
                    )
                ),
                (
                    "max_chars_total",
                    Zod.Describe(Zod.Optional(Zod.Number()), "Maximum total characters across all result excerpts.")
                ),
                (
                    "fetch_policy",
                    Zod.Describe(
                        Zod.Optional(
                            Zod.Object(
                                (
                                    "max_age_seconds",
                                    Zod.Describe(
                                        Zod.Optional(Zod.Number()),
                                        "Maximum age of cached content in seconds before a live fetch, minimum 600."
                                    )
                                ),
                                (
                                    "timeout_seconds",
                                    Zod.Describe(
                                        Zod.Optional(Zod.Number()),
                                        "Timeout in seconds for a live fetch when content is not cached. Capped by the gateway tool timeout."
                                    )
                                ),
                                (
                                    "disable_cache_fallback",
                                    Zod.Describe(
                                        Zod.Optional(Zod.Boolean()),
                                        "Return an error instead of older cached content when a live fetch fails."
                                    )
                                )
                            )
                        ),
                        "Fetch policy for controlling content freshness."
                    )
                ),
                (
                    "location",
                    Zod.Describe(
                        Zod.Optional(Zod.String()),
                        "ISO 3166-1 alpha-2 country code to localize results, e.g. \"us\"."
                    )
                )
            )
        )
    );

    private static readonly LazySchema s_outputSchema = SchemaUtils.LazySchema(() =>
        SchemaUtils.ZodSchema(
            Zod.Union(
                // Success response (Parallel's response, passed through by the gateway)
                Zod.Object(
                    ("search_id", Zod.String()),
                    (
                        "results",
                        Zod.Array(
                            Zod.Object(
                                ("url", Zod.String()),
                                ("title", Zod.Optional(Zod.Nullable(Zod.String()))),
                                ("publish_date", Zod.Optional(Zod.Nullable(Zod.String()))),
                                ("excerpts", Zod.Array(Zod.String()))
                            )
                        )
                    ),
                    (
                        "warnings",
                        Zod.Optional(
                            Zod.Nullable(
                                Zod.Array(
                                    Zod.Object(
                                        ("type", Zod.String()),
                                        ("message", Zod.String()),
                                        ("detail", Zod.Optional(Zod.Nullable(Zod.Record(Zod.Unknown()))))
                                    )
                                )
                            )
                        )
                    ),
                    (
                        "usage",
                        Zod.Optional(
                            Zod.Nullable(
                                Zod.Array(Zod.Object(("name", Zod.String()), ("count", Zod.Optional(Zod.Number()))))
                            )
                        )
                    ),
                    ("session_id", Zod.Optional(Zod.String()))
                ),
                // Error response
                GatewayToolErrors.GatewayToolErrorSchema()
            )
        )
    );

    /// <summary>Upstream's <c>parallelSearchToolFactory</c>.</summary>
    public static Func<
        ParallelSearchConfig,
        ProviderExecutedTool<ParallelSearchInput, IParallelSearchOutput>
    > ParallelSearchToolFactory { get; } =
        ProviderExecutedToolFactory.CreateProviderExecutedToolFactory<
            ParallelSearchInput,
            IParallelSearchOutput,
            ParallelSearchConfig
        >("gateway.parallel_search", s_inputSchema, s_outputSchema);

    /// <summary>
    /// Upstream's <c>parallelSearch</c>: searches the web using Parallel AI's Search API for LLM-optimized excerpts. Takes a natural
    /// language objective and returns relevant excerpts, replacing multiple keyword searches with a single call for broad or complex
    /// queries. Supports different search types for depth vs breadth tradeoffs.
    /// </summary>
    public static ProviderExecutedTool<ParallelSearchInput, IParallelSearchOutput> ParallelSearch(
        ParallelSearchConfig? config = null
    ) => ParallelSearchToolFactory(config ?? new ParallelSearchConfig());
}
