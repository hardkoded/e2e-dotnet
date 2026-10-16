// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Limrun;

/// <summary>Builds the options objects the device client takes. A null value is left out, as upstream leaves out an undefined one.</summary>
internal static class DeviceArgs
{
    public static JsonObject Of(params (string Key, JsonNode? Value)[] properties)
    {
        var result = new JsonObject();
        foreach (var (key, value) in properties)
        {
            if (value is not null)
            {
                result[key] = value;
            }
        }

        return result;
    }
}
