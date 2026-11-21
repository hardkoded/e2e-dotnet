// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>Upstream's <c>BrowserbaseSearchConfig</c>.</summary>
public sealed class BrowserbaseSearchConfig
{
    /// <summary>Default maximum number of results to return (1-25, default: 10).</summary>
    public double? NumResults { get; init; }
}

/// <summary>Upstream's <c>BrowserbaseSearchInput</c>.</summary>
public sealed class BrowserbaseSearchInput
{
    /// <summary>Web search query (1-200 characters).</summary>
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    /// <summary>Maximum number of results to return (1-25, default: 10).</summary>
    [JsonPropertyName("num_results")]
    public int? NumResults { get; init; }
}

/// <summary>Upstream's <c>BrowserbaseSearchResult</c>.</summary>
public sealed class BrowserbaseSearchResult
{
    /// <summary>Unique identifier for the result.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Title of the search result.</summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>URL of the search result.</summary>
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    /// <summary>Author of the content, when available.</summary>
    [JsonPropertyName("author")]
    public string? Author { get; init; }

    /// <summary>Favicon URL, when available.</summary>
    [JsonPropertyName("favicon")]
    public string? Favicon { get; init; }

    /// <summary>Image URL, when available.</summary>
    [JsonPropertyName("image")]
    public string? Image { get; init; }

    /// <summary>Publication date in ISO 8601 format, when available.</summary>
    [JsonPropertyName("publishedDate")]
    public string? PublishedDate { get; init; }
}

/// <summary>Upstream's <c>BrowserbaseSearchOutput</c>: a <see cref="BrowserbaseSearchResponse"/> or a <see cref="BrowserbaseSearchError"/>.</summary>
public interface IBrowserbaseSearchOutput { }

/// <summary>Upstream's <c>BrowserbaseSearchResponse</c>.</summary>
public sealed class BrowserbaseSearchResponse : IBrowserbaseSearchOutput
{
    /// <summary>The search query that was executed.</summary>
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    /// <summary>Unique identifier for the request.</summary>
    [JsonPropertyName("requestId")]
    public required string RequestId { get; init; }

    /// <summary>Search results.</summary>
    [JsonPropertyName("results")]
    public required List<BrowserbaseSearchResult> Results { get; init; }
}

/// <summary>Upstream's <c>BrowserbaseSearchError</c>.</summary>
public sealed class BrowserbaseSearchError : IBrowserbaseSearchOutput
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

/// <summary>
/// The tool of <c>tool/browserbase-search.ts</c>. The class name is .NET-only: C# has no module-level functions, so
/// <c>browserbaseSearch</c> and <c>browserbaseSearchToolFactory</c> are members of it.
/// </summary>
public static class BrowserbaseSearchTool
{
    private static readonly LazySchema s_inputSchema = SchemaUtils.LazySchema(() =>
        SchemaUtils.ZodSchema(
            Zod.Object(
                (
                    "query",
                    Zod.Describe(
                        Zod.String(minLength: 1, maxLength: 200),
                        "Web search query. Must be between 1 and 200 characters."
                    )
                ),
                (
                    "num_results",
                    Zod.Describe(
                        Zod.Optional(Zod.Number(integer: true, minimum: 1, maximum: 25)),
                        "Maximum number of results to return (1-25, default: 10)."
                    )
                )
            )
        )
    );

    private static readonly LazySchema s_outputSchema = SchemaUtils.LazySchema(() =>
        SchemaUtils.ZodSchema(
            Zod.Union(
                Zod.Object(
                    ("query", Zod.String()),
                    ("requestId", Zod.String()),
                    (
                        "results",
                        Zod.Array(
                            Zod.Object(
                                ("id", Zod.String()),
                                ("title", Zod.String()),
                                ("url", Zod.String()),
                                ("author", Zod.Optional(Zod.String())),
                                ("favicon", Zod.Optional(Zod.String())),
                                ("image", Zod.Optional(Zod.String())),
                                ("publishedDate", Zod.Optional(Zod.String()))
                            )
                        )
                    )
                ),
                Zod.Object(
                    (
                        "error",
                        Zod.Enum(
                            "api_error",
                            "configuration_error",
                            "execution_error",
                            "invalid_input",
                            "rate_limit",
                            "timeout",
                            "unknown"
                        )
                    ),
                    ("statusCode", Zod.Optional(Zod.Number())),
                    ("message", Zod.String())
                )
            )
        )
    );

    /// <summary>Upstream's <c>browserbaseSearchToolFactory</c>.</summary>
    public static Func<
        BrowserbaseSearchConfig,
        ProviderExecutedTool<BrowserbaseSearchInput, IBrowserbaseSearchOutput>
    > BrowserbaseSearchToolFactory { get; } =
        ProviderExecutedToolFactory.CreateProviderExecutedToolFactory<
            BrowserbaseSearchInput,
            IBrowserbaseSearchOutput,
            BrowserbaseSearchConfig
        >("gateway.browserbase_search", s_inputSchema, s_outputSchema);

    /// <summary>
    /// Upstream's <c>browserbaseSearch</c>: searches the web using Browserbase's Search API for fast, structured results. Returns titles,
    /// URLs, and available publication metadata without requiring a browser session.
    /// </summary>
    public static ProviderExecutedTool<BrowserbaseSearchInput, IBrowserbaseSearchOutput> BrowserbaseSearch(
        BrowserbaseSearchConfig? config = null
    ) => BrowserbaseSearchToolFactory(config ?? new BrowserbaseSearchConfig());
}
