// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>
/// A warning from the model (the AI SDK's <c>SharedV4Warning</c>): a feature the model does not support, a compatibility mode, a deprecated
/// setting, or something else. <see cref="Type"/> is <c>unsupported</c>, <c>compatibility</c>, <c>deprecated</c>, or <c>other</c>.
/// </summary>
public sealed record SharedV4Warning
{
    public required string Type { get; init; }

    /// <summary>The feature that is not supported (<c>unsupported</c>), or used in a compatibility mode (<c>compatibility</c>).</summary>
    public string? Feature { get; init; }

    /// <summary>Additional details about an <c>unsupported</c> or <c>compatibility</c> warning.</summary>
    public string? Details { get; init; }

    /// <summary>The deprecated setting or feature name of a <c>deprecated</c> warning.</summary>
    public string? Setting { get; init; }

    /// <summary>The message of a <c>deprecated</c> or <c>other</c> warning.</summary>
    public string? Message { get; init; }
}
