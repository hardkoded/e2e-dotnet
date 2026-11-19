// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Decision.Tests;

internal static class GatewayJson
{
    public static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
