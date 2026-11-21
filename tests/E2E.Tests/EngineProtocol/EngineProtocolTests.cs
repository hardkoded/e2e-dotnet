// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Tests.EngineProtocol;

/// <summary>
/// The <c>it</c> of upstream's <c>describe('engine protocol')</c> (<c>packages/e2e/tests/unit/engine-protocol.test.ts</c>) that tester-army/e2e#1633
/// adds or changes. The port has no engine protocol (no JSON-RPC client, <c>describeEngine</c>, or loopback transport), so both are
/// <c>Partial_</c>: they check what the port has, the manifest the author declares and the published manifest schema.
/// </summary>
public sealed class EngineProtocolTests
{
    private static readonly JsonSchemaCheck Validate = JsonSchemaCheck.FromFile("engine-manifest-v1.schema.json");

    /// <summary>
    /// Partial: upstream sends the read through a loopback JSON-RPC pair and reads the values on the other side. The port checks that an
    /// engine declaring <c>readStyle</c> has the <c>computed-style</c> capability, and that one without <c>observe</c> is refused.
    /// </summary>
    [Fact]
    public void Partial_Carries_a_read_request_and_computed_values_through_JSON_RPC()
    {
        Func<NodeRef, IReadOnlyList<string>, OperationContext, Task<IReadOnlyDictionary<string, string>>> read = (
            _,
            _,
            _
        ) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string> { ["color"] = "rgb(255, 0, 0)" }
            );
        var toy = new EngineSpec
        {
            Name = "toy",
            Version = "1",
            SpiVersion = 1,
            Observe = (_, _) => Task.FromResult<EngineSnapshot>(null!),
            ReadStyle = read,
        };

        var handle = EngineManifest.DefineEngine(toy);

        Assert.Contains("computed-style", handle.Capabilities);
        Assert.Same(read, handle.ReadStyle);
        var refused = Assert.Throws<ConfigurationException>(() =>
            EngineManifest.DefineEngine(
                new EngineSpec
                {
                    Name = "bad",
                    Version = "1",
                    SpiVersion = 1,
                    ReadStyle = read,
                }
            )
        );
        Assert.Contains("readStyle without observe", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Partial: upstream also describes a running engine and checks that the manifest it produces validates. The port has no
    /// <c>describeEngine</c>; it checks the manifests the schema refuses.
    /// </summary>
    [Fact]
    public void Partial_Describes_an_engine_with_a_manifest_the_published_schema_accepts_and_the_schema_names_every_method_and_kind()
    {
        var schema = ReportSchema.ReadJson("engine-manifest-v1.schema.json");
        Assert.Equal(
            EngineActionKinds.KnownLocator,
            schema["properties"]!["actions"]!["items"]!["enum"]!.AsArray().Select(kind => kind!.GetValue<string>())
        );
        Assert.Equal(
            EngineActionKinds.Pointer,
            schema["properties"]!["pointerActions"]!["items"]!["enum"]!.AsArray().Select(kind => kind!.GetValue<string>())
        );
        Assert.True(
            Validate.IsValid(
                JsonNode.Parse(
                    """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["engine/observe", "engine/perform", "engine/performAt"], "actions": ["tap", "blur"], "pointerActions": ["tap"] }"""
                )!
            )
        );

        foreach (
            var refused in new[]
            {
                """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["engine/observe", "engine/teleport"] }""",
                """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["engine/readStyle"] }""",
                """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["engine/observe"], "actions": ["tap"] }""",
                """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["engine/observe"], "tapModifiers": true }""",
                """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["engine/observe"], "pointerActions": ["tap"] }""",
                """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["engine/locate"] }""",
                """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["engine/observe", "keyboard/type"] }""",
                """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["state/capture"] }""",
                """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["artifacts/screenshot", "artifacts/startVideo"] }""",
            }
        )
        {
            Assert.False(Validate.IsValid(JsonNode.Parse(refused)!), refused);
        }
    }
}
