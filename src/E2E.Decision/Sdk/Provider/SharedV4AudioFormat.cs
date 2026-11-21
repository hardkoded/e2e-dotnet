// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>SharedV4AudioFormat</c> (<c>@ai-sdk/provider</c>): an audio MIME type and its sample rate.</summary>
public sealed record SharedV4AudioFormat(string Type, int? Rate = null);
