// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>Upstream's <c>ExaSearchType</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExaSearchType>))]
public enum ExaSearchType
{
    /// <summary><c>auto</c>.</summary>
    [JsonStringEnumMemberName("auto")]
    Auto,

    /// <summary><c>fast</c>.</summary>
    [JsonStringEnumMemberName("fast")]
    Fast,

    /// <summary><c>instant</c>.</summary>
    [JsonStringEnumMemberName("instant")]
    Instant,
}

/// <summary>Upstream's <c>ExaSearchCategory</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExaSearchCategory>))]
public enum ExaSearchCategory
{
    /// <summary><c>company</c>.</summary>
    [JsonStringEnumMemberName("company")]
    Company,

    /// <summary><c>people</c>.</summary>
    [JsonStringEnumMemberName("people")]
    People,

    /// <summary><c>research paper</c>.</summary>
    [JsonStringEnumMemberName("research paper")]
    ResearchPaper,

    /// <summary><c>news</c>.</summary>
    [JsonStringEnumMemberName("news")]
    News,

    /// <summary><c>personal site</c>.</summary>
    [JsonStringEnumMemberName("personal site")]
    PersonalSite,

    /// <summary><c>financial report</c>.</summary>
    [JsonStringEnumMemberName("financial report")]
    FinancialReport,
}

/// <summary>Upstream's <c>ExaTextSection</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExaTextSection>))]
public enum ExaTextSection
{
    /// <summary><c>header</c>.</summary>
    [JsonStringEnumMemberName("header")]
    Header,

    /// <summary><c>navigation</c>.</summary>
    [JsonStringEnumMemberName("navigation")]
    Navigation,

    /// <summary><c>banner</c>.</summary>
    [JsonStringEnumMemberName("banner")]
    Banner,

    /// <summary><c>body</c>.</summary>
    [JsonStringEnumMemberName("body")]
    Body,

    /// <summary><c>sidebar</c>.</summary>
    [JsonStringEnumMemberName("sidebar")]
    Sidebar,

    /// <summary><c>footer</c>.</summary>
    [JsonStringEnumMemberName("footer")]
    Footer,

    /// <summary><c>metadata</c>.</summary>
    [JsonStringEnumMemberName("metadata")]
    Metadata,
}

/// <summary>The <c>verbosity</c> values of <see cref="ExaSearchTextConfig"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExaSearchVerbosity>))]
public enum ExaSearchVerbosity
{
    /// <summary><c>compact</c>.</summary>
    [JsonStringEnumMemberName("compact")]
    Compact,

    /// <summary><c>standard</c>.</summary>
    [JsonStringEnumMemberName("standard")]
    Standard,

    /// <summary><c>full</c>.</summary>
    [JsonStringEnumMemberName("full")]
    Full,
}

/// <summary>Upstream's <c>ExaSearchTextConfig</c>.</summary>
public sealed class ExaSearchTextConfig
{
    /// <summary>Maximum characters of text.</summary>
    public double? MaxCharacters { get; init; }

    /// <summary>Whether to keep HTML tags.</summary>
    public bool? IncludeHtmlTags { get; init; }

    /// <summary>How much text to return.</summary>
    public ExaSearchVerbosity? Verbosity { get; init; }

    /// <summary>Page sections to include.</summary>
    public List<ExaTextSection>? IncludeSections { get; init; }

    /// <summary>Page sections to exclude.</summary>
    public List<ExaTextSection>? ExcludeSections { get; init; }
}

/// <summary>Upstream's <c>ExaSearchHighlightsConfig</c>.</summary>
public sealed class ExaSearchHighlightsConfig
{
    /// <summary>The query the highlights answer.</summary>
    public string? Query { get; init; }

    /// <summary>Maximum characters of highlights.</summary>
    public double? MaxCharacters { get; init; }
}

/// <summary>Upstream's <c>ExaSearchExtrasConfig</c>.</summary>
public sealed class ExaSearchExtrasConfig
{
    /// <summary>Number of links to return.</summary>
    public double? Links { get; init; }

    /// <summary>Number of image links to return.</summary>
    public double? ImageLinks { get; init; }
}

/// <summary>Upstream's <c>ExaSearchContentsConfig</c>.</summary>
public sealed class ExaSearchContentsConfig
{
    /// <summary>Text extraction: a flag or its options.</summary>
    public BooleanOr<ExaSearchTextConfig>? Text { get; init; }

    /// <summary>Highlights extraction: a flag or its options.</summary>
    public BooleanOr<ExaSearchHighlightsConfig>? Highlights { get; init; }

    /// <summary>Maximum age of the content, in hours.</summary>
    public double? MaxAgeHours { get; init; }

    /// <summary>Live crawl timeout.</summary>
    public double? LivecrawlTimeout { get; init; }

    /// <summary>Number of subpages.</summary>
    public double? Subpages { get; init; }

    /// <summary>Subpage target: a string or a list of strings.</summary>
    public StringOrStrings? SubpageTarget { get; init; }

    /// <summary>Extra content to return.</summary>
    public ExaSearchExtrasConfig? Extras { get; init; }
}

/// <summary>Upstream's <c>ExaSearchConfig</c>.</summary>
public sealed class ExaSearchConfig
{
    /// <summary>Default search method. Exa defaults to auto when omitted.</summary>
    public ExaSearchType? Type { get; init; }

    /// <summary>Default maximum number of results to return (1-100, default: 10).</summary>
    public double? NumResults { get; init; }

    /// <summary>Default category filter for result types.</summary>
    public ExaSearchCategory? Category { get; init; }

    /// <summary>Default two-letter ISO country code for location-aware search.</summary>
    public string? UserLocation { get; init; }

    /// <summary>Default domains to include.</summary>
    public List<string>? IncludeDomains { get; init; }

    /// <summary>Default domains to exclude.</summary>
    public List<string>? ExcludeDomains { get; init; }

    /// <summary>Default published date filter (ISO 8601): from.</summary>
    public string? StartPublishedDate { get; init; }

    /// <summary>Default published date filter (ISO 8601): to.</summary>
    public string? EndPublishedDate { get; init; }

    /// <summary>Default content extraction controls.</summary>
    public ExaSearchContentsConfig? Contents { get; init; }
}

/// <summary>Upstream's <c>ExaSearchExtras</c> (<c>ExaSearchResult.extras</c>).</summary>
public sealed class ExaSearchResultExtras
{
    /// <summary>Links on the page.</summary>
    [JsonPropertyName("links")]
    public List<string>? Links { get; init; }

    /// <summary>Image links on the page.</summary>
    [JsonPropertyName("imageLinks")]
    public List<string>? ImageLinks { get; init; }
}

/// <summary>Upstream's <c>ExaSearchResult</c>.</summary>
public sealed class ExaSearchResult
{
    /// <summary>The page title.</summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>The page URL.</summary>
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    /// <summary>The result ID.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>The publication date.</summary>
    [JsonPropertyName("publishedDate")]
    public string? PublishedDate { get; init; }

    /// <summary>The author.</summary>
    [JsonPropertyName("author")]
    public string? Author { get; init; }

    /// <summary>The image URL.</summary>
    [JsonPropertyName("image")]
    public string? Image { get; init; }

    /// <summary>The favicon URL.</summary>
    [JsonPropertyName("favicon")]
    public string? Favicon { get; init; }

    /// <summary>The page text.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>The highlights.</summary>
    [JsonPropertyName("highlights")]
    public List<string>? Highlights { get; init; }

    /// <summary>The highlight scores.</summary>
    [JsonPropertyName("highlightScores")]
    public List<double>? HighlightScores { get; init; }

    /// <summary>The summary.</summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    /// <summary>The subpages.</summary>
    [JsonPropertyName("subpages")]
    public List<ExaSearchResult>? Subpages { get; init; }

    /// <summary>Extra content.</summary>
    [JsonPropertyName("extras")]
    public ExaSearchResultExtras? Extras { get; init; }
}

/// <summary>Upstream's <c>ExaSearchResponse.costDollars</c>.</summary>
public sealed class ExaSearchCostDollars
{
    /// <summary>The total cost.</summary>
    [JsonPropertyName("total")]
    public double? Total { get; init; }

    /// <summary>The cost by search type.</summary>
    [JsonPropertyName("search")]
    public Dictionary<string, double>? Search { get; init; }
}

/// <summary>Upstream's <c>ExaSearchOutput</c>: an <see cref="ExaSearchResponse"/> or an <see cref="ExaSearchError"/>.</summary>
public interface IExaSearchOutput { }

/// <summary>Upstream's <c>ExaSearchResponse</c>.</summary>
public sealed class ExaSearchResponse : IExaSearchOutput
{
    /// <summary>The request ID.</summary>
    [JsonPropertyName("requestId")]
    public required string RequestId { get; init; }

    /// <summary>The search type.</summary>
    [JsonPropertyName("searchType")]
    public string? SearchType { get; init; }

    /// <summary>The search type Exa resolved.</summary>
    [JsonPropertyName("resolvedSearchType")]
    public string? ResolvedSearchType { get; init; }

    /// <summary>The results.</summary>
    [JsonPropertyName("results")]
    public required List<ExaSearchResult> Results { get; init; }

    /// <summary>The cost.</summary>
    [JsonPropertyName("costDollars")]
    public ExaSearchCostDollars? CostDollars { get; init; }
}

/// <summary>Upstream's <c>ExaSearchError</c>.</summary>
public sealed class ExaSearchError : IExaSearchOutput
{
    /// <summary>The kind of error.</summary>
    [JsonPropertyName("error")]
    public required BrowserbaseToolErrorCode Error { get; init; }

    /// <summary>The HTTP status code, when there is one.</summary>
    [JsonPropertyName("statusCode")]
    public double? StatusCode { get; init; }

    /// <summary>A human-readable message.</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

/// <summary>Upstream's <c>ExaSearchInput.contents.text</c> options.</summary>
public sealed class ExaSearchInputText
{
    /// <summary>Maximum characters of text.</summary>
    [JsonPropertyName("max_characters")]
    public double? MaxCharacters { get; init; }

    /// <summary>Whether to keep HTML tags.</summary>
    [JsonPropertyName("include_html_tags")]
    public bool? IncludeHtmlTags { get; init; }

    /// <summary>How much text to return.</summary>
    [JsonPropertyName("verbosity")]
    public ExaSearchVerbosity? Verbosity { get; init; }

    /// <summary>Page sections to include.</summary>
    [JsonPropertyName("include_sections")]
    public List<ExaTextSection>? IncludeSections { get; init; }

    /// <summary>Page sections to exclude.</summary>
    [JsonPropertyName("exclude_sections")]
    public List<ExaTextSection>? ExcludeSections { get; init; }
}

/// <summary>Upstream's <c>ExaSearchInput.contents.highlights</c> options.</summary>
public sealed class ExaSearchInputHighlights
{
    /// <summary>The query the highlights answer.</summary>
    [JsonPropertyName("query")]
    public string? Query { get; init; }

    /// <summary>Maximum characters of highlights.</summary>
    [JsonPropertyName("max_characters")]
    public double? MaxCharacters { get; init; }
}

/// <summary>Upstream's <c>ExaSearchInput.contents.extras</c>.</summary>
public sealed class ExaSearchInputExtras
{
    /// <summary>Number of links to return.</summary>
    [JsonPropertyName("links")]
    public double? Links { get; init; }

    /// <summary>Number of image links to return.</summary>
    [JsonPropertyName("image_links")]
    public double? ImageLinks { get; init; }
}

/// <summary>Upstream's <c>ExaSearchInput.contents</c>.</summary>
public sealed class ExaSearchInputContents
{
    /// <summary>Text extraction: a flag or its options.</summary>
    [JsonPropertyName("text")]
    public BooleanOr<ExaSearchInputText>? Text { get; init; }

    /// <summary>Highlights extraction: a flag or its options.</summary>
    [JsonPropertyName("highlights")]
    public BooleanOr<ExaSearchInputHighlights>? Highlights { get; init; }

    /// <summary>Maximum age of the content, in hours.</summary>
    [JsonPropertyName("max_age_hours")]
    public double? MaxAgeHours { get; init; }

    /// <summary>Live crawl timeout.</summary>
    [JsonPropertyName("livecrawl_timeout")]
    public double? LivecrawlTimeout { get; init; }

    /// <summary>Number of subpages.</summary>
    [JsonPropertyName("subpages")]
    public double? Subpages { get; init; }

    /// <summary>Subpage target: a string or a list of strings.</summary>
    [JsonPropertyName("subpage_target")]
    public StringOrStrings? SubpageTarget { get; init; }

    /// <summary>Extra content to return.</summary>
    [JsonPropertyName("extras")]
    public ExaSearchInputExtras? Extras { get; init; }
}

/// <summary>Upstream's <c>ExaSearchInput</c>.</summary>
public sealed class ExaSearchInput
{
    /// <summary>Natural-language web search query.</summary>
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    /// <summary>Search method.</summary>
    [JsonPropertyName("type")]
    public ExaSearchType? Type { get; init; }

    /// <summary>Maximum number of results to return (1-100, default: 10).</summary>
    [JsonPropertyName("num_results")]
    public double? NumResults { get; init; }

    /// <summary>Content category to focus results.</summary>
    [JsonPropertyName("category")]
    public ExaSearchCategory? Category { get; init; }

    /// <summary>Two-letter ISO country code.</summary>
    [JsonPropertyName("user_location")]
    public string? UserLocation { get; init; }

    /// <summary>Only return results from these domains.</summary>
    [JsonPropertyName("include_domains")]
    public List<string>? IncludeDomains { get; init; }

    /// <summary>Exclude results from these domains.</summary>
    [JsonPropertyName("exclude_domains")]
    public List<string>? ExcludeDomains { get; init; }

    /// <summary>Only return links published after this ISO 8601 date.</summary>
    [JsonPropertyName("start_published_date")]
    public string? StartPublishedDate { get; init; }

    /// <summary>Only return links published before this ISO 8601 date.</summary>
    [JsonPropertyName("end_published_date")]
    public string? EndPublishedDate { get; init; }

    /// <summary>Controls extracted page content and freshness.</summary>
    [JsonPropertyName("contents")]
    public ExaSearchInputContents? Contents { get; init; }
}

/// <summary>
/// The tool of <c>tool/exa-search.ts</c>. The class name is .NET-only: C# has no module-level functions, so <c>exaSearch</c> and
/// <c>exaSearchToolFactory</c> are members of it.
/// </summary>
public static class ExaSearchTool
{
    private static readonly string[] s_textSections =
    [
        "header",
        "navigation",
        "banner",
        "body",
        "sidebar",
        "footer",
        "metadata",
    ];

    private static readonly LazySchema s_inputSchema = SchemaUtils.LazySchema(() =>
        SchemaUtils.ZodSchema(
            Zod.Object(
                ("query", Zod.Describe(Zod.String(), "Natural-language web search query. This is required.")),
                (
                    "type",
                    Zod.Describe(
                        Zod.Optional(Zod.Enum("auto", "fast", "instant")),
                        "Search method. Use auto for the default balance of speed and quality."
                    )
                ),
                (
                    "num_results",
                    Zod.Describe(
                        Zod.Optional(Zod.Number()),
                        "Maximum number of results to return (1-100, default: 10)."
                    )
                ),
                (
                    "category",
                    Zod.Describe(
                        Zod.Optional(
                            Zod.Enum("company", "people", "research paper", "news", "personal site", "financial report")
                        ),
                        "Optional content category to focus results."
                    )
                ),
                (
                    "user_location",
                    Zod.Describe(Zod.Optional(Zod.String()), "Two-letter ISO country code such as 'US'.")
                ),
                (
                    "include_domains",
                    Zod.Describe(Zod.Optional(Zod.Array(Zod.String())), "Only return results from these domains.")
                ),
                (
                    "exclude_domains",
                    Zod.Describe(Zod.Optional(Zod.Array(Zod.String())), "Exclude results from these domains.")
                ),
                (
                    "start_published_date",
                    Zod.Describe(Zod.Optional(Zod.String()), "Only return links published after this ISO 8601 date.")
                ),
                (
                    "end_published_date",
                    Zod.Describe(Zod.Optional(Zod.String()), "Only return links published before this ISO 8601 date.")
                ),
                (
                    "contents",
                    Zod.Describe(
                        Zod.Optional(
                            Zod.Object(
                                (
                                    "text",
                                    Zod.Optional(
                                        Zod.Union(
                                            Zod.Boolean(),
                                            Zod.Object(
                                                ("max_characters", Zod.Optional(Zod.Number())),
                                                ("include_html_tags", Zod.Optional(Zod.Boolean())),
                                                ("verbosity", Zod.Optional(Zod.Enum("compact", "standard", "full"))),
                                                ("include_sections", Zod.Optional(Zod.Array(Zod.Enum(s_textSections)))),
                                                ("exclude_sections", Zod.Optional(Zod.Array(Zod.Enum(s_textSections))))
                                            )
                                        )
                                    )
                                ),
                                (
                                    "highlights",
                                    Zod.Optional(
                                        Zod.Union(
                                            Zod.Boolean(),
                                            Zod.Object(
                                                ("query", Zod.Optional(Zod.String())),
                                                ("max_characters", Zod.Optional(Zod.Number()))
                                            )
                                        )
                                    )
                                ),
                                ("max_age_hours", Zod.Optional(Zod.Number())),
                                ("livecrawl_timeout", Zod.Optional(Zod.Number())),
                                ("subpages", Zod.Optional(Zod.Number())),
                                ("subpage_target", Zod.Optional(Zod.Union(Zod.String(), Zod.Array(Zod.String())))),
                                (
                                    "extras",
                                    Zod.Optional(
                                        Zod.Object(
                                            ("links", Zod.Optional(Zod.Number())),
                                            ("image_links", Zod.Optional(Zod.Number()))
                                        )
                                    )
                                )
                            )
                        ),
                        "Controls extracted page content and freshness."
                    )
                )
            )
        )
    );

    private static readonly LazySchema s_outputSchema = SchemaUtils.LazySchema(() =>
        SchemaUtils.ZodSchema(
            Zod.Union(
                Zod.Object(
                    ("requestId", Zod.String()),
                    ("searchType", Zod.Optional(Zod.String())),
                    ("resolvedSearchType", Zod.Optional(Zod.String())),
                    (
                        "results",
                        Zod.Array(
                            Zod.Object(
                                ("title", Zod.String()),
                                ("url", Zod.String()),
                                ("id", Zod.String()),
                                ("publishedDate", Zod.Optional(Zod.Nullable(Zod.String()))),
                                ("author", Zod.Optional(Zod.Nullable(Zod.String()))),
                                ("image", Zod.Optional(Zod.Nullable(Zod.String()))),
                                ("favicon", Zod.Optional(Zod.Nullable(Zod.String()))),
                                ("text", Zod.Optional(Zod.String())),
                                ("highlights", Zod.Optional(Zod.Array(Zod.String()))),
                                ("highlightScores", Zod.Optional(Zod.Array(Zod.Number()))),
                                ("summary", Zod.Optional(Zod.String())),
                                ("subpages", Zod.Optional(Zod.Array(Zod.Any()))),
                                (
                                    "extras",
                                    Zod.Optional(
                                        Zod.Object(
                                            ("links", Zod.Optional(Zod.Array(Zod.String()))),
                                            ("imageLinks", Zod.Optional(Zod.Array(Zod.String())))
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    (
                        "costDollars",
                        Zod.Optional(
                            Zod.Object(
                                ("total", Zod.Optional(Zod.Number())),
                                ("search", Zod.Optional(Zod.Record(Zod.Number())))
                            )
                        )
                    )
                ),
                Zod.Object(
                    (
                        "error",
                        Zod.Enum(
                            "api_error",
                            "rate_limit",
                            "timeout",
                            "invalid_input",
                            "configuration_error",
                            "execution_error",
                            "unknown"
                        )
                    ),
                    ("statusCode", Zod.Optional(Zod.Number())),
                    ("message", Zod.String())
                )
            )
        )
    );

    /// <summary>Upstream's <c>exaSearchToolFactory</c>.</summary>
    public static Func<
        ExaSearchConfig,
        ProviderExecutedTool<ExaSearchInput, IExaSearchOutput>
    > ExaSearchToolFactory { get; } =
        ProviderExecutedToolFactory.CreateProviderExecutedToolFactory<
            ExaSearchInput,
            IExaSearchOutput,
            ExaSearchConfig
        >("gateway.exa_search", s_inputSchema, s_outputSchema);

    /// <summary>
    /// Upstream's <c>exaSearch</c>: searches the web using Exa for current information and token-efficient excerpts optimized for agent
    /// workflows. Supports search type, category, domain, date, location, and content extraction controls.
    /// </summary>
    public static ProviderExecutedTool<ExaSearchInput, IExaSearchOutput> ExaSearch(ExaSearchConfig? config = null) =>
        ExaSearchToolFactory(config ?? new ExaSearchConfig());
}
