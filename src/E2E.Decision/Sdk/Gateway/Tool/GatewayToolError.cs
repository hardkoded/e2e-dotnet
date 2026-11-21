// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>The <c>error</c> values of a <see cref="GatewayToolError"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GatewayToolErrorCode>))]
public enum GatewayToolErrorCode
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

    /// <summary><c>gateway_rate_limit</c>.</summary>
    [JsonStringEnumMemberName("gateway_rate_limit")]
    GatewayRateLimit,

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

    /// <summary><c>unknown_tool</c>.</summary>
    [JsonStringEnumMemberName("unknown_tool")]
    UnknownTool,
}

/// <summary>Upstream's <c>GatewayToolError</c> (<c>tool/gateway-tool-error.ts</c>): the structured error a Gateway tool returns.</summary>
public sealed class GatewayToolError
    : IDeepSeekFetchOutput,
        IDeepSeekSearchOutput,
        IParallelSearchOutput,
        ITakoSearchOutput
{
    /// <summary>The kind of error.</summary>
    [JsonPropertyName("error")]
    public required GatewayToolErrorCode Error { get; init; }

    /// <summary>A human-readable message.</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>The HTTP status code, when there is one.</summary>
    [JsonPropertyName("statusCode")]
    public double? StatusCode { get; init; }

    /// <summary>The <c>Retry-After</c> value, when there is one.</summary>
    [JsonPropertyName("retryAfter")]
    public string? RetryAfter { get; init; }

    /// <summary>The HTTP status text, when there is one.</summary>
    [JsonPropertyName("statusText")]
    public string? StatusText { get; init; }

    /// <summary>Fields the Gateway adds later (the schema keeps them: <c>z.catchall(..., z.unknown())</c>).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

/// <summary>Upstream's <c>gatewayToolErrorSchema</c> (<c>tool/gateway-tool-error.ts</c>).</summary>
internal static class GatewayToolErrors
{
    internal static ZodType GatewayToolErrorSchema() =>
        Zod.Catchall(
            Zod.Object(
                (
                    "error",
                    Zod.Enum(
                        "api_error",
                        "configuration_error",
                        "execution_error",
                        "gateway_rate_limit",
                        "invalid_input",
                        "rate_limit",
                        "timeout",
                        "unknown",
                        "unknown_tool"
                    )
                ),
                ("message", Zod.String()),
                ("statusCode", Zod.Optional(Zod.Number())),
                ("retryAfter", Zod.Optional(Zod.String())),
                ("statusText", Zod.Optional(Zod.String()))
            ),
            Zod.Unknown()
        );
}
