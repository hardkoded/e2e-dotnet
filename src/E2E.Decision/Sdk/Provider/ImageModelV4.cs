// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>ImageModelV4File</c>: an input image, as data or as a URL.</summary>
public abstract record ImageModelV4File
{
    public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
}

/// <summary>A file (<c>type: 'file'</c>) whose data is base64 text or bytes (<c>string | Uint8Array</c>).</summary>
public sealed record ImageModelV4FileData : ImageModelV4File
{
    public ImageModelV4FileData(string mediaType, string base64Data)
    {
        MediaType = mediaType;
        Base64Data = base64Data;
    }

    public ImageModelV4FileData(string mediaType, byte[] data)
    {
        MediaType = mediaType;
        Data = data;
    }

    public string MediaType { get; }

    /// <summary>The data as base64 text, when it was given as text.</summary>
    public string? Base64Data { get; }

    /// <summary>The data as bytes, when it was given as bytes.</summary>
    public byte[]? Data { get; }
}

/// <summary>A file at a URL (<c>type: 'url'</c>).</summary>
public sealed record ImageModelV4FileUrl(string Url) : ImageModelV4File;

/// <summary>The AI SDK's <c>ImageModelV4CallOptions</c>.</summary>
public sealed class ImageModelV4CallOptions
{
    public string? Prompt { get; init; }

    public required int N { get; init; }

    /// <summary>The size, such as <c>1024x1024</c>.</summary>
    public string? Size { get; init; }

    /// <summary>The aspect ratio, such as <c>16:9</c>.</summary>
    public string? AspectRatio { get; init; }

    public long? Seed { get; init; }

    public IReadOnlyList<ImageModelV4File>? Files { get; init; }

    public ImageModelV4File? Mask { get; init; }

    public required IReadOnlyDictionary<string, JsonElement> ProviderOptions { get; init; }

    public IReadOnlyDictionary<string, string?>? Headers { get; init; }
}

/// <summary>The AI SDK's <c>ImageModelV4Usage</c>.</summary>
public sealed record ImageModelV4Usage(double? InputTokens, double? OutputTokens, double? TotalTokens);

/// <summary>The <c>response</c> of an <see cref="ImageModelV4Result"/>.</summary>
public sealed record ImageModelV4ResultResponse(
    DateTimeOffset Timestamp,
    string ModelId,
    IReadOnlyDictionary<string, string>? Headers
);

/// <summary>The AI SDK's <c>ImageModelV4Result</c>.</summary>
public sealed class ImageModelV4Result
{
    /// <summary>The images as base64 text (<c>Array&lt;string&gt;</c>); null when the result holds <see cref="ImageBytes"/>.</summary>
    public IReadOnlyList<string>? Images { get; init; }

    /// <summary>The images as bytes (<c>Array&lt;Uint8Array&gt;</c>); null when the result holds <see cref="Images"/>.</summary>
    public IReadOnlyList<byte[]>? ImageBytes { get; init; }

    public bool? IsRetryable { get; init; }

    public required IReadOnlyList<SharedV4Warning> Warnings { get; init; }

    /// <summary>The provider metadata, keyed by provider name (<c>ImageModelV4ProviderMetadata</c>).</summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderMetadata { get; init; }

    public required ImageModelV4ResultResponse Response { get; init; }

    public ImageModelV4Usage? Usage { get; init; }
}

/// <summary>The AI SDK's <c>ImageModelV4</c>: a model that generates images.</summary>
public interface IImageModelV4
{
    string SpecificationVersion { get; }

    string Provider { get; }

    string ModelId { get; }

    long? MaxImagesPerCall { get; }

    bool? SupportsFileInputs => null;

    bool? SupportsMaskInputs => null;

    Task<ImageModelV4Result> DoGenerateAsync(
        ImageModelV4CallOptions options,
        CancellationToken cancellationToken = default
    );
}
