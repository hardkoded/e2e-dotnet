// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace E2E.Decision.Sdk;

/// <summary>
/// Upstream's <c>ProviderExecutedTool</c> (<c>provider-utils/src/types/tool.ts</c>): a tool with provider-defined input and output
/// schemas that the provider executes.
/// </summary>
public class ProviderExecutedTool
{
    internal ProviderExecutedTool(
        string id,
        JsonObject args,
        FlexibleSchema inputSchema,
        FlexibleSchema outputSchema,
        bool? supportsDeferredResults
    )
    {
        Id = id;
        Args = args;
        InputSchema = inputSchema;
        OutputSchema = outputSchema;
        SupportsDeferredResults = supportsDeferredResults;
    }

    /// <summary>Always <c>provider</c>.</summary>
    public string Type => "provider";

    /// <summary>The ID of the tool, in the format <c>&lt;provider-name&gt;.&lt;unique-tool-name&gt;</c>.</summary>
    public string Id { get; }

    /// <summary>The arguments that configure the tool, as the provider expects them (camelCase names, unset values left out).</summary>
    public JsonObject Args { get; }

    /// <summary>The schema of the input the model gives the tool.</summary>
    public FlexibleSchema InputSchema { get; }

    /// <summary>The schema of the output the provider returns.</summary>
    public FlexibleSchema OutputSchema { get; }

    /// <summary>Always true: the provider executes the tool.</summary>
    public bool IsProviderExecuted => true;

    /// <summary>Whether the tool result may come in a later turn than the tool call.</summary>
    public bool? SupportsDeferredResults { get; }
}

/// <summary>Upstream's <c>ProviderExecutedTool&lt;INPUT, OUTPUT&gt;</c>: <typeparamref name="TInput"/> and <typeparamref name="TOutput"/> type the schemas.</summary>
/// <typeparam name="TInput">What the input schema describes.</typeparam>
/// <typeparam name="TOutput">What the output schema describes.</typeparam>
public sealed class ProviderExecutedTool<TInput, TOutput> : ProviderExecutedTool
{
    internal ProviderExecutedTool(
        string id,
        JsonObject args,
        FlexibleSchema inputSchema,
        FlexibleSchema outputSchema,
        bool? supportsDeferredResults
    )
        : base(id, args, inputSchema, outputSchema, supportsDeferredResults) { }
}

/// <summary>
/// The <c>createProviderExecutedToolFactory</c> function of <c>provider-utils/src/provider-executed-tool-factory.ts</c>. It leaves out
/// upstream's <c>onInputStart</c>, <c>onInputDelta</c>, <c>onInputAvailable</c> and <c>redactArgs</c>, which no Gateway tool uses.
/// </summary>
public static class ProviderExecutedToolFactory
{
    private static readonly JsonSerializerOptions s_argsOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Upstream's <c>createProviderExecutedToolFactory</c>.</summary>
    /// <param name="id">The tool ID, in the format <c>&lt;provider-name&gt;.&lt;unique-tool-name&gt;</c>.</param>
    /// <param name="inputSchema">The schema of the tool input.</param>
    /// <param name="outputSchema">The schema of the tool output.</param>
    /// <param name="supportsDeferredResults">Whether the tool result may come in a later turn than the tool call.</param>
    /// <typeparam name="TInput">What the input schema describes.</typeparam>
    /// <typeparam name="TOutput">What the output schema describes.</typeparam>
    /// <typeparam name="TArgs">The arguments that configure the tool.</typeparam>
    public static Func<TArgs, ProviderExecutedTool<TInput, TOutput>> CreateProviderExecutedToolFactory<
        TInput,
        TOutput,
        TArgs
    >(string id, FlexibleSchema inputSchema, FlexibleSchema outputSchema, bool? supportsDeferredResults = null)
        where TArgs : class =>
        args => new ProviderExecutedTool<TInput, TOutput>(
            id,
            JsonSerializer.SerializeToNode(args, s_argsOptions)?.AsObject() ?? [],
            inputSchema,
            outputSchema,
            supportsDeferredResults
        );
}
