// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>SharedV4Warning</c> (<c>@ai-sdk/provider</c>): a warning a provider reports for a call.</summary>
public abstract record SharedV4Warning;

/// <summary>A feature the model does not support (<c>type: 'unsupported'</c>).</summary>
public sealed record SharedV4UnsupportedWarning(string Feature, string? Details = null) : SharedV4Warning;

/// <summary>A feature the model supports in a compatibility mode (<c>type: 'compatibility'</c>).</summary>
public sealed record SharedV4CompatibilityWarning(string Feature, string? Details = null) : SharedV4Warning;

/// <summary>A setting that is deprecated (<c>type: 'deprecated'</c>).</summary>
public sealed record SharedV4DeprecatedWarning(string Setting, string Message) : SharedV4Warning;

/// <summary>Any other warning (<c>type: 'other'</c>).</summary>
public sealed record SharedV4OtherWarning(string Message) : SharedV4Warning;
