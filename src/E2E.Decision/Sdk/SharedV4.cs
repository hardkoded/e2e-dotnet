// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Decision.Sdk;

/// <summary>
/// The AI SDK's <c>SharedV4Warning</c> (<c>shared/v4/shared-v4-warning.ts</c>): a warning from the model, for example that a feature is
/// unsupported or that a compatibility mode is used. <see cref="Type"/> is <c>unsupported</c>, <c>compatibility</c>, <c>deprecated</c>, or
/// <c>other</c>; the fields that type does not have are null.
/// </summary>
public sealed record SharedV4Warning
{
    /// <summary>The kind of warning: <c>unsupported</c>, <c>compatibility</c>, <c>deprecated</c>, or <c>other</c>.</summary>
    public required string Type { get; init; }

    /// <summary>The feature that is not supported or used in a compatibility mode (<c>unsupported</c> and <c>compatibility</c>).</summary>
    public string? Feature { get; init; }

    /// <summary>Additional details about the warning (<c>unsupported</c> and <c>compatibility</c>).</summary>
    public string? Details { get; init; }

    /// <summary>The deprecated setting or feature name (<c>deprecated</c>).</summary>
    public string? Setting { get; init; }

    /// <summary>The message of the warning (<c>deprecated</c> and <c>other</c>).</summary>
    public string? Message { get; init; }

    internal static SharedV4Warning FromJson(JsonElement warning) =>
        new()
        {
            Type =
                warning.ValueKind == JsonValueKind.Object
                    ? OptionalString(warning, "type") ?? string.Empty
                    : throw new InvalidResponseDataException("Type validation failed: a warning must be an object."),
            Feature = OptionalString(warning, "feature"),
            Details = OptionalString(warning, "details"),
            Setting = OptionalString(warning, "setting"),
            Message = OptionalString(warning, "message"),
        };

    internal JsonObject ToJson()
    {
        var json = new JsonObject { ["type"] = Type };
        if (Feature is not null)
        {
            json["feature"] = Feature;
        }

        if (Details is not null)
        {
            json["details"] = Details;
        }

        if (Setting is not null)
        {
            json["setting"] = Setting;
        }

        if (Message is not null)
        {
            json["message"] = Message;
        }

        return json;
    }

    private static string? OptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

/// <summary>
/// The AI SDK's <c>SharedV4FileData</c> (<c>shared/v4/shared-v4-file-data.ts</c>): file data as a tagged union, a
/// <see cref="SharedV4FileDataData"/>, <see cref="SharedV4FileDataUrl"/>, <see cref="SharedV4FileDataReference"/>, or
/// <see cref="SharedV4FileDataText"/>.
/// </summary>
public abstract record SharedV4FileData
{
    private protected SharedV4FileData() { }

    internal abstract JsonObject ToJson();
}

/// <summary>File data holding raw bytes (upstream's <c>Uint8Array</c>) or a base64-encoded string.</summary>
public sealed record SharedV4FileDataData : SharedV4FileData
{
    /// <summary>Raw bytes.</summary>
    public SharedV4FileDataData(byte[] data) => Bytes = data;

    /// <summary>A base64-encoded string.</summary>
    public SharedV4FileDataData(string base64) => Base64 = base64;

    /// <summary>The raw bytes, when the data is bytes.</summary>
    public byte[]? Bytes { get; }

    /// <summary>The base64 string, when the data is a string.</summary>
    public string? Base64 { get; }

    internal override JsonObject ToJson() =>
        new() { ["type"] = "data", ["data"] = Base64 ?? Convert.ToBase64String(Bytes!) };
}

/// <summary>File data holding a URL that points to the file.</summary>
/// <param name="Url">The URL.</param>
public sealed record SharedV4FileDataUrl(Uri Url) : SharedV4FileData
{
    /// <summary>The original URL string before it was parsed into a <see cref="Uri"/>, to preserve an opaque non-HTTP URI exactly.</summary>
    public string? OriginalUrl { get; init; }

    internal override JsonObject ToJson()
    {
        var json = new JsonObject { ["type"] = "url", ["url"] = Url.AbsoluteUri };
        if (OriginalUrl is not null)
        {
            json["originalUrl"] = OriginalUrl;
        }

        return json;
    }
}

/// <summary>File data holding a provider reference (<c>{ [provider]: id }</c>).</summary>
/// <param name="Reference">The file identifier of each provider.</param>
public sealed record SharedV4FileDataReference(IReadOnlyDictionary<string, string> Reference) : SharedV4FileData
{
    internal override JsonObject ToJson()
    {
        var reference = new JsonObject();
        foreach (var (provider, id) in Reference)
        {
            reference[provider] = id;
        }

        return new JsonObject { ["type"] = "reference", ["reference"] = reference };
    }
}

/// <summary>File data holding inline text content, such as an inline text document.</summary>
/// <param name="Text">The text.</param>
public sealed record SharedV4FileDataText(string Text) : SharedV4FileData
{
    internal override JsonObject ToJson() => new() { ["type"] = "text", ["text"] = Text };
}
