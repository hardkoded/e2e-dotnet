// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E;

/// <summary>
/// One captured engine state (upstream's <c>EngineState</c>, <c>engine/surface.ts</c>): an opaque, versioned payload an engine can save
/// from a session and restore into another. <paramref name="ExpiresAt"/> is an RFC 3339 timestamp after which the state must not be restored.
/// </summary>
public sealed record EngineState(string Format, int Version, JsonNode? Data, string? ExpiresAt = null);
