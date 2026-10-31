// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Mobile.Tests.RawNodes;
using static E2E.Mobile.Tests.Screens;

namespace E2E.Mobile.Tests.Engine;

/// <summary>Upstream's <c>describe('input freshness')</c> of <c>engine.test.ts</c>; only the commit's three <c>it</c>s are here.</summary>
public sealed class InputFreshnessTests
{
    /// <summary>A uniquely identified field and its neighbor, as upstream's <c>form</c>, with the field's type and what it holds named.</summary>
    private static JsonObject Form(RawNode field, RawNode? inside = null)
    {
        var account = field with { Ref = "@e20", Index = 1, ParentIndex = 0, Depth = 1, Label = "Account", Identifier = "account", Rect = R(40, 237, 322, 49) };
        List<RawNode> nodes =
        [
            SettingsNodes[0],
            account,
            N("@e21", index: 2, parentIndex: 0, depth: 1, type: "text-field", label: "Username", identifier: "username", rect: R(40, 218, 322, 49)),
        ];
        if (inside is not null)
        {
            nodes.Add(inside);
        }

        return FakeDeviceClient.SnapshotOf(nodes);
    }

    // agent-device 0.21.33's simulator accessibility tree reads a Compose Multiplatform field as an unclassified
    // `Other`: an empty one over the placeholder it draws, a filled one with its text as the value.
    private static JsonObject Unclassified(string? value = null, RawNode? inside = null) => Form(N(type: "Other", value: value), inside);

    private static RawNode Child(string type, string label) =>
        N("@e22", index: 3, parentIndex: 1, depth: 2, type: type, label: label, rect: R(52, 252, 101, 18));

    private static LocatorAction Act(string kind) => kind == "fill" ? new LocatorAction.Fill("example") : new LocatorAction.Clear();

    public static TheoryData<ActionOrigin, string, string> Cases(params string[] names)
    {
        var data = new TheoryData<ActionOrigin, string, string>();
        foreach (var name in names)
        {
            foreach (var origin in new[] { ActionOrigin.Test, ActionOrigin.Agent })
            {
                foreach (var kind in new[] { "fill", "clear" })
                {
                    data.Add(origin, kind, name);
                }
            }
        }

        return data;
    }

    public static TheoryData<ActionOrigin, string, string> ActsCases() => Cases("empty, over its placeholder", "filled");

    public static TheoryData<ActionOrigin, string, string> RejectsCases() =>
        Cases("childless and valueless, like a React Native Pressable", "holding a control");

    [Theory]
    [MemberData(nameof(ActsCases))]
    public async Task Acts_on_an_unclassified_iOS_field(ActionOrigin origin, string kind, string field)
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        session.Origin = origin;
        var snapshot = field == "filled" ? Unclassified("Grace") : Unclassified(inside: Child("static-text", "Ada Lovelace"));
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => snapshot.DeepClone());
        var observed = await Observed(session, "Account");

        await session.PerformAsync(observed, Act(kind), CancellationToken.None);

        JsonAssert.Args(kind == "fill" ? """{"ref":"@e20","text":"example"}""" : """{"ref":"@e20","text":""}""", h.Fake.LastArgs("interactions.fill") is { } args ? Strip(args) : null);
    }

    [Theory]
    [MemberData(nameof(RejectsCases))]
    public async Task Rejects_an_unclassified_iOS_element(ActionOrigin origin, string kind, string element)
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        session.Origin = origin;
        var snapshot = element == "holding a control" ? Unclassified(inside: Child("button", "Clear")) : Unclassified();
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => snapshot.DeepClone());
        var observed = await Observed(session, "Account");

        var failure = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(observed, Act(kind), CancellationToken.None));

        Assert.Equal("UNSUPPORTED_CAPABILITY", failure.Code);
        Assert.False(failure.Retryable);
        Assert.DoesNotContain("interactions.fill", h.Fake.Methods());
    }

    [Theory]
    [MemberData(nameof(GainedCases))]
    public async Task Rejects_an_unclassified_iOS_element_that_gained_a_control_before_typing(ActionOrigin origin, string kind)
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        session.Origin = origin;
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Unclassified(inside: Child("static-text", "Loading")));
        var observed = await Observed(session, "Account");
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Unclassified(inside: Child("button", "Loading")));

        var failure = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(observed, Act(kind), CancellationToken.None));

        Assert.Equal("UNSUPPORTED_CAPABILITY", failure.Code);
        Assert.False(failure.Retryable);
        Assert.DoesNotContain("interactions.fill", h.Fake.Methods());
    }

    public static TheoryData<ActionOrigin, string> GainedCases()
    {
        var data = new TheoryData<ActionOrigin, string>();
        foreach (var origin in new[] { ActionOrigin.Test, ActionOrigin.Agent })
        {
            foreach (var kind in new[] { "fill", "clear" })
            {
                data.Add(origin, kind);
            }
        }

        return data;
    }

    /// <summary>The ref and text of a fill call, the way upstream's <c>toMatchObject</c> reads them: settle options are not part of the assertion.</summary>
    private static JsonObject Strip(JsonObject args) => new() { ["ref"] = args["ref"]?.DeepClone(), ["text"] = args["text"]?.DeepClone() };
}
