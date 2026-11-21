// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>The <c>error</c> values of the Browserbase and Exa tool errors.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BrowserbaseToolErrorCode>))]
public enum BrowserbaseToolErrorCode
{
    /// <summary><c>api_error</c>.</summary>
    [JsonStringEnumMemberName("api_error")]
    ApiError,

    /// <summary><c>configuration_error</c>.</summary>
    [JsonStringEnumMemberName("configuration_error")]
    ConfigurationError,

    /// <summary><c>execution_error</c>.</summary>
    [JsonStringEnumMemberName("execution_error")]
    ExecutionError,

    /// <summary><c>invalid_input</c>.</summary>
    [JsonStringEnumMemberName("invalid_input")]
    InvalidInput,

    /// <summary><c>rate_limit</c>.</summary>
    [JsonStringEnumMemberName("rate_limit")]
    RateLimit,

    /// <summary><c>timeout</c>.</summary>
    [JsonStringEnumMemberName("timeout")]
    Timeout,

    /// <summary><c>unknown</c>.</summary>
    [JsonStringEnumMemberName("unknown")]
    Unknown,
}

/// <summary>Upstream's <c>BrowserbaseFetchFormat</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BrowserbaseFetchFormat>))]
public enum BrowserbaseFetchFormat
{
    /// <summary><c>raw</c>.</summary>
    [JsonStringEnumMemberName("raw")]
    Raw,

    /// <summary><c>json</c>.</summary>
    [JsonStringEnumMemberName("json")]
    Json,

    /// <summary><c>markdown</c>.</summary>
    [JsonStringEnumMemberName("markdown")]
    Markdown,
}

/// <summary>Upstream's <c>BrowserbaseFetchConfig</c>.</summary>
public sealed class BrowserbaseFetchConfig
{
    /// <summary>Whether to follow HTTP redirects (default: false).</summary>
    public bool? AllowRedirects { get; init; }

    /// <summary>Whether to bypass TLS certificate verification (default: false).</summary>
    public bool? AllowInsecureSsl { get; init; }

    /// <summary>Whether to route the request through Browserbase proxies (default: false).</summary>
    public bool? Proxies { get; init; }

    /// <summary>Output format for the response content (default: raw).</summary>
    public BrowserbaseFetchFormat? Format { get; init; }

    /// <summary>JSON Schema describing the desired response content. Only used when format is json.</summary>
    public Dictionary<string, JsonNode?>? Schema { get; init; }
}

/// <summary>Upstream's <c>BrowserbaseFetchInput</c>.</summary>
public sealed class BrowserbaseFetchInput
{
    /// <summary>URL of the page to fetch.</summary>
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    /// <summary>Whether to follow HTTP redirects.</summary>
    [JsonPropertyName("allow_redirects")]
    public bool? AllowRedirects { get; init; }

    /// <summary>Whether to bypass TLS certificate verification.</summary>
    [JsonPropertyName("allow_insecure_ssl")]
    public bool? AllowInsecureSsl { get; init; }

    /// <summary>Whether to route the request through Browserbase proxies.</summary>
    [JsonPropertyName("proxies")]
    public bool? Proxies { get; init; }

    /// <summary>Output format for the response content.</summary>
    [JsonPropertyName("format")]
    public BrowserbaseFetchFormat? Format { get; init; }

    /// <summary>JSON Schema describing the desired response content. Only used when format is json.</summary>
    [JsonPropertyName("schema")]
    public Dictionary<string, JsonNode?>? Schema { get; init; }
}

/// <summary>Upstream's <c>BrowserbaseFetchOutput</c>: a <see cref="BrowserbaseFetchResponse"/> or a <see cref="BrowserbaseFetchError"/>.</summary>
public interface IBrowserbaseFetchOutput { }

/// <summary>Upstream's <c>BrowserbaseFetchResponse</c>.</summary>
public sealed class BrowserbaseFetchResponse : IBrowserbaseFetchOutput
{
    /// <summary>Unique identifier for the fetch request.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Response body. Raw and markdown responses return strings (a JSON string); JSON extraction returns an object matching the
    /// requested schema.
    /// </summary>
    [JsonPropertyName("content")]
    public required JsonNode Content { get; init; }

    /// <summary>MIME type of the response.</summary>
    [JsonPropertyName("contentType")]
    public required string ContentType { get; init; }

    /// <summary>Character encoding of the response.</summary>
    [JsonPropertyName("encoding")]
    public required string Encoding { get; init; }

    /// <summary>Response headers from the fetched page.</summary>
    [JsonPropertyName("headers")]
    public required Dictionary<string, string> Headers { get; init; }

    /// <summary>HTTP status code returned by the fetched page.</summary>
    [JsonPropertyName("statusCode")]
    public required double StatusCode { get; init; }
}

/// <summary>Upstream's <c>BrowserbaseFetchError</c>.</summary>
public sealed class BrowserbaseFetchError : IBrowserbaseFetchOutput
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
/// The tool of <c>tool/browserbase-fetch.ts</c>. The class name is .NET-only: C# has no module-level functions, so
/// <c>browserbaseFetch</c> and <c>browserbaseFetchToolFactory</c> are members of it.
/// </summary>
public static class BrowserbaseFetchTool
{
    private static readonly LazySchema s_inputSchema = SchemaUtils.LazySchema(() =>
        SchemaUtils.ZodSchema(
            Zod.Object(
                ("url", Zod.Describe(Zod.Url(), "URL of the page to fetch.")),
                (
                    "allow_redirects",
                    Zod.Describe(Zod.Optional(Zod.Boolean()), "Whether to follow HTTP redirects (default: false).")
                ),
                (
                    "allow_insecure_ssl",
                    Zod.Describe(
                        Zod.Optional(Zod.Boolean()),
                        "Whether to bypass TLS certificate verification (default: false). Only use for trusted hosts."
                    )
                ),
                (
                    "proxies",
                    Zod.Describe(
                        Zod.Optional(Zod.Boolean()),
                        "Whether to route the request through Browserbase proxies (default: false)."
                    )
                ),
                (
                    "format",
                    Zod.Describe(
                        Zod.Optional(Zod.Enum("raw", "json", "markdown")),
                        "Output format. raw returns the response body unchanged, markdown returns page content as Markdown, and json returns structured content using schema."
                    )
                ),
                (
                    "schema",
                    Zod.Describe(
                        Zod.Optional(JsonObjectSchema()),
                        "JSON Schema for structured extraction. Only use with format set to json."
                    )
                )
            )
        )
    );

    private static readonly LazySchema s_outputSchema = SchemaUtils.LazySchema(() =>
        SchemaUtils.ZodSchema(
            Zod.Union(
                Zod.Object(
                    ("id", Zod.String()),
                    ("content", Zod.Union(Zod.String(), JsonObjectSchema())),
                    ("contentType", Zod.String()),
                    ("encoding", Zod.String()),
                    ("headers", Zod.Record(Zod.String())),
                    ("statusCode", Zod.Number())
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

    /// <summary>Upstream's <c>browserbaseFetchToolFactory</c>.</summary>
    public static Func<
        BrowserbaseFetchConfig,
        ProviderExecutedTool<BrowserbaseFetchInput, IBrowserbaseFetchOutput>
    > BrowserbaseFetchToolFactory { get; } =
        ProviderExecutedToolFactory.CreateProviderExecutedToolFactory<
            BrowserbaseFetchInput,
            IBrowserbaseFetchOutput,
            BrowserbaseFetchConfig
        >("gateway.browserbase_fetch", s_inputSchema, s_outputSchema);

    /// <summary>
    /// Upstream's <c>browserbaseFetch</c>: fetches page content using Browserbase's lightweight Fetch API. Supports raw, Markdown, and
    /// schema-driven JSON output as well as redirects, proxy routing, and TLS controls.
    /// </summary>
    public static ProviderExecutedTool<BrowserbaseFetchInput, IBrowserbaseFetchOutput> BrowserbaseFetch(
        BrowserbaseFetchConfig? config = null
    ) => BrowserbaseFetchToolFactory(config ?? new BrowserbaseFetchConfig());

    private static ZodType JsonObjectSchema() => Zod.Record(Zod.Unknown());
}
