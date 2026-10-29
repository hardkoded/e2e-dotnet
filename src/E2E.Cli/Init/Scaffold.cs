// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Cli;

/// <summary>The model choice <c>init</c> writes: which gateway, and for an OpenAI-compatible one, where.</summary>
/// <param name="Gateway">The <see cref="GatewayPreset.Id"/>.</param>
/// <param name="Endpoint">The base URL of an OpenAI-compatible gateway.</param>
internal sealed record ScaffoldModel(string Gateway, string? Endpoint = null);

/// <summary>What <see cref="Scaffold.CreateScaffold"/> composes: the text of <c>e2e.config.json</c>.</summary>
/// <param name="Config">The config file's text.</param>
internal sealed record ScaffoldResult(string Config);

/// <summary>
/// Upstream's <c>cli/init/scaffold.ts</c>. It writes <c>e2e.config.json</c> where upstream writes <c>e2e.config.ts</c> and
/// the example tests; the port scaffolds no test project, since a .NET test project is the user's own.
/// </summary>
internal static class Scaffold
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// The agents block. The model is named by its provider, written in full, so a reader sees where every model call goes
    /// without knowing any e2e default; there is none.
    /// </summary>
    private static JsonObject AgentConfig(ScaffoldModel model)
    {
        var preset = Gateways.GetGatewayPreset(model.Gateway);
        var agent = new JsonObject { ["provider"] = preset.Provider, ["model"] = preset.Model };
        if (model.Gateway == "openai-compatible")
        {
            agent["baseUrl"] = model.Endpoint ?? Gateways.DefaultEndpoint;
        }

        agent["system"] = "You are a thorough QA agent. Verify every outcome.";
        return new JsonObject { ["default"] = agent };
    }

    /// <summary>Composes the config from the selected engines (none for an engine-less config, which keeps the default web target) and the model.</summary>
    public static ScaffoldResult CreateScaffold(IReadOnlyList<string> engineIds, ScaffoldModel? model)
    {
        ArgumentNullException.ThrowIfNull(engineIds);
        var engines = engineIds.Select(Engines.GetEnginePreset).ToList();
        if (engines.Count > 1)
        {
            throw new ArgumentException("the .NET port runs one target; choose one engine", nameof(engineIds));
        }

        var config = new JsonObject();
        if (engines.Count == 1)
        {
            config["targets"] = new JsonArray(engines[0].Target());
        }

        if (model is not null)
        {
            config["agents"] = AgentConfig(model);
        }

        return new ScaffoldResult(config.ToJsonString(Indented) + "\n");
    }
}
