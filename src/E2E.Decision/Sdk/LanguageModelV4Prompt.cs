// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Decision.Sdk;

/// <summary>
/// The AI SDK's <c>LanguageModelV4Message</c> (<c>language-model/v4/language-model-v4-prompt.ts</c>). A prompt (<c>LanguageModelV4Prompt</c>)
/// is a list of messages. This is not the user-facing prompt: it is the standardized format a language model takes.
/// </summary>
public abstract record LanguageModelV4Message
{
    private protected LanguageModelV4Message() { }

    /// <summary>Additional provider-specific options, passed through to the provider, keyed by provider name.</summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }

    internal abstract JsonObject ToJson();

    internal virtual LanguageModelV4Message MapParts(Func<LanguageModelV4PromptPart, LanguageModelV4PromptPart> map) =>
        this;

    private protected JsonObject WithOptions(JsonObject json)
    {
        LanguageModelV4Json.AddProviderOptions(json, ProviderOptions);
        return json;
    }
}

/// <summary>A system message.</summary>
/// <param name="Content">The instructions.</param>
public sealed record LanguageModelV4SystemMessage(string Content) : LanguageModelV4Message
{
    internal override JsonObject ToJson() => WithOptions(new JsonObject { ["role"] = "system", ["content"] = Content });
}

/// <summary>A user message: text and file parts.</summary>
/// <param name="Content">The <see cref="LanguageModelV4TextPart"/> and <see cref="LanguageModelV4FilePart"/> parts.</param>
public sealed record LanguageModelV4UserMessage(IReadOnlyList<LanguageModelV4PromptPart> Content)
    : LanguageModelV4Message
{
    internal override JsonObject ToJson() =>
        WithOptions(new JsonObject { ["role"] = "user", ["content"] = LanguageModelV4Json.Parts(Content) });

    internal override LanguageModelV4Message MapParts(Func<LanguageModelV4PromptPart, LanguageModelV4PromptPart> map) =>
        this with
        {
            Content = [.. Content.Select(map)],
        };
}

/// <summary>An assistant message: text, file, custom, reasoning, reasoning file, tool call, and tool result parts.</summary>
/// <param name="Content">The parts.</param>
public sealed record LanguageModelV4AssistantMessage(IReadOnlyList<LanguageModelV4PromptPart> Content)
    : LanguageModelV4Message
{
    internal override JsonObject ToJson() =>
        WithOptions(new JsonObject { ["role"] = "assistant", ["content"] = LanguageModelV4Json.Parts(Content) });

    internal override LanguageModelV4Message MapParts(Func<LanguageModelV4PromptPart, LanguageModelV4PromptPart> map) =>
        this with
        {
            Content = [.. Content.Select(map)],
        };
}

/// <summary>A tool message: tool result and tool approval response parts.</summary>
/// <param name="Content">The parts.</param>
public sealed record LanguageModelV4ToolMessage(IReadOnlyList<LanguageModelV4PromptPart> Content)
    : LanguageModelV4Message
{
    internal override JsonObject ToJson() =>
        WithOptions(new JsonObject { ["role"] = "tool", ["content"] = LanguageModelV4Json.Parts(Content) });

    internal override LanguageModelV4Message MapParts(Func<LanguageModelV4PromptPart, LanguageModelV4PromptPart> map) =>
        this with
        {
            Content = [.. Content.Select(map)],
        };
}

/// <summary>A content part of a prompt message.</summary>
public abstract record LanguageModelV4PromptPart
{
    private protected LanguageModelV4PromptPart() { }

    /// <summary>Additional provider-specific options, passed through to the provider, keyed by provider name.</summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }

    internal abstract JsonObject ToJson();

    private protected JsonObject WithOptions(JsonObject json)
    {
        LanguageModelV4Json.AddProviderOptions(json, ProviderOptions);
        return json;
    }
}

/// <summary>Text content part of a prompt.</summary>
/// <param name="Text">The text content.</param>
public sealed record LanguageModelV4TextPart(string Text) : LanguageModelV4PromptPart
{
    internal override JsonObject ToJson() => WithOptions(new JsonObject { ["type"] = "text", ["text"] = Text });
}

/// <summary>Reasoning content part of a prompt.</summary>
/// <param name="Text">The reasoning text.</param>
public sealed record LanguageModelV4ReasoningPart(string Text) : LanguageModelV4PromptPart
{
    internal override JsonObject ToJson() => WithOptions(new JsonObject { ["type"] = "reasoning", ["text"] = Text });
}

/// <summary>Reasoning file content part of a prompt: a file generated as part of reasoning.</summary>
/// <param name="Data">The file data: a <see cref="SharedV4FileDataData"/> or a <see cref="SharedV4FileDataUrl"/>.</param>
/// <param name="MediaType">The IANA media type of the file.</param>
public sealed record LanguageModelV4ReasoningFilePart(SharedV4FileData Data, string MediaType)
    : LanguageModelV4PromptPart
{
    internal override JsonObject ToJson() =>
        WithOptions(
            new JsonObject
            {
                ["type"] = "reasoning-file",
                ["data"] = Data.ToJson(),
                ["mediaType"] = MediaType,
            }
        );
}

/// <summary>Provider-specific content part of a prompt, with no standardized payload beyond provider options.</summary>
/// <param name="Kind">The kind of custom content, in the format <c>{provider}.{provider-type}</c>.</param>
public sealed record LanguageModelV4CustomPart(string Kind) : LanguageModelV4PromptPart
{
    internal override JsonObject ToJson() => WithOptions(new JsonObject { ["type"] = "custom", ["kind"] = Kind });
}

/// <summary>File content part of a prompt.</summary>
/// <param name="Data">The file data.</param>
/// <param name="MediaType">Either a full IANA media type (<c>image/png</c>) or just the top-level segment (<c>image</c>).</param>
public sealed record LanguageModelV4FilePart(SharedV4FileData Data, string MediaType) : LanguageModelV4PromptPart
{
    /// <summary>Optional filename of the file.</summary>
    public string? Filename { get; init; }

    internal override JsonObject ToJson()
    {
        var json = new JsonObject { ["type"] = "file" };
        if (Filename is not null)
        {
            json["filename"] = Filename;
        }

        json["data"] = Data.ToJson();
        json["mediaType"] = MediaType;
        return WithOptions(json);
    }
}

/// <summary>Tool call content part of a prompt: a tool call, usually generated by the model.</summary>
/// <param name="ToolCallId">The ID of the tool call, used to match it with the tool result.</param>
/// <param name="ToolName">The name of the tool that is being called.</param>
/// <param name="Input">The arguments of the tool call: a JSON value that matches the tool's input schema.</param>
public sealed record LanguageModelV4ToolCallPart(string ToolCallId, string ToolName, JsonElement Input)
    : LanguageModelV4PromptPart
{
    /// <summary>Whether the provider executes the tool call. When not set or false, the client executes it.</summary>
    public bool? ProviderExecuted { get; init; }

    internal override JsonObject ToJson()
    {
        var json = new JsonObject
        {
            ["type"] = "tool-call",
            ["toolCallId"] = ToolCallId,
            ["toolName"] = ToolName,
            ["input"] = JsonNode.Parse(Input.GetRawText()),
        };
        if (ProviderExecuted is { } providerExecuted)
        {
            json["providerExecuted"] = providerExecuted;
        }

        return WithOptions(json);
    }
}

/// <summary>Tool result content part of a prompt: the result of the tool call with the matching ID.</summary>
/// <param name="ToolCallId">The ID of the tool call this result belongs to.</param>
/// <param name="ToolName">The name of the tool that generated the result.</param>
/// <param name="Output">The result of the tool call.</param>
public sealed record LanguageModelV4ToolResultPart(
    string ToolCallId,
    string ToolName,
    LanguageModelV4ToolResultOutput Output
) : LanguageModelV4PromptPart
{
    internal override JsonObject ToJson() =>
        WithOptions(
            new JsonObject
            {
                ["type"] = "tool-result",
                ["toolCallId"] = ToolCallId,
                ["toolName"] = ToolName,
                ["output"] = Output.ToJson(),
            }
        );
}

/// <summary>Tool approval response content part of a prompt: the user's decision to approve or deny a provider-executed tool call.</summary>
/// <param name="ApprovalId">The ID of the approval request this response refers to.</param>
/// <param name="Approved">Whether the approval was granted.</param>
public sealed record LanguageModelV4ToolApprovalResponsePart(string ApprovalId, bool Approved)
    : LanguageModelV4PromptPart
{
    /// <summary>The reason for the approval or denial.</summary>
    public string? Reason { get; init; }

    internal override JsonObject ToJson()
    {
        var json = new JsonObject
        {
            ["type"] = "tool-approval-response",
            ["approvalId"] = ApprovalId,
            ["approved"] = Approved,
        };
        if (Reason is not null)
        {
            json["reason"] = Reason;
        }

        return WithOptions(json);
    }
}

/// <summary>
/// The AI SDK's <c>LanguageModelV4ToolResultOutput</c>: the result of a tool call. One of <see cref="LanguageModelV4ToolResultOutputText"/>,
/// <see cref="LanguageModelV4ToolResultOutputJson"/>, <see cref="LanguageModelV4ToolResultOutputExecutionDenied"/>,
/// <see cref="LanguageModelV4ToolResultOutputErrorText"/>, <see cref="LanguageModelV4ToolResultOutputErrorJson"/>, or
/// <see cref="LanguageModelV4ToolResultOutputContent"/>.
/// </summary>
public abstract record LanguageModelV4ToolResultOutput
{
    private protected LanguageModelV4ToolResultOutput() { }

    /// <summary>Provider-specific options.</summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }

    internal abstract JsonObject ToJson();

    internal virtual LanguageModelV4ToolResultOutput MapParts(
        Func<LanguageModelV4ToolResultContentPart, LanguageModelV4ToolResultContentPart> map
    ) => this;

    private protected JsonObject WithOptions(JsonObject json)
    {
        LanguageModelV4Json.AddProviderOptions(json, ProviderOptions);
        return json;
    }
}

/// <summary>Text tool output that is sent directly to the API.</summary>
/// <param name="Value">The text.</param>
public sealed record LanguageModelV4ToolResultOutputText(string Value) : LanguageModelV4ToolResultOutput
{
    internal override JsonObject ToJson() => WithOptions(new JsonObject { ["type"] = "text", ["value"] = Value });
}

/// <summary>JSON tool output.</summary>
/// <param name="Value">The JSON value.</param>
public sealed record LanguageModelV4ToolResultOutputJson(JsonElement Value) : LanguageModelV4ToolResultOutput
{
    internal override JsonObject ToJson() =>
        WithOptions(new JsonObject { ["type"] = "json", ["value"] = JsonNode.Parse(Value.GetRawText()) });
}

/// <summary>The user denied the execution of the tool call.</summary>
public sealed record LanguageModelV4ToolResultOutputExecutionDenied : LanguageModelV4ToolResultOutput
{
    /// <summary>The reason for the denial.</summary>
    public string? Reason { get; init; }

    internal override JsonObject ToJson()
    {
        var json = new JsonObject { ["type"] = "execution-denied" };
        if (Reason is not null)
        {
            json["reason"] = Reason;
        }

        return WithOptions(json);
    }
}

/// <summary>A tool error as text.</summary>
/// <param name="Value">The text.</param>
public sealed record LanguageModelV4ToolResultOutputErrorText(string Value) : LanguageModelV4ToolResultOutput
{
    internal override JsonObject ToJson() => WithOptions(new JsonObject { ["type"] = "error-text", ["value"] = Value });
}

/// <summary>A tool error as JSON.</summary>
/// <param name="Value">The JSON value.</param>
public sealed record LanguageModelV4ToolResultOutputErrorJson(JsonElement Value) : LanguageModelV4ToolResultOutput
{
    internal override JsonObject ToJson() =>
        WithOptions(new JsonObject { ["type"] = "error-json", ["value"] = JsonNode.Parse(Value.GetRawText()) });
}

/// <summary>Tool output made of content parts: text, files, and custom content.</summary>
/// <param name="Value">The content parts.</param>
public sealed record LanguageModelV4ToolResultOutputContent(IReadOnlyList<LanguageModelV4ToolResultContentPart> Value)
    : LanguageModelV4ToolResultOutput
{
    internal override JsonObject ToJson() =>
        WithOptions(
            new JsonObject
            {
                ["type"] = "content",
                ["value"] = new JsonArray([.. Value.Select(part => (JsonNode?)part.ToJson())]),
            }
        );

    internal override LanguageModelV4ToolResultOutput MapParts(
        Func<LanguageModelV4ToolResultContentPart, LanguageModelV4ToolResultContentPart> map
    ) => this with { Value = [.. Value.Select(map)] };
}

/// <summary>A part of a <see cref="LanguageModelV4ToolResultOutputContent"/>.</summary>
public abstract record LanguageModelV4ToolResultContentPart
{
    private protected LanguageModelV4ToolResultContentPart() { }

    /// <summary>Provider-specific options.</summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }

    internal abstract JsonObject ToJson();

    private protected JsonObject WithOptions(JsonObject json)
    {
        LanguageModelV4Json.AddProviderOptions(json, ProviderOptions);
        return json;
    }
}

/// <summary>Text content.</summary>
/// <param name="Text">The text.</param>
public sealed record LanguageModelV4ToolResultContentText(string Text) : LanguageModelV4ToolResultContentPart
{
    internal override JsonObject ToJson() => WithOptions(new JsonObject { ["type"] = "text", ["text"] = Text });
}

/// <summary>File content.</summary>
/// <param name="Data">The file data.</param>
/// <param name="MediaType">Either a full IANA media type or just the top-level segment.</param>
public sealed record LanguageModelV4ToolResultContentFile(SharedV4FileData Data, string MediaType)
    : LanguageModelV4ToolResultContentPart
{
    /// <summary>Optional filename of the file.</summary>
    public string? Filename { get; init; }

    internal override JsonObject ToJson()
    {
        var json = new JsonObject
        {
            ["type"] = "file",
            ["data"] = Data.ToJson(),
            ["mediaType"] = MediaType,
        };
        if (Filename is not null)
        {
            json["filename"] = Filename;
        }

        return WithOptions(json);
    }
}

/// <summary>Custom content, for provider-specific content parts.</summary>
public sealed record LanguageModelV4ToolResultContentCustom : LanguageModelV4ToolResultContentPart
{
    internal override JsonObject ToJson() => WithOptions(new JsonObject { ["type"] = "custom" });
}
