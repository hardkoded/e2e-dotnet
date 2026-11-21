// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>Upstream's <c>DeepSeekSearchConfig</c>.</summary>
public sealed class DeepSeekSearchConfig
{
    /// <summary>Pin the query instead of allowing the model to choose it.</summary>
    public string? Query { get; init; }

    /// <summary>Maximum number of results, 1-50 (default: 10).</summary>
    public double? Count { get; init; }

    /// <summary>Include available page content (default: false).</summary>
    public bool? NeedContent { get; init; }

    /// <summary>Original question, 1-128 Unicode characters; null means absent.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Undefinable<string> OriQuestion { get; init; }

    /// <summary>Developer-configured IP address for result localization.</summary>
    public string? Localization { get; init; }
}

/// <summary>Upstream's <c>DeepSeekSearchInput</c>.</summary>
public sealed class DeepSeekSearchInput
{
    /// <summary>Web search query, 1 to 128 Unicode characters.</summary>
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    /// <summary>Number of results, 1 to 50 (default: 10).</summary>
    [JsonPropertyName("count")]
    public int? Count { get; init; }

    /// <summary>Include available page content (default: false).</summary>
    [JsonPropertyName("need_content")]
    public bool? NeedContent { get; init; }

    /// <summary>Original user question, 1 to 128 Unicode characters.</summary>
    [JsonPropertyName("ori_question")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Undefinable<string> OriQuestion { get; init; }
}

/// <summary>Upstream's <c>DeepSeekSearchResult</c>.</summary>
public sealed class DeepSeekSearchResult
{
    /// <summary>The authority of the source.</summary>
    [JsonPropertyName("authority")]
    public double? Authority { get; init; }

    /// <summary>The page content.</summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }

    /// <summary>The page date.</summary>
    [JsonPropertyName("date")]
    public string? Date { get; init; }

    /// <summary>The rerank score.</summary>
    [JsonPropertyName("rerank_score")]
    public double? RerankScore { get; init; }

    /// <summary>The site icon URL.</summary>
    [JsonPropertyName("site_icon")]
    public string? SiteIcon { get; init; }

    /// <summary>The site name.</summary>
    [JsonPropertyName("site_name")]
    public string? SiteName { get; init; }

    /// <summary>The snippet.</summary>
    [JsonPropertyName("snippet")]
    public string? Snippet { get; init; }

    /// <summary>The summary.</summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    /// <summary>The title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>The URL.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    /// <summary>Fields the Gateway adds later (the schema keeps them).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

/// <summary>Upstream's <c>DeepSeekSearchOutput</c>: a <see cref="DeepSeekSearchResponse"/> or a <see cref="GatewayToolError"/>.</summary>
public interface IDeepSeekSearchOutput { }

/// <summary>Upstream's <c>DeepSeekSearchResponse</c>.</summary>
public sealed class DeepSeekSearchResponse : IDeepSeekSearchOutput
{
    /// <summary>The search results.</summary>
    [JsonPropertyName("results")]
    public required List<DeepSeekSearchResult> Results { get; init; }

    /// <summary>Fields the Gateway adds later (the schema keeps them).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

/// <summary>
/// The tool of <c>tool/deepseek-search.ts</c>. The class name is .NET-only: C# has no module-level functions, so <c>deepseekSearch</c>
/// and <c>deepseekSearchToolFactory</c> are members of it.
/// </summary>
public static class DeepseekSearchTool
{
    private static readonly LazySchema s_inputSchema = SchemaUtils.LazySchema(() =>
        SchemaUtils.ZodSchema(
            Zod.Object(
                (
                    "query",
                    Zod.Describe(
                        Zod.String(),
                        "Web search query, 1 to 128 Unicode characters. Supports site:, -site:, lang:, after:, before:, and quoted phrases."
                    )
                ),
                (
                    "count",
                    Zod.Describe(Zod.Optional(Zod.Number(integer: true)), "Number of results, 1 to 50 (default: 10).")
                ),
                (
                    "need_content",
                    Zod.Describe(Zod.Optional(Zod.Boolean()), "Include available page content (default: false).")
                ),
                (
                    "ori_question",
                    Zod.Describe(
                        Zod.Optional(Zod.Nullable(Zod.String())),
                        "Original user question, 1 to 128 Unicode characters."
                    )
                )
            )
        )
    );

    private static readonly LazySchema s_outputSchema = SchemaUtils.LazySchema(() =>
        SchemaUtils.ZodSchema(
            Zod.Union(
                Zod.Catchall(
                    Zod.Object(
                        (
                            "results",
                            Zod.Array(
                                Zod.Catchall(
                                    Zod.Object(
                                        ("authority", Zod.Nullish(Zod.Number())),
                                        ("content", Zod.Nullish(Zod.String())),
                                        ("date", Zod.Nullish(Zod.String())),
                                        ("rerank_score", Zod.Nullish(Zod.Number())),
                                        ("site_icon", Zod.Nullish(Zod.String())),
                                        ("site_name", Zod.Nullish(Zod.String())),
                                        ("snippet", Zod.Nullish(Zod.String())),
                                        ("summary", Zod.Nullish(Zod.String())),
                                        ("title", Zod.Nullish(Zod.String())),
                                        ("url", Zod.Nullish(Zod.String()))
                                    ),
                                    Zod.Unknown()
                                )
                            )
                        )
                    ),
                    Zod.Unknown()
                ),
                GatewayToolErrors.GatewayToolErrorSchema()
            )
        )
    );

    /// <summary>Upstream's <c>deepseekSearchToolFactory</c>.</summary>
    public static Func<
        DeepSeekSearchConfig,
        ProviderExecutedTool<DeepSeekSearchInput, IDeepSeekSearchOutput>
    > DeepseekSearchToolFactory { get; } =
        ProviderExecutedToolFactory.CreateProviderExecutedToolFactory<
            DeepSeekSearchInput,
            IDeepSeekSearchOutput,
            DeepSeekSearchConfig
        >("gateway.deepseek_search", s_inputSchema, s_outputSchema);

    /// <summary>
    /// Upstream's <c>deepseekSearch</c>: searches the web through DeepSeek's standalone Search API. Executed by AI Gateway for any model,
    /// separate from native model web search.
    /// </summary>
    public static ProviderExecutedTool<DeepSeekSearchInput, IDeepSeekSearchOutput> DeepseekSearch(
        DeepSeekSearchConfig? config = null
    ) => DeepseekSearchToolFactory(config ?? new DeepSeekSearchConfig());
}
