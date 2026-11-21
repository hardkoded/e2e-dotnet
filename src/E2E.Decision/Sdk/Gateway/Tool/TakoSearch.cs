// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>Upstream's <c>TakoSearchEffort</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TakoSearchEffort>))]
public enum TakoSearchEffort
{
    /// <summary><c>deep</c>.</summary>
    [JsonStringEnumMemberName("deep")]
    Deep,

    /// <summary><c>fast</c>.</summary>
    [JsonStringEnumMemberName("fast")]
    Fast,

    /// <summary><c>instant</c>.</summary>
    [JsonStringEnumMemberName("instant")]
    Instant,
}

/// <summary>Upstream's <c>TakoContentFormat</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TakoContentFormat>))]
public enum TakoContentFormat
{
    /// <summary><c>card_json</c>.</summary>
    [JsonStringEnumMemberName("card_json")]
    CardJson,

    /// <summary><c>csv</c>.</summary>
    [JsonStringEnumMemberName("csv")]
    Csv,

    /// <summary><c>json_compact</c>.</summary>
    [JsonStringEnumMemberName("json_compact")]
    JsonCompact,

    /// <summary><c>json_records</c>.</summary>
    [JsonStringEnumMemberName("json_records")]
    JsonRecords,
}

/// <summary>The <c>mode</c> values of <see cref="TakoDataSourceConfig"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TakoDataMode>))]
public enum TakoDataMode
{
    /// <summary><c>inline</c>.</summary>
    [JsonStringEnumMemberName("inline")]
    Inline,

    /// <summary><c>url</c>.</summary>
    [JsonStringEnumMemberName("url")]
    Url,
}

/// <summary>The <c>category</c> values of <see cref="TakoWebSourceConfig"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TakoWebCategory>))]
public enum TakoWebCategory
{
    /// <summary><c>finance</c>.</summary>
    [JsonStringEnumMemberName("finance")]
    Finance,

    /// <summary><c>news</c>.</summary>
    [JsonStringEnumMemberName("news")]
    News,

    /// <summary><c>sports</c>.</summary>
    [JsonStringEnumMemberName("sports")]
    Sports,
}

/// <summary>Upstream's <c>TakoDataSourceConfig</c>.</summary>
public sealed class TakoDataSourceConfig
{
    /// <summary>Maximum number of data results to return (1-20). When includeContents is true, each additional result adds its own data surcharge.</summary>
    public double? Count { get; init; }

    /// <summary>
    /// Inline rows for each data result. This adds a data surcharge based on row count and dataset source. To estimate cost, search with
    /// includeContents disabled and inspect cards.content.export_pricing. This applies to every returned card; limit sources.data.count
    /// and sources.data.maxRows to control cost.
    /// </summary>
    public bool? IncludeContents { get; init; }

    /// <summary>Requested delivery mode for card data. Search cards are always inlined.</summary>
    public TakoDataMode? Mode { get; init; }

    /// <summary>Serialization for inlined card data.</summary>
    public TakoContentFormat? ContentFormat { get; init; }

    /// <summary>
    /// Maximum rows to inline per result. Omit to use the allowance in cards.content.export_pricing. A data surcharge applies per 1,000
    /// exported rows; lower values reduce cost.
    /// </summary>
    public double? MaxRows { get; init; }

    /// <summary>Data Graph node IDs to prioritize.</summary>
    public List<string>? NodeIds { get; init; }

    /// <summary>Return only cards matching nodeIds. Requires at least one node ID.</summary>
    public bool? Strict { get; init; }
}

/// <summary>Upstream's <c>TakoWebSourceConfig</c>.</summary>
public sealed class TakoWebSourceConfig
{
    /// <summary>Maximum number of web results to return (1-20).</summary>
    public double? Count { get; init; }

    /// <summary>Inline extracted web page text. This can add a data charge.</summary>
    public bool? IncludeContents { get; init; }

    /// <summary>Optional web-result category filter.</summary>
    public TakoWebCategory? Category { get; init; }

    /// <summary>Only return results from these bare domains.</summary>
    public List<string>? IncludeDomains { get; init; }

    /// <summary>Exclude results from these bare domains.</summary>
    public List<string>? ExcludeDomains { get; init; }

    /// <summary>Maximum characters in each web-result snippet.</summary>
    public double? SnippetMaxChars { get; init; }

    /// <summary>Include highlighted passages in web results. Defaults to true in AI Gateway.</summary>
    public bool? Highlights { get; init; }

    /// <summary>Maximum extracted characters per web page when including contents.</summary>
    public double? ArticleContentMaxChars { get; init; }

    /// <summary>Keep results published on or after this ISO date.</summary>
    public string? PublishedAfter { get; init; }

    /// <summary>Keep results published on or before this ISO date.</summary>
    public string? PublishedBefore { get; init; }
}

/// <summary>Upstream's <c>TakoSearchConfig.sources</c>.</summary>
public sealed class TakoSearchConfigSources
{
    /// <summary>Curated data source settings.</summary>
    public TakoDataSourceConfig? Data { get; init; }

    /// <summary>Web source settings.</summary>
    public TakoWebSourceConfig? Web { get; init; }
}

/// <summary>A pair of end-user coordinates (<c>TakoSearchConfig.location</c> and <c>TakoSearchInput.location</c>).</summary>
public sealed class TakoLocation
{
    /// <summary>Latitude between -90 and 90.</summary>
    [JsonPropertyName("latitude")]
    public required double Latitude { get; init; }

    /// <summary>Longitude between -180 and 180.</summary>
    [JsonPropertyName("longitude")]
    public required double Longitude { get; init; }
}

/// <summary>Upstream's <c>TakoSearchConfig.outputSettings</c>.</summary>
public sealed class TakoSearchConfigOutputSettings
{
    /// <summary>Render card preview images in dark mode.</summary>
    public bool? ImageDarkMode { get; init; }

    /// <summary>Instant-effort only.</summary>
    public bool? ForceRefresh { get; init; }
}

/// <summary>Upstream's <c>TakoSearchConfig</c>.</summary>
public sealed class TakoSearchConfig
{
    /// <summary>Search effort. fast is the default balanced option.</summary>
    public TakoSearchEffort? Effort { get; init; }

    /// <summary>Sources to search. Omit to search both curated data and the web.</summary>
    public TakoSearchConfigSources? Sources { get; init; }

    /// <summary>End-user coordinates for localized results.</summary>
    public TakoLocation? Location { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public string? CountryCode { get; init; }

    /// <summary>BCP-47 locale.</summary>
    public string? Locale { get; init; }

    /// <summary>IANA timezone.</summary>
    public string? Timezone { get; init; }

    /// <summary>Settings that control card rendering.</summary>
    public TakoSearchConfigOutputSettings? OutputSettings { get; init; }

    /// <summary>Maximum related search suggestions to include (1-20).</summary>
    public double? IncludeRelated { get; init; }

    /// <summary>
    /// Serve per-unit-priced data, such as web traffic and SEO. Each such card adds a charge, capped by maxVariableCostUsd. On by
    /// default; set false to opt out, and those cards come back as placeholders.
    /// </summary>
    public bool? VariableCost { get; init; }

    /// <summary>
    /// Most one call can be charged in variable cost, in USD. Defaults to 0.25; <see cref="Undefinable{T}.Null"/> removes the cap. Cards
    /// that don't fit come back as placeholders with reason <c>over_cap</c>. Can't be combined with <c>VariableCost = false</c>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Undefinable<double> MaxVariableCostUsd { get; init; }

    /// <summary>
    /// Extra Tako request fields in snake_case, deep-merged into the body last. Can't set fields this tool models; Tako rejects unknown
    /// keys (api_error).
    /// </summary>
    public Dictionary<string, JsonNode?>? ExtraBody { get; init; }
}

/// <summary>Upstream's <c>TakoSearchInput.sources.data</c>.</summary>
public sealed class TakoSearchInputDataSource
{
    /// <summary>Maximum number of data results to return (1-20).</summary>
    [JsonPropertyName("count")]
    public double? Count { get; init; }

    /// <summary>Inline rows for each data result.</summary>
    [JsonPropertyName("include_contents")]
    public bool? IncludeContents { get; init; }

    /// <summary>Requested data delivery mode.</summary>
    [JsonPropertyName("mode")]
    public TakoDataMode? Mode { get; init; }

    /// <summary>Serialization for inlined card data.</summary>
    [JsonPropertyName("content_format")]
    public TakoContentFormat? ContentFormat { get; init; }

    /// <summary>Maximum rows to inline per result.</summary>
    [JsonPropertyName("max_rows")]
    public double? MaxRows { get; init; }

    /// <summary>Data Graph node IDs to prioritize.</summary>
    [JsonPropertyName("node_ids")]
    public List<string>? NodeIds { get; init; }

    /// <summary>Only return cards matching node_ids.</summary>
    [JsonPropertyName("strict")]
    public bool? Strict { get; init; }
}

/// <summary>Upstream's <c>TakoSearchInput.sources.web</c>.</summary>
public sealed class TakoSearchInputWebSource
{
    /// <summary>Maximum number of web results to return (1-20).</summary>
    [JsonPropertyName("count")]
    public double? Count { get; init; }

    /// <summary>Inline extracted web page text.</summary>
    [JsonPropertyName("include_contents")]
    public bool? IncludeContents { get; init; }

    /// <summary>Web-result category filter.</summary>
    [JsonPropertyName("category")]
    public TakoWebCategory? Category { get; init; }

    /// <summary>Only return results from these bare domains.</summary>
    [JsonPropertyName("include_domains")]
    public List<string>? IncludeDomains { get; init; }

    /// <summary>Exclude results from these bare domains.</summary>
    [JsonPropertyName("exclude_domains")]
    public List<string>? ExcludeDomains { get; init; }

    /// <summary>Maximum characters in each web-result snippet.</summary>
    [JsonPropertyName("snippet_max_chars")]
    public double? SnippetMaxChars { get; init; }

    /// <summary>Include highlighted passages in web results.</summary>
    [JsonPropertyName("highlights")]
    public bool? Highlights { get; init; }

    /// <summary>Maximum extracted characters per web page.</summary>
    [JsonPropertyName("article_content_max_chars")]
    public double? ArticleContentMaxChars { get; init; }

    /// <summary>Keep results published on or after this ISO date (YYYY-MM-DD).</summary>
    [JsonPropertyName("published_after")]
    public string? PublishedAfter { get; init; }

    /// <summary>Keep results published on or before this ISO date (YYYY-MM-DD).</summary>
    [JsonPropertyName("published_before")]
    public string? PublishedBefore { get; init; }
}

/// <summary>Upstream's <c>TakoSearchInput.sources</c>.</summary>
public sealed class TakoSearchInputSources
{
    /// <summary>Curated data source settings.</summary>
    [JsonPropertyName("data")]
    public TakoSearchInputDataSource? Data { get; init; }

    /// <summary>Web source settings.</summary>
    [JsonPropertyName("web")]
    public TakoSearchInputWebSource? Web { get; init; }
}

/// <summary>Upstream's <c>TakoSearchInput.output_settings</c>.</summary>
public sealed class TakoSearchInputOutputSettings
{
    /// <summary>Render card preview images in dark mode.</summary>
    [JsonPropertyName("image_dark_mode")]
    public bool? ImageDarkMode { get; init; }

    /// <summary>Instant-effort only. Request a refreshed instant result.</summary>
    [JsonPropertyName("force_refresh")]
    public bool? ForceRefresh { get; init; }
}

/// <summary>Upstream's <c>TakoSearchInput</c>.</summary>
public sealed class TakoSearchInput
{
    /// <summary>Natural-language search query.</summary>
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    /// <summary>Search effort.</summary>
    [JsonPropertyName("effort")]
    public TakoSearchEffort? Effort { get; init; }

    /// <summary>Sources to search.</summary>
    [JsonPropertyName("sources")]
    public TakoSearchInputSources? Sources { get; init; }

    /// <summary>End-user coordinates for localized results.</summary>
    [JsonPropertyName("location")]
    public TakoLocation? Location { get; init; }

    /// <summary>Two-letter ISO 3166-1 country code.</summary>
    [JsonPropertyName("country_code")]
    public string? CountryCode { get; init; }

    /// <summary>BCP-47 locale.</summary>
    [JsonPropertyName("locale")]
    public string? Locale { get; init; }

    /// <summary>IANA timezone.</summary>
    [JsonPropertyName("timezone")]
    public string? Timezone { get; init; }

    /// <summary>Controls card rendering in the search response.</summary>
    [JsonPropertyName("output_settings")]
    public TakoSearchInputOutputSettings? OutputSettings { get; init; }

    /// <summary>Maximum related search suggestions to include (1-20).</summary>
    [JsonPropertyName("include_related")]
    public double? IncludeRelated { get; init; }
}

/// <summary>Upstream's <c>TakoResultContent.dataset.columns</c> item. (<c>TakoDatasetCell</c>, <c>boolean | number | string | null</c>, is a <see cref="JsonNode"/> here.)</summary>
public sealed class TakoDatasetColumn
{
    /// <summary>The column name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>One of <c>boolean</c>, <c>date</c>, <c>datetime</c>, <c>number</c>, <c>string</c>.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>The unit.</summary>
    [JsonPropertyName("unit")]
    public string? Unit { get; init; }
}

/// <summary>Upstream's <c>TakoResultContent.dataset.sources</c> item.</summary>
public sealed class TakoDatasetSource
{
    /// <summary>The source name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary><c>data</c> or <c>web</c>.</summary>
    [JsonPropertyName("index")]
    public string? Index { get; init; }
}

/// <summary>Upstream's <c>TakoResultContent.dataset</c>.</summary>
public sealed class TakoDataset
{
    /// <summary>The columns.</summary>
    [JsonPropertyName("columns")]
    public required List<TakoDatasetColumn> Columns { get; init; }

    /// <summary>The rows. Every cell is a boolean, number, string or null (<c>TakoDatasetCell</c>).</summary>
    [JsonPropertyName("rows")]
    public required List<List<JsonNode?>> Rows { get; init; }

    /// <summary>The total number of rows.</summary>
    [JsonPropertyName("total_rows")]
    public required double TotalRows { get; init; }

    /// <summary>Whether the rows are cut short.</summary>
    [JsonPropertyName("truncated")]
    public required bool Truncated { get; init; }

    /// <summary>The dataset reference.</summary>
    [JsonPropertyName("ref")]
    public required string Ref { get; init; }

    /// <summary>The sources.</summary>
    [JsonPropertyName("sources")]
    public required List<TakoDatasetSource> Sources { get; init; }

    /// <summary><c>query</c> or <c>web_extraction</c>.</summary>
    [JsonPropertyName("provenance")]
    public string? Provenance { get; init; }
}

/// <summary>Upstream's <c>TakoResultContent.export_pricing</c>.</summary>
public sealed class TakoExportPricing
{
    /// <summary>The baseline price in USD.</summary>
    [JsonPropertyName("baseline_usd")]
    public required double BaselineUsd { get; init; }

    /// <summary>Always 0, and Tako will stop sending it. Deprecated.</summary>
    [JsonPropertyName("free_rows")]
    public double? FreeRows { get; init; }

    /// <summary>The ceiling of rows.</summary>
    [JsonPropertyName("max_rows_ceiling")]
    public required double MaxRowsCeiling { get; init; }

    /// <summary>USD per 1,000 rows.</summary>
    [JsonPropertyName("row_cpm_usd")]
    public required double RowCpmUsd { get; init; }

    /// <summary>USD per 1,000 premium records by class, added to <c>row_cpm_usd</c> when premium.</summary>
    [JsonPropertyName("premium_row_cpm_usd")]
    public Dictionary<string, double>? PremiumRowCpmUsd { get; init; }

    /// <summary>Upper bound on this card's variable-cost charge; set only for per-unit-priced sources.</summary>
    [JsonPropertyName("variable_cost_max_usd")]
    public double? VariableCostMaxUsd { get; init; }

    /// <summary>Fields Tako adds later (the schema keeps them).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

/// <summary>Upstream's <c>TakoResultContent.manifest</c> item.</summary>
public sealed class TakoManifestEntry
{
    /// <summary>One of <c>boolean</c>, <c>date</c>, <c>datetime</c>, <c>number</c>, <c>string</c>.</summary>
    [JsonPropertyName("dtype")]
    public string? Dtype { get; init; }

    /// <summary>The entity.</summary>
    [JsonPropertyName("entity")]
    public string? Entity { get; init; }

    /// <summary>The metric.</summary>
    [JsonPropertyName("metric")]
    public string? Metric { get; init; }

    /// <summary>The name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The unit.</summary>
    [JsonPropertyName("unit")]
    public string? Unit { get; init; }
}

/// <summary>Upstream's <c>TakoResultContent</c>.</summary>
public sealed class TakoResultContent
{
    /// <summary>The content format.</summary>
    [JsonPropertyName("content_format")]
    public TakoContentFormat? ContentFormat { get; init; }

    /// <summary>The data cost.</summary>
    [JsonPropertyName("cost")]
    public double? Cost { get; init; }

    /// <summary>The serialized data.</summary>
    [JsonPropertyName("data")]
    public string? Data { get; init; }

    /// <summary>The records. Every cell is a boolean, number, string or null (<c>TakoDatasetCell</c>).</summary>
    [JsonPropertyName("records")]
    public List<Dictionary<string, JsonNode?>>? Records { get; init; }

    /// <summary>The dataset.</summary>
    [JsonPropertyName("dataset")]
    public TakoDataset? Dataset { get; init; }

    /// <summary>The card data.</summary>
    [JsonPropertyName("card_data")]
    public Dictionary<string, JsonNode?>? CardData { get; init; }

    /// <summary>The card data schema.</summary>
    [JsonPropertyName("card_data_schema")]
    public Dictionary<string, JsonNode?>? CardDataSchema { get; init; }

    /// <summary>The URL of the data.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    /// <summary>When the URL expires.</summary>
    [JsonPropertyName("expires_at")]
    public string? ExpiresAt { get; init; }

    /// <summary>The total number of rows.</summary>
    [JsonPropertyName("total_rows")]
    public double? TotalRows { get; init; }

    /// <summary>Whether the rows are cut short.</summary>
    [JsonPropertyName("truncated")]
    public bool? Truncated { get; init; }

    /// <summary>Distinct records per premium class (e.g. company_record) among delivered rows.</summary>
    [JsonPropertyName("premium_rows")]
    public Dictionary<string, double>? PremiumRows { get; init; }

    /// <summary>Column header to 0-based delivered-row positions whose premium value was withheld.</summary>
    [JsonPropertyName("redacted_cells")]
    public Dictionary<string, List<double>>? RedactedCells { get; init; }

    /// <summary>Full set size when the card lists only a capped slice of it.</summary>
    [JsonPropertyName("total_matching")]
    public double? TotalMatching { get; init; }

    /// <summary>The export pricing.</summary>
    [JsonPropertyName("export_pricing")]
    public TakoExportPricing? ExportPricing { get; init; }

    /// <summary>The manifest.</summary>
    [JsonPropertyName("manifest")]
    public List<TakoManifestEntry>? Manifest { get; init; }

    /// <summary>Fields Tako adds later (the schema keeps them).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

/// <summary>Upstream's <c>TakoCard.sources</c> item.</summary>
public sealed class TakoCardSource
{
    /// <summary>The source name.</summary>
    [JsonPropertyName("source_name")]
    public string? SourceName { get; init; }

    /// <summary>The source description.</summary>
    [JsonPropertyName("source_description")]
    public string? SourceDescription { get; init; }

    /// <summary><c>data</c> or <c>web</c>.</summary>
    [JsonPropertyName("source_index")]
    public required string SourceIndex { get; init; }

    /// <summary>The source text.</summary>
    [JsonPropertyName("source_text")]
    public string? SourceText { get; init; }

    /// <summary>The source URL.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }
}

/// <summary>Upstream's <c>TakoCard.methodologies</c> item.</summary>
public sealed class TakoMethodology
{
    /// <summary>The methodology name.</summary>
    [JsonPropertyName("methodology_name")]
    public string? MethodologyName { get; init; }

    /// <summary>The methodology description.</summary>
    [JsonPropertyName("methodology_description")]
    public string? MethodologyDescription { get; init; }
}

/// <summary>Upstream's <c>TakoCard.nodes</c> item.</summary>
public sealed class TakoCardNode
{
    /// <summary>The node ID.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary><c>entity</c> or <c>metric</c>.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>The node name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>The node description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>Upstream's <c>TakoCard.metric_definitions</c> item.</summary>
public sealed class TakoMetricDefinition
{
    /// <summary>The metric name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>The metric definition.</summary>
    [JsonPropertyName("definition")]
    public required string Definition { get; init; }
}

/// <summary>Upstream's <c>TakoCard.data_freshness</c>.</summary>
public sealed class TakoDataFreshness
{
    /// <summary>The end of the data coverage.</summary>
    [JsonPropertyName("coverage_end")]
    public string? CoverageEnd { get; init; }

    /// <summary>When the data was current.</summary>
    [JsonPropertyName("data_as_of")]
    public string? DataAsOf { get; init; }

    /// <summary>When the data was last updated.</summary>
    [JsonPropertyName("last_updated")]
    public string? LastUpdated { get; init; }
}

/// <summary>
/// Upstream's <c>TakoCard.placeholder</c>: set when Tako withholds the card's data: variable cost off or not served (<c>not_served</c>),
/// or over maxVariableCostUsd (<c>over_cap</c>).
/// </summary>
public sealed class TakoCardPlaceholder
{
    /// <summary>The withheld source.</summary>
    [JsonPropertyName("withheld_source")]
    public required string WithheldSource { get; init; }

    /// <summary><c>not_served</c> or <c>over_cap</c>.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    /// <summary>Fields Tako adds later (the schema keeps them).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

/// <summary>Upstream's <c>TakoCard</c>.</summary>
public sealed class TakoCard
{
    /// <summary>The card ID.</summary>
    [JsonPropertyName("card_id")]
    public string? CardId { get; init; }

    /// <summary>The title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>The description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>The semantic description.</summary>
    [JsonPropertyName("semantic_description")]
    public string? SemanticDescription { get; init; }

    /// <summary>The webpage URL.</summary>
    [JsonPropertyName("webpage_url")]
    public string? WebpageUrl { get; init; }

    /// <summary>The image URL.</summary>
    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; init; }

    /// <summary>The embed URL.</summary>
    [JsonPropertyName("embed_url")]
    public string? EmbedUrl { get; init; }

    /// <summary>The sources.</summary>
    [JsonPropertyName("sources")]
    public List<TakoCardSource>? Sources { get; init; }

    /// <summary>The methodologies.</summary>
    [JsonPropertyName("methodologies")]
    public List<TakoMethodology>? Methodologies { get; init; }

    /// <summary>The source indexes (<c>data</c> or <c>web</c>).</summary>
    [JsonPropertyName("source_indexes")]
    public List<string>? SourceIndexes { get; init; }

    /// <summary>The card type.</summary>
    [JsonPropertyName("card_type")]
    public string? CardType { get; init; }

    /// <summary><c>High</c>, <c>Low</c> or <c>Medium</c>.</summary>
    [JsonPropertyName("relevance")]
    public string? Relevance { get; init; }

    /// <summary>Relevance to the query, 1.0-5.0. Null unless the account is entitled.</summary>
    [JsonPropertyName("relevance_score")]
    public double? RelevanceScore { get; init; }

    /// <summary>The content.</summary>
    [JsonPropertyName("content")]
    public TakoResultContent? Content { get; init; }

    /// <summary>Whether the card is exportable.</summary>
    [JsonPropertyName("exportable")]
    public bool? Exportable { get; init; }

    /// <summary>The nodes.</summary>
    [JsonPropertyName("nodes")]
    public List<TakoCardNode>? Nodes { get; init; }

    /// <summary>The metric definitions.</summary>
    [JsonPropertyName("metric_definitions")]
    public List<TakoMetricDefinition>? MetricDefinitions { get; init; }

    /// <summary>The data freshness.</summary>
    [JsonPropertyName("data_freshness")]
    public TakoDataFreshness? DataFreshness { get; init; }

    /// <summary>Set when Tako withholds the card's data.</summary>
    [JsonPropertyName("placeholder")]
    public TakoCardPlaceholder? Placeholder { get; init; }

    /// <summary>Fields Tako adds later (the schema keeps them).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

/// <summary>Upstream's <c>TakoWebResult</c>.</summary>
public sealed class TakoWebResult
{
    /// <summary>The title.</summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>The URL.</summary>
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    /// <summary>The snippet.</summary>
    [JsonPropertyName("snippet")]
    public string? Snippet { get; init; }

    /// <summary>The source name.</summary>
    [JsonPropertyName("source_name")]
    public string? SourceName { get; init; }

    /// <summary>The publication date.</summary>
    [JsonPropertyName("publish_date")]
    public string? PublishDate { get; init; }

    /// <summary>The content.</summary>
    [JsonPropertyName("content")]
    public TakoResultContent? Content { get; init; }

    /// <summary>Fields Tako adds later (the schema keeps them).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

/// <summary>Upstream's <c>TakoSearchResponse.usage.compute</c> and <c>.variable_cost</c>.</summary>
public sealed class TakoUsageCost
{
    /// <summary>The cost in USD.</summary>
    [JsonPropertyName("cost_usd")]
    public required double CostUsd { get; init; }

    /// <summary>Fields Tako adds later (the schema keeps them).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

/// <summary>Upstream's <c>TakoSearchResponse.usage.data</c>.</summary>
public sealed class TakoUsageData
{
    /// <summary>The cost in USD.</summary>
    [JsonPropertyName("cost_usd")]
    public required double CostUsd { get; init; }

    /// <summary>The number of datasets.</summary>
    [JsonPropertyName("datasets")]
    public required double Datasets { get; init; }

    /// <summary>The number of data points.</summary>
    [JsonPropertyName("data_points")]
    public double? DataPoints { get; init; }

    /// <summary>Fields Tako adds later (the schema keeps them).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

/// <summary>Upstream's <c>TakoSearchResponse.usage</c>.</summary>
public sealed class TakoUsage
{
    /// <summary>The total cost in USD.</summary>
    [JsonPropertyName("total_cost_usd")]
    public required double TotalCostUsd { get; init; }

    /// <summary>The compute cost.</summary>
    [JsonPropertyName("compute")]
    public TakoUsageCost? Compute { get; init; }

    /// <summary>The data cost.</summary>
    [JsonPropertyName("data")]
    public TakoUsageData? Data { get; init; }

    /// <summary>The variable cost.</summary>
    [JsonPropertyName("variable_cost")]
    public TakoUsageCost? VariableCost { get; init; }

    /// <summary>Fields Tako adds later (the schema keeps them).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

/// <summary>Upstream's <c>TakoSearchOutput</c>: a <see cref="TakoSearchResponse"/> or a <see cref="GatewayToolError"/> (<c>TakoSearchError</c>).</summary>
public interface ITakoSearchOutput { }

/// <summary>Upstream's <c>TakoSearchResponse</c>.</summary>
public sealed class TakoSearchResponse : ITakoSearchOutput
{
    /// <summary>The request ID.</summary>
    [JsonPropertyName("request_id")]
    public required string RequestId { get; init; }

    /// <summary>The cards.</summary>
    [JsonPropertyName("cards")]
    public List<TakoCard>? Cards { get; init; }

    /// <summary>The web results.</summary>
    [JsonPropertyName("web_results")]
    public List<TakoWebResult>? WebResults { get; init; }

    /// <summary>The usage.</summary>
    [JsonPropertyName("usage")]
    public TakoUsage? Usage { get; init; }

    /// <summary>The related searches.</summary>
    [JsonPropertyName("related")]
    public List<Dictionary<string, JsonNode?>>? Related { get; init; }

    /// <summary>Fields Tako adds later (the schema keeps them).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

/// <summary>
/// The tool of <c>tool/tako-search.ts</c>. The class name is .NET-only: C# has no module-level functions, so <c>takoSearch</c>,
/// <c>takoSearchToolFactory</c> and <c>takoResultContentSchema</c> are members of it.
/// </summary>
public static class TakoSearchTool
{
    private static readonly LazySchema s_inputSchema = SchemaUtils.LazySchema(() =>
        SchemaUtils.ZodSchema(
            Zod.Object(
                (
                    "query",
                    Zod.Describe(
                        Zod.String(),
                        "Natural-language search query. Include the entity, metric, and time period. Quote a phrase to force it to one entity, for example \"Tesla\":PRODUCT price."
                    )
                ),
                (
                    "effort",
                    Zod.Describe(
                        Zod.Optional(Zod.Enum("deep", "fast", "instant")),
                        "Search effort. fast is the balanced default, instant favors cached results and low latency, and deep broadens retrieval with reranking at higher cost and latency."
                    )
                ),
                (
                    "sources",
                    Zod.Describe(
                        Zod.Optional(
                            Zod.Object(
                                ("data", Zod.Optional(TakoDataSourceInputSchema())),
                                ("web", Zod.Optional(TakoWebSourceInputSchema()))
                            )
                        ),
                        "Sources to search. Omit to search both curated data and the web. When provided, only keys present are searched."
                    )
                ),
                (
                    "location",
                    Zod.Describe(
                        Zod.Optional(
                            Zod.Object(
                                ("latitude", Zod.Describe(Zod.Number(), "Latitude between -90 and 90.")),
                                ("longitude", Zod.Describe(Zod.Number(), "Longitude between -180 and 180."))
                            )
                        ),
                        "End-user coordinates for localized results."
                    )
                ),
                (
                    "country_code",
                    Zod.Describe(Zod.Optional(Zod.String()), "Two-letter ISO 3166-1 country code, such as 'US'.")
                ),
                ("locale", Zod.Describe(Zod.Optional(Zod.String()), "BCP-47 locale, such as 'en-US'.")),
                ("timezone", Zod.Describe(Zod.Optional(Zod.String()), "IANA timezone, such as 'America/New_York'.")),
                (
                    "output_settings",
                    Zod.Describe(
                        Zod.Optional(
                            Zod.Object(
                                (
                                    "image_dark_mode",
                                    Zod.Describe(
                                        Zod.Optional(Zod.Boolean()),
                                        "Render card preview images in dark mode."
                                    )
                                ),
                                (
                                    "force_refresh",
                                    Zod.Describe(
                                        Zod.Optional(Zod.Boolean()),
                                        "Instant-effort only. Request a refreshed instant result."
                                    )
                                )
                            )
                        ),
                        "Controls card rendering in the search response."
                    )
                ),
                (
                    "include_related",
                    Zod.Describe(Zod.Optional(Zod.Number()), "Maximum related search suggestions to include (1-20).")
                )
            )
        )
    );

    private static readonly LazySchema s_outputSchema = SchemaUtils.LazySchema(() =>
        SchemaUtils.ZodSchema(
            Zod.Union(
                Zod.Catchall(
                    Zod.Object(
                        ("request_id", Zod.String()),
                        ("cards", Zod.Optional(Zod.Array(TakoCardSchema()))),
                        ("web_results", Zod.Optional(Zod.Array(TakoWebResultSchema()))),
                        (
                            "usage",
                            Zod.Nullish(
                                Zod.Catchall(
                                    Zod.Object(
                                        ("total_cost_usd", Zod.Number()),
                                        (
                                            "compute",
                                            Zod.Nullish(
                                                Zod.Catchall(Zod.Object(("cost_usd", Zod.Number())), Zod.Unknown())
                                            )
                                        ),
                                        (
                                            "data",
                                            Zod.Nullish(
                                                Zod.Catchall(
                                                    Zod.Object(
                                                        ("cost_usd", Zod.Number()),
                                                        ("datasets", Zod.Number()),
                                                        ("data_points", Zod.Nullish(Zod.Number()))
                                                    ),
                                                    Zod.Unknown()
                                                )
                                            )
                                        ),
                                        (
                                            "variable_cost",
                                            Zod.Nullish(
                                                Zod.Catchall(Zod.Object(("cost_usd", Zod.Number())), Zod.Unknown())
                                            )
                                        )
                                    ),
                                    Zod.Unknown()
                                )
                            )
                        ),
                        ("related", Zod.Nullish(Zod.Array(Zod.Catchall(Zod.Object(), Zod.Unknown()))))
                    ),
                    Zod.Unknown()
                ),
                GatewayToolErrors.GatewayToolErrorSchema()
            )
        )
    );

    /// <summary>Upstream's <c>takoSearchToolFactory</c>.</summary>
    public static Func<
        TakoSearchConfig,
        ProviderExecutedTool<TakoSearchInput, ITakoSearchOutput>
    > TakoSearchToolFactory { get; } =
        ProviderExecutedToolFactory.CreateProviderExecutedToolFactory<
            TakoSearchInput,
            ITakoSearchOutput,
            TakoSearchConfig
        >("gateway.tako_search", s_inputSchema, s_outputSchema);

    /// <summary>
    /// Upstream's <c>takoSearch</c>: searches the web and Tako's curated knowledge graph in one call for token-efficient web excerpts and
    /// structured data results grounded in premium sources, each with an embed-ready visualization. Supports effort, per-source web and
    /// data controls, localization, and inline contents for agents that need to reason over underlying data.
    /// </summary>
    public static ProviderExecutedTool<TakoSearchInput, ITakoSearchOutput> TakoSearch(
        TakoSearchConfig? config = null
    ) => TakoSearchToolFactory(config ?? new TakoSearchConfig());

    /// <summary>Upstream's <c>takoResultContentSchema</c>, exported for reuse.</summary>
    public static ZodType TakoResultContentSchema() =>
        Zod.Catchall(
            Zod.Object(
                ("content_format", Zod.Nullish(Zod.Enum("card_json", "csv", "json_compact", "json_records"))),
                ("cost", Zod.Optional(Zod.Number())),
                ("data", Zod.Nullish(Zod.String())),
                ("records", Zod.Nullish(Zod.Array(Zod.Record(TakoDatasetCellSchema())))),
                (
                    "dataset",
                    Zod.Nullish(
                        Zod.Object(
                            (
                                "columns",
                                Zod.Array(
                                    Zod.Object(
                                        ("name", Zod.String()),
                                        ("type", Zod.Enum("boolean", "date", "datetime", "number", "string")),
                                        ("unit", Zod.Nullish(Zod.String()))
                                    )
                                )
                            ),
                            ("rows", Zod.Array(Zod.Array(TakoDatasetCellSchema()))),
                            ("total_rows", Zod.Number()),
                            ("truncated", Zod.Boolean()),
                            ("ref", Zod.String()),
                            (
                                "sources",
                                Zod.Array(
                                    Zod.Object(("name", Zod.String()), ("index", Zod.Optional(Zod.Enum("data", "web"))))
                                )
                            ),
                            ("provenance", Zod.Optional(Zod.Enum("query", "web_extraction")))
                        )
                    )
                ),
                ("card_data", Zod.Nullish(Zod.Catchall(Zod.Object(), Zod.Unknown()))),
                ("card_data_schema", Zod.Nullish(Zod.Catchall(Zod.Object(), Zod.Unknown()))),
                ("url", Zod.Nullish(Zod.String())),
                ("expires_at", Zod.Nullish(Zod.String())),
                ("total_rows", Zod.Nullish(Zod.Number())),
                ("truncated", Zod.Optional(Zod.Boolean())),
                ("premium_rows", Zod.Nullish(Zod.Record(Zod.Number()))),
                ("redacted_cells", Zod.Nullish(Zod.Record(Zod.Array(Zod.Number())))),
                ("total_matching", Zod.Nullish(Zod.Number())),
                (
                    "export_pricing",
                    Zod.Nullish(
                        Zod.Catchall(
                            Zod.Object(
                                ("baseline_usd", Zod.Number()),
                                ("free_rows", Zod.Optional(Zod.Number())),
                                ("max_rows_ceiling", Zod.Number()),
                                ("row_cpm_usd", Zod.Number()),
                                ("premium_row_cpm_usd", Zod.Nullish(Zod.Record(Zod.Number()))),
                                ("variable_cost_max_usd", Zod.Nullish(Zod.Number()))
                            ),
                            Zod.Unknown()
                        )
                    )
                ),
                (
                    "manifest",
                    Zod.Nullish(
                        Zod.Array(
                            Zod.Object(
                                ("dtype", Zod.Nullish(Zod.Enum("boolean", "date", "datetime", "number", "string"))),
                                ("entity", Zod.Nullish(Zod.String())),
                                ("metric", Zod.Nullish(Zod.String())),
                                ("name", Zod.Nullish(Zod.String())),
                                ("unit", Zod.Nullish(Zod.String()))
                            )
                        )
                    )
                )
            ),
            Zod.Unknown()
        );

    private static ZodType TakoDataSourceInputSchema() =>
        Zod.Object(
            (
                "count",
                Zod.Describe(
                    Zod.Optional(Zod.Number()),
                    "Maximum number of data results to return (1-20). When include_contents is true, each additional result adds its own data surcharge."
                )
            ),
            (
                "include_contents",
                Zod.Describe(
                    Zod.Optional(Zod.Boolean()),
                    "Inline rows for each data result. This adds a data surcharge based on row count and dataset source. To estimate cost, search with include_contents disabled and inspect cards.content.export_pricing. This applies to every returned card; limit sources.data.count and sources.data.max_rows to control cost."
                )
            ),
            (
                "mode",
                Zod.Describe(
                    Zod.Optional(Zod.Enum("inline", "url")),
                    "Requested data delivery mode. Search card data is always inline."
                )
            ),
            (
                "content_format",
                Zod.Describe(
                    Zod.Optional(Zod.Enum("card_json", "csv", "json_compact", "json_records")),
                    "Serialization for inlined card data."
                )
            ),
            (
                "max_rows",
                Zod.Describe(
                    Zod.Optional(Zod.Number()),
                    "Maximum rows to inline per result. Omit to use the allowance in cards.content.export_pricing. A data surcharge applies per 1,000 exported rows; lower values reduce cost."
                )
            ),
            (
                "node_ids",
                Zod.Describe(Zod.Optional(Zod.Array(Zod.String())), "Data Graph node IDs to prioritize. Maximum 20.")
            ),
            (
                "strict",
                Zod.Describe(
                    Zod.Optional(Zod.Boolean()),
                    "Only return cards matching node_ids. Requires a non-empty node_ids."
                )
            )
        );

    private static ZodType TakoWebSourceInputSchema() =>
        Zod.Object(
            ("count", Zod.Describe(Zod.Optional(Zod.Number()), "Maximum number of web results to return (1-20).")),
            (
                "include_contents",
                Zod.Describe(Zod.Optional(Zod.Boolean()), "Inline extracted web page text. This can add a data charge.")
            ),
            (
                "category",
                Zod.Describe(
                    Zod.Optional(Zod.Enum("finance", "news", "sports")),
                    "Optional web-result category filter."
                )
            ),
            (
                "include_domains",
                Zod.Describe(Zod.Optional(Zod.Array(Zod.String())), "Only return results from these bare domains.")
            ),
            (
                "exclude_domains",
                Zod.Describe(Zod.Optional(Zod.Array(Zod.String())), "Exclude results from these bare domains.")
            ),
            (
                "snippet_max_chars",
                Zod.Describe(Zod.Optional(Zod.Number()), "Maximum characters in each web-result snippet.")
            ),
            (
                "highlights",
                Zod.Describe(
                    Zod.Optional(Zod.Boolean()),
                    "Include highlighted passages in web results. Defaults to true in AI Gateway."
                )
            ),
            (
                "article_content_max_chars",
                Zod.Describe(
                    Zod.Optional(Zod.Number()),
                    "Maximum extracted characters per web page when including contents."
                )
            ),
            (
                "published_after",
                Zod.Describe(
                    Zod.Optional(Zod.String()),
                    "Keep results published on or after this ISO date (YYYY-MM-DD)."
                )
            ),
            (
                "published_before",
                Zod.Describe(
                    Zod.Optional(Zod.String()),
                    "Keep results published on or before this ISO date (YYYY-MM-DD)."
                )
            )
        );

    private static ZodType TakoDatasetCellSchema() =>
        Zod.Nullable(Zod.Union(Zod.Boolean(), Zod.Number(), Zod.String()));

    private static ZodType TakoCardSchema() =>
        Zod.Catchall(
            Zod.Object(
                ("card_id", Zod.Nullish(Zod.String())),
                ("title", Zod.Nullish(Zod.String())),
                ("description", Zod.Nullish(Zod.String())),
                ("semantic_description", Zod.Nullish(Zod.String())),
                ("webpage_url", Zod.Nullish(Zod.String())),
                ("image_url", Zod.Nullish(Zod.String())),
                ("embed_url", Zod.Nullish(Zod.String())),
                (
                    "sources",
                    Zod.Nullish(
                        Zod.Array(
                            Zod.Object(
                                ("source_name", Zod.Nullish(Zod.String())),
                                ("source_description", Zod.Nullish(Zod.String())),
                                ("source_index", Zod.Enum("data", "web")),
                                ("source_text", Zod.Nullish(Zod.String())),
                                ("url", Zod.Nullish(Zod.String()))
                            )
                        )
                    )
                ),
                (
                    "methodologies",
                    Zod.Nullish(
                        Zod.Array(
                            Zod.Object(
                                ("methodology_name", Zod.Nullable(Zod.String())),
                                ("methodology_description", Zod.Nullable(Zod.String()))
                            )
                        )
                    )
                ),
                ("source_indexes", Zod.Nullish(Zod.Array(Zod.Enum("data", "web")))),
                ("card_type", Zod.Nullish(Zod.String())),
                ("relevance", Zod.Nullish(Zod.Enum("High", "Low", "Medium"))),
                ("relevance_score", Zod.Nullish(Zod.Number())),
                ("content", Zod.Nullish(TakoResultContentSchema())),
                ("exportable", Zod.Optional(Zod.Boolean())),
                (
                    "nodes",
                    Zod.Nullish(
                        Zod.Array(
                            Zod.Object(
                                ("id", Zod.String()),
                                ("type", Zod.Enum("entity", "metric")),
                                ("name", Zod.String()),
                                ("description", Zod.Nullish(Zod.String()))
                            )
                        )
                    )
                ),
                (
                    "metric_definitions",
                    Zod.Nullish(Zod.Array(Zod.Object(("name", Zod.String()), ("definition", Zod.String()))))
                ),
                (
                    "data_freshness",
                    Zod.Nullish(
                        Zod.Object(
                            ("coverage_end", Zod.Nullish(Zod.String())),
                            ("data_as_of", Zod.Nullish(Zod.String())),
                            ("last_updated", Zod.Nullish(Zod.String()))
                        )
                    )
                ),
                (
                    "placeholder",
                    Zod.Nullish(
                        Zod.Catchall(
                            Zod.Object(
                                ("withheld_source", Zod.String()),
                                ("reason", Zod.Nullish(Zod.Enum("not_served", "over_cap")))
                            ),
                            Zod.Unknown()
                        )
                    )
                )
            ),
            Zod.Unknown()
        );

    private static ZodType TakoWebResultSchema() =>
        Zod.Catchall(
            Zod.Object(
                ("title", Zod.String()),
                ("url", Zod.String()),
                ("snippet", Zod.Nullish(Zod.String())),
                ("source_name", Zod.Nullish(Zod.String())),
                ("publish_date", Zod.Nullish(Zod.String())),
                ("content", Zod.Nullish(TakoResultContentSchema()))
            ),
            Zod.Unknown()
        );
}
