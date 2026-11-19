// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>The AI SDK's <c>to-gateway-state-parts.ts</c>.</summary>
public static class ToGatewayStateParts
{
    /// <summary>
    /// Upstream's <c>toGatewayStateParts</c>: the state as the Gateway takes it. Inline file bytes become base64, and file data that cannot be
    /// sent (a URL or a provider reference) is rejected.
    /// </summary>
    public static JsonArray Convert(IReadOnlyList<DecisionStatePart> state, string modelType)
    {
        ArgumentNullException.ThrowIfNull(state);
        var parts = new JsonArray();
        foreach (var part in state)
        {
            switch (part)
            {
                case DecisionTextPart text:
                    parts.Add(new JsonObject { ["type"] = "text", ["text"] = text.Text });
                    break;
                case DecisionJsonPart json:
                    parts.Add(
                        new JsonObject { ["type"] = "json", ["value"] = JsonNode.Parse(json.Value.GetRawText()) }
                    );
                    break;
                case DecisionFilePart file:
                    if (file.DataType != "data")
                    {
                        throw new UnsupportedFunctionalityException(
                            "Gateway " + modelType + " file input: " + file.DataType + " data"
                        );
                    }

                    var converted = new JsonObject { ["type"] = "file", ["mediaType"] = file.MediaType };
                    if (file.Filename is not null)
                    {
                        converted["filename"] = file.Filename;
                    }

                    converted["data"] = new JsonObject { ["type"] = "data", ["data"] = file.GetBase64() };
                    if (file.ProviderOptions is not null)
                    {
                        converted["providerOptions"] = JsonSerializer.SerializeToNode(file.ProviderOptions);
                    }

                    parts.Add(converted);
                    break;
            }
        }

        return parts;
    }
}
