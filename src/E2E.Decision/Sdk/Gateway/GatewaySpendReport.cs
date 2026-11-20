// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>Upstream's <c>GatewaySpendReportParams</c>.</summary>
public sealed class GatewaySpendReportParams
{
    /// <summary>Start date in YYYY-MM-DD format (inclusive).</summary>
    public required string StartDate { get; init; }

    /// <summary>End date in YYYY-MM-DD format (inclusive).</summary>
    public required string EndDate { get; init; }

    /// <summary>Primary aggregation dimension: <c>day</c>, <c>user</c>, <c>model</c>, <c>tag</c>, <c>provider</c>, or <c>credential_type</c>. Defaults to <c>day</c>.</summary>
    public string? GroupBy { get; init; }

    /// <summary>Time granularity when <see cref="GroupBy"/> is <c>day</c>: <c>day</c> or <c>hour</c>.</summary>
    public string? DatePart { get; init; }

    /// <summary>Filter to a specific user's spend.</summary>
    public string? UserId { get; init; }

    /// <summary>Filter to a specific model, such as <c>anthropic/claude-sonnet-4.5</c>.</summary>
    public string? Model { get; init; }

    /// <summary>Filter to a specific provider, such as <c>anthropic</c>.</summary>
    public string? Provider { get; init; }

    /// <summary>Filter to <c>byok</c> or <c>system</c> credentials.</summary>
    public string? CredentialType { get; init; }

    /// <summary>Filter to requests with these tags.</summary>
    public IReadOnlyList<string>? Tags { get; init; }
}

/// <summary>Upstream's <c>GatewaySpendReportRow</c>.</summary>
public sealed record GatewaySpendReportRow
{
    /// <summary>Date string (present when <c>groupBy</c> is <c>day</c>).</summary>
    public string? Day { get; init; }

    /// <summary>Hour timestamp (present when <c>groupBy</c> is <c>day</c> and <c>datePart</c> is <c>hour</c>).</summary>
    public string? Hour { get; init; }

    /// <summary>User identifier (present when <c>groupBy</c> is <c>user</c>).</summary>
    public string? User { get; init; }

    /// <summary>Model identifier (present when <c>groupBy</c> is <c>model</c>).</summary>
    public string? Model { get; init; }

    /// <summary>Tag value (present when <c>groupBy</c> is <c>tag</c>).</summary>
    public string? Tag { get; init; }

    /// <summary>Provider name (present when <c>groupBy</c> is <c>provider</c>).</summary>
    public string? Provider { get; init; }

    /// <summary>Credential type, <c>byok</c> or <c>system</c> (present when <c>groupBy</c> is <c>credential_type</c>).</summary>
    public string? CredentialType { get; init; }

    /// <summary>Total cost in USD.</summary>
    public required double TotalCost { get; init; }

    /// <summary>Market cost in USD.</summary>
    public double? MarketCost { get; init; }

    /// <summary>Number of input tokens.</summary>
    public double? InputTokens { get; init; }

    /// <summary>Number of output tokens.</summary>
    public double? OutputTokens { get; init; }

    /// <summary>Number of cached input tokens.</summary>
    public double? CachedInputTokens { get; init; }

    /// <summary>Number of cache creation input tokens.</summary>
    public double? CacheCreationInputTokens { get; init; }

    /// <summary>Number of reasoning tokens.</summary>
    public double? ReasoningTokens { get; init; }

    /// <summary>Number of requests.</summary>
    public double? RequestCount { get; init; }
}

/// <summary>Upstream's <c>GatewaySpendReportResponse</c>.</summary>
public sealed record GatewaySpendReportResponse(IReadOnlyList<GatewaySpendReportRow> Results);

/// <summary>The AI SDK's <c>GatewaySpendReport</c> (<c>gateway-spend-report.ts</c>): <c>GET {origin}/v1/report</c>.</summary>
public sealed class GatewaySpendReport(GatewayConfig config)
{
    /// <summary>Upstream's <c>getSpendReport</c>.</summary>
    public async Task<GatewaySpendReportResponse> GetSpendReportAsync(
        GatewaySpendReportParams parameters,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(parameters);
        try
        {
            var baseUrl = new Uri(config.BaseUrl);
            var query = new List<KeyValuePair<string, string>>
            {
                new("start_date", parameters.StartDate),
                new("end_date", parameters.EndDate),
            };
            AddIfSet(query, "group_by", parameters.GroupBy);
            AddIfSet(query, "date_part", parameters.DatePart);
            AddIfSet(query, "user_id", parameters.UserId);
            AddIfSet(query, "model", parameters.Model);
            AddIfSet(query, "provider", parameters.Provider);
            AddIfSet(query, "credential_type", parameters.CredentialType);
            if (parameters.Tags is { Count: > 0 })
            {
                query.Add(new("tags", string.Join(",", parameters.Tags)));
            }

            var headers = config.Headers is null
                ? null
                : ProviderUtils.CombineHeaders(await config.Headers(cancellationToken).ConfigureAwait(false));
            var response = await ProviderUtils
                .GetFromApiAsync(
                    GatewayHttp.Client(config),
                    baseUrl.GetLeftPart(UriPartial.Authority) + "/v1/report?" + GatewayHttp.QueryString(query),
                    headers,
                    GatewayHttp.ErrorMessage,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return ProviderUtils.ProcessSuccessfulResponse(response, ReadResponse);
        }
        catch (Exception error)
        {
            throw GatewayErrors.AsGatewayError(error);
        }
    }

    private static void AddIfSet(List<KeyValuePair<string, string>> query, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            query.Add(new(name, value));
        }
    }

    // gatewaySpendReportResponseSchema
    private static GatewaySpendReportResponse ReadResponse(JsonElement root)
    {
        const string Path = "response.results[]";
        var results = new List<GatewaySpendReportRow>();
        foreach (
            var element in Zod.Array(
                    Zod.Property(Zod.Object(root, "response"), "results", "response"),
                    "response.results"
                )
                .EnumerateArray()
        )
        {
            var row = Zod.Object(element, Path);
            var credentialType = Zod.OptionalString(row, "credential_type", Path);
            if (credentialType is not (null or "byok" or "system"))
            {
                throw Zod.Invalid(Path + ".credential_type", "expected 'byok' or 'system'");
            }

            results.Add(
                new GatewaySpendReportRow
                {
                    Day = Zod.OptionalString(row, "day", Path),
                    Hour = Zod.OptionalString(row, "hour", Path),
                    User = Zod.OptionalString(row, "user", Path),
                    Model = Zod.OptionalString(row, "model", Path),
                    Tag = Zod.OptionalString(row, "tag", Path),
                    Provider = Zod.OptionalString(row, "provider", Path),
                    CredentialType = credentialType,
                    TotalCost = Zod.Number(row, "total_cost", Path),
                    MarketCost = Zod.OptionalNumber(row, "market_cost", Path),
                    InputTokens = Zod.OptionalNumber(row, "input_tokens", Path),
                    OutputTokens = Zod.OptionalNumber(row, "output_tokens", Path),
                    CachedInputTokens = Zod.OptionalNumber(row, "cached_input_tokens", Path),
                    CacheCreationInputTokens = Zod.OptionalNumber(row, "cache_creation_input_tokens", Path),
                    ReasoningTokens = Zod.OptionalNumber(row, "reasoning_tokens", Path),
                    RequestCount = Zod.OptionalNumber(row, "request_count", Path),
                }
            );
        }

        return new GatewaySpendReportResponse(results);
    }
}
