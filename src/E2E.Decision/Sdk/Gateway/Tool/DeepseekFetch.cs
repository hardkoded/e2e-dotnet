// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>Upstream's <c>DeepSeekFetchConfig</c>.</summary>
public sealed class DeepSeekFetchConfig
{
    /// <summary>Pin the target URL instead of allowing the model to choose it.</summary>
    public string? Url { get; init; }
}

/// <summary>Upstream's <c>DeepSeekFetchInput</c>.</summary>
public sealed class DeepSeekFetchInput
{
    /// <summary>URL to fetch, at most 8192 characters; HTTP(S) only, without a username or password.</summary>
    [JsonPropertyName("url")]
    public required string Url { get; init; }
}

/// <summary>Upstream's <c>DeepSeekFetchOutput</c>: a <see cref="DeepSeekFetchResponse"/> or a <see cref="GatewayToolError"/>.</summary>
public interface IDeepSeekFetchOutput { }

/// <summary>Upstream's <c>DeepSeekFetchResponse</c>.</summary>
public sealed class DeepSeekFetchResponse : IDeepSeekFetchOutput
{
    /// <summary>The page title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Page content as Markdown.</summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    /// <summary>Fields the Gateway adds later (the schema keeps them).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

/// <summary>
/// The tool of <c>tool/deepseek-fetch.ts</c>. The class name is .NET-only: C# has no module-level functions, so <c>deepseekFetch</c>
/// and <c>deepseekFetchToolFactory</c> are members of it.
/// </summary>
public static class DeepseekFetchTool
{
    private static readonly LazySchema s_inputSchema = SchemaUtils.LazySchema(() =>
        SchemaUtils.ZodSchema(
            Zod.Object(
                (
                    "url",
                    Zod.Describe(
                        Zod.String(),
                        "URL to fetch, at most 8192 characters; HTTP(S) only, without a username or password."
                    )
                )
            )
        )
    );

    private static readonly LazySchema s_outputSchema = SchemaUtils.LazySchema(() =>
        SchemaUtils.ZodSchema(
            Zod.Union(
                Zod.Catchall(
                    Zod.Object(("title", Zod.Nullish(Zod.String())), ("content", Zod.String())),
                    Zod.Unknown()
                ),
                GatewayToolErrors.GatewayToolErrorSchema()
            )
        )
    );

    /// <summary>Upstream's <c>deepseekFetchToolFactory</c>.</summary>
    public static Func<
        DeepSeekFetchConfig,
        ProviderExecutedTool<DeepSeekFetchInput, IDeepSeekFetchOutput>
    > DeepseekFetchToolFactory { get; } =
        ProviderExecutedToolFactory.CreateProviderExecutedToolFactory<
            DeepSeekFetchInput,
            IDeepSeekFetchOutput,
            DeepSeekFetchConfig
        >("gateway.deepseek_fetch", s_inputSchema, s_outputSchema);

    /// <summary>
    /// Upstream's <c>deepseekFetch</c>: fetches a page as Markdown through DeepSeek's standalone Fetch API. Executed by AI Gateway for any
    /// model, separate from native model web search.
    /// </summary>
    public static ProviderExecutedTool<DeepSeekFetchInput, IDeepSeekFetchOutput> DeepseekFetch(
        DeepSeekFetchConfig? config = null
    ) => DeepseekFetchToolFactory(config ?? new DeepSeekFetchConfig());
}
