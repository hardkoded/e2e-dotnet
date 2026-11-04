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

/// <summary>One test file the scaffold writes, as <c>&lt;Name&gt;.cs</c> (upstream's <c>ScaffoldTest</c>, which writes <c>tests/&lt;name&gt;.e2e.ts</c>).</summary>
/// <param name="Name">The file's name without its extension.</param>
/// <param name="Content">The file's text.</param>
internal sealed record ScaffoldTest(string Name, string Content);

/// <summary>What <see cref="Scaffold.CreateScaffold"/> composes: the packages, the text of <c>e2e.config.json</c>, and the example tests.</summary>
/// <param name="Dependencies">The package references to add to the test project, with their version ranges.</param>
/// <param name="Config">The config file's text.</param>
/// <param name="Tests">The example test files.</param>
/// <param name="NeedsAppUrl">Whether the example drives a URL the user's app must answer.</param>
internal sealed record ScaffoldResult(IReadOnlyList<KeyValuePair<string, string>> Dependencies, string Config, IReadOnlyList<ScaffoldTest> Tests, bool NeedsAppUrl);

/// <summary>
/// Upstream's <c>cli/init/scaffold.ts</c>. It writes <c>e2e.config.json</c> where upstream writes <c>e2e.config.ts</c>, a C#
/// example test where it writes <c>tests/example.e2e.ts</c>, and the package references of a <c>.csproj</c> where it writes
/// the dependencies of a <c>package.json</c>.
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

    /// <summary>The example of a config with no engine, which keeps the default web target: a request to the app, without a browser.</summary>
    private static string NoEngineExample(FrameworkPreset framework, string rootNamespace) => $$"""
        using {{framework.FrameworkUsing}};

        namespace {{rootNamespace}};

        public sealed class ExampleTests
        {
            [{{framework.TestAttribute}}]
            public async Task App_responds()
            {
                using var client = new HttpClient();
                using var response = await client.GetAsync(Environment.GetEnvironmentVariable("APP_URL") ?? "http://localhost:3000");
                {{string.Format(System.Globalization.CultureInfo.InvariantCulture, framework.TrueAssertion, "response.IsSuccessStatusCode")}};
            }
        }

        """;

    /// <summary>The example test of an engine: its test, and the commented agent example after it when a model is configured.</summary>
    private static string EngineExample(EnginePreset engine, FrameworkPreset framework, string rootNamespace, bool ai)
    {
        var example = engine.Example(framework);
        return $$"""
            using E2E;
            using {{example.Namespace}};
            using {{framework.FrameworkUsing}};

            namespace {{rootNamespace}};

            public sealed class ExampleTests : {{example.BaseClass}}
            {
            {{example.Test}}{{(ai ? "\n" + engine.AiExample(framework) : "")}}
            }

            """;
    }

    /// <summary>
    /// Composes the config from the selected engines (none for an engine-less config, which keeps the default web target)
    /// and the model, the package references for <paramref name="framework"/>, and the example test in
    /// <paramref name="rootNamespace"/>.
    /// </summary>
    public static ScaffoldResult CreateScaffold(IReadOnlyList<string> engineIds, ScaffoldModel? model, FrameworkPreset? framework = null, string rootNamespace = "E2E.Tests")
    {
        ArgumentNullException.ThrowIfNull(engineIds);
        framework ??= Frameworks.Get(Frameworks.DefaultFrameworkId);
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

        // The test framework's own packages first, as `dotnet new` lays them out, then the E2E packages at the version of this tool.
        var range = Versions.DependencyRange(Versions.RunnerVersion);
        IReadOnlyList<string> packages = engines.Count == 1 ? engines[0].Dependencies(framework) : ["E2E", "E2E." + framework.AdapterSuffix];
        List<KeyValuePair<string, string>> dependencies = [.. framework.Dependencies, .. packages.Select(name => new KeyValuePair<string, string>(name, range))];

        var content = engines.Count == 1 ? EngineExample(engines[0], framework, rootNamespace, model is not null) : NoEngineExample(framework, rootNamespace);
        return new ScaffoldResult(
            dependencies,
            config.ToJsonString(Indented) + "\n",
            [new ScaffoldTest("ExampleTests", content)],
            // An engine-less config's example requests APP_URL too.
            engines.Count == 0 || engines.Any(engine => engine.NeedsAppUrl));
    }
}
