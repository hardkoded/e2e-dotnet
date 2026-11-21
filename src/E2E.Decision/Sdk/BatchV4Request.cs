// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Decision.Sdk;

/// <summary>Upstream's <c>BatchV4RequestBase</c> (<c>batch-v4-request.ts</c>), the union of <c>TextBatchV4Request | ImageBatchV4Request</c>.</summary>
public abstract record BatchV4Request
{
    /// <summary>The id of the request, unique in the batch.</summary>
    public required string Id { get; init; }

    /// <summary>The model that answers the request.</summary>
    public required string ModelId { get; init; }

    /// <summary>The modality: <c>text</c> or <c>image</c>.</summary>
    public abstract string Type { get; }

    /// <summary>
    /// The call options of the request: for text, the <c>prompt</c> and the other <c>LanguageModelV4CallOptions</c> a batch takes; for an image,
    /// the <c>ImageModelV4CallOptions</c> a batch takes. They are JSON because this port has no model call option types. A <c>byte[]</c> value
    /// (<c>JsonValue.Create(bytes)</c>) is file data.
    /// </summary>
    public required JsonObject Options { get; init; }
}

/// <summary>Upstream's <c>TextBatchV4Request</c> (<c>text-batch-v4-request.ts</c>).</summary>
public sealed record TextBatchV4Request : BatchV4Request
{
    public override string Type => "text";
}

/// <summary>Upstream's <c>ImageBatchV4Request</c> (<c>image-batch-v4-request.ts</c>).</summary>
public sealed record ImageBatchV4Request : BatchV4Request
{
    public override string Type => "image";
}
