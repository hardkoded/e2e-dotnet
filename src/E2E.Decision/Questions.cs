// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;

namespace E2E.Decision;

/// <summary>
/// The pixels a decision request carries beside its state. Upstream's <c>DecisionRequest.screenshot</c>.
/// </summary>
public sealed record DecisionScreenshot(string MediaType, byte[] Data);

/// <summary>
/// One decide call's input (upstream's <c>DecisionRequest</c> in <c>questions.ts</c>). The question builders of that file are the
/// decision executor's and come with it.
/// </summary>
public sealed class DecisionRequest
{
    /// <summary>The JSON state every question reads.</summary>
    public required System.Text.Json.JsonElement State { get; init; }

    public required IReadOnlyDictionary<string, DecisionQuestion> Questions { get; init; }

    /// <summary>The masked screenshot that rides beside the JSON state as a file part. A text-only provider rejects it before the request.</summary>
    public DecisionScreenshot? Screenshot { get; init; }
}
