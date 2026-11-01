// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Mobile.Tests.RawNodes;
using static E2E.Mobile.Tests.Screens;

namespace E2E.Mobile.Tests.Engine;

/// <summary>Upstream's <c>describe('input freshness')</c> of <c>engine.test.ts</c>, as of tester-army/e2e#1410 and #1427.</summary>
public sealed class InputFreshnessTests
{
    /// <summary>Upstream's <c>form(y, changes)</c>: the secure field <c>Account</c> and its neighbor, after a slow accessibility capture.</summary>
    private static JsonObject Account(double y = 237, Func<RawNode, RawNode>? change = null, params (string Key, JsonNode? Value)[] more)
    {
        var field = N("@e20", index: 1, parentIndex: 0, depth: 1, type: "secure-text-field", label: "Account", identifier: "account", rect: R(40, y, 322, 49));
        List<RawNode> nodes =
        [
            SettingsNodes[0],
            change?.Invoke(field) ?? field,
            N("@e21", index: 2, parentIndex: 0, depth: 1, type: "text-field", label: "Username", identifier: "username", rect: R(40, 218, 322, 49)),
        ];
        return FakeDeviceClient.SnapshotOf(nodes, more);
    }

    private static Harness WithTransition(int milliseconds) =>
        Harness.Create(options => options with { TransitionMilliseconds = milliseconds });

    private static async Task<DeviceSession> OpenAs(Harness h, ActionOrigin origin, TimeSpan? actionTimeout = null)
    {
        var session = await OpenAttempt(h, actionTimeout: actionTimeout);
        session.Origin = origin;
        return session;
    }

    private static int Snapshots(Harness h) => h.Fake.Methods().Count(method => method == "capture.snapshot");

    private static int Fills(Harness h) => h.Fake.Methods().Count(method => method == "interactions.fill");

    private static double Waited(Harness h, int since) => h.Delays.Skip(since).Sum(delay => delay.TotalMilliseconds);

    private static LocatorAction Typing(string kind, bool sensitive) => kind == "fill" ? new LocatorAction.Fill("example", sensitive) : new LocatorAction.Clear();

    private static string Fill(string @ref, string text) => $$"""{"ref":"{{@ref}}","text":"{{text}}"}""";

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

    [Theory]
    [MemberData(nameof(GainedCases))]
    public async Task Uses_one_fresh_binding_after_a_slow_capture_for_origin_and_kind(ActionOrigin origin, string kind)
    {
        var h = Harness.Create();
        var session = await OpenAs(h, origin);
        h.Fake.RespondAsync(DeviceMethods.CaptureSnapshot, (_, _) =>
        {
            h.Clock.Spend(TimeSpan.FromMilliseconds(600));
            return Task.FromResult<JsonNode?>(Account());
        });
        var old = await Observed(session, "Account");
        var captures = Snapshots(h);
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Account(126, field => field with { Ref = "@e42" }));

        await session.PerformAsync(old, Typing(kind, sensitive: true), CancellationToken.None);

        JsonAssert.Args(Fill("@e42", kind == "fill" ? "example" : ""), Strip(h.Fake.LastArgs("interactions.fill")!));
        Assert.Equal(captures + 1, Snapshots(h));
        Assert.Equal(1, Fills(h));
    }

    [Theory]
    [MemberData(nameof(GainedCases))]
    public async Task Acts_on_a_sliding_input_with_one_capture_for_origin_and_kind(ActionOrigin origin, string kind)
    {
        var h = Harness.Create();
        var session = await OpenAs(h, origin);
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Account(2000));
        var sliding = await Observed(session, "Account");
        var captures = Snapshots(h);
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Account(126, field => field with { Ref = "@e42" }));
        var delays = h.Delays.Count;

        await session.PerformAsync(sliding, Typing(kind, sensitive: false), CancellationToken.None);

        JsonAssert.Args(Fill("@e42", kind == "fill" ? "example" : ""), Strip(h.Fake.LastArgs("interactions.fill")!));
        Assert.Equal(captures + 1, Snapshots(h));
        if (origin == ActionOrigin.Test)
        {
            Assert.True(Waited(h, delays) >= 500);
        }
    }

    [Theory]
    [MemberData(nameof(GainedCases))]
    public async Task Rejects_an_originally_non_input_target_before_waiting_or_capturing_for_origin_and_kind(ActionOrigin origin, string kind)
    {
        var h = Harness.Create();
        var session = await OpenAs(h, origin);
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Account(237, field => field with { Type = "button" }));
        var button = await Observed(session, "Account");
        var captures = Snapshots(h);
        var delays = h.Delays.Count;

        var failure = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(button, Typing(kind, sensitive: false), CancellationToken.None));

        Assert.Equal("UNSUPPORTED_CAPABILITY", failure.Code);
        Assert.False(failure.Retryable);
        Assert.Equal(delays, h.Delays.Count);
        Assert.Equal(captures, Snapshots(h));
        Assert.DoesNotContain("interactions.fill", h.Fake.Methods());
    }

    [Fact]
    public async Task Refreshes_an_unchanged_input_even_when_the_transition_shortcut_skips_the_wait()
    {
        var h = Harness.Create();
        var session = await OpenAs(h, ActionOrigin.Test);
        await session.PerformAsync(await Observed(session, "Back"), new LocatorAction.Tap(), CancellationToken.None);
        var search = await Observed(session, "Search");
        var captures = Snapshots(h);
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SnapshotOf(SettingsNodes.Select(node => node.Label == "Search" ? node with { Ref = "@e42" } : node)));
        var delays = h.Delays.Count;

        await session.PerformAsync(search, new LocatorAction.Clear(), CancellationToken.None);

        Assert.Equal(delays, h.Delays.Count);
        JsonAssert.Args(Fill("@e42", ""), Strip(h.Fake.LastArgs("interactions.fill")!));
        Assert.Equal(captures + 1, Snapshots(h));
    }

    public static TheoryData<string> ChangedTargets() => new("missing", "renamed", "different identifier", "truncated", "non-editable", "no longer secure", "disabled", "hidden");

    private static JsonObject Changed(string name) => name switch
    {
        "missing" => FakeDeviceClient.SnapshotOf([SettingsNodes[0]]),
        "renamed" => Account(237, field => field with { Label = "Other account" }),
        "different identifier" => Account(237, field => field with { Identifier = "other" }),
        "truncated" => Account(237, null, ("truncated", true)),
        "non-editable" => Account(237, field => field with { Type = "button" }),
        "no longer secure" => Account(237, field => field with { Type = "text-field" }),
        "disabled" => Account(237, field => field with { Enabled = false }),
        _ => Account(237, field => field with { VisibleToUser = false }),
    };

    [Theory]
    [MemberData(nameof(ChangedTargets))]
    public async Task Rejects_a_target_without_dispatching_or_exposing_the_value(string name)
    {
        var h = WithTransition(0);
        var session = await OpenAttempt(h);
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Account());
        var old = await Observed(session, "Account");
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Changed(name));

        var failure = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(old, new LocatorAction.Fill("not-for-diagnostics", Sensitive: true), CancellationToken.None));

        Assert.Equal("NODE_STALE", failure.Code);
        Assert.True(failure.Retryable);
        Assert.DoesNotContain("not-for-diagnostics", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("interactions.fill", h.Fake.Methods());
    }

    public static TheoryData<string, int> OtpCases() => new() { { "fill", 0 }, { "fill", 1 }, { "clear", 0 }, { "clear", 1 } };

    [Theory]
    [MemberData(nameof(OtpCases))]
    public async Task Can_act_on_identical_OTP_input_with_the_existing_nearest_node_matching(string kind, int selected)
    {
        var h = Harness.Create();
        var session = await OpenAs(h, ActionOrigin.Test);
        static JsonObject Digits(string[] refs, double shift) => FakeDeviceClient.SnapshotOf(
        [
            SettingsNodes[0],
            .. refs.Select((@ref, index) => N(@ref, index: index + 1, parentIndex: 0, depth: 1, type: "text-field", label: "Digit", rect: R(40, 100 + (index * 100) + shift, 49, 49))),
        ]);
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Digits(["@e20", "@e21"], 0));
        var target = Walk(await RootOf(session)).Where(node => node.Name == "Digit").ElementAt(selected);
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Digits(["@e41", "@e42"], 10));
        var captures = Snapshots(h);

        await session.PerformAsync(target, Typing(kind, sensitive: false), CancellationToken.None);

        Assert.Equal(selected == 0 ? "@e41" : "@e42", h.Fake.LastArgs("interactions.fill")!["ref"]!.GetValue<string>());
        Assert.Equal(captures + 1, Snapshots(h));
    }

    [Fact]
    public async Task Recovers_from_a_truncated_capture_after_re_observation()
    {
        var h = WithTransition(0);
        var session = await OpenAttempt(h);
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Account());
        var old = await Observed(session, "Account");
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Account(237, null, ("truncated", true)));

        var failure = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(old, new LocatorAction.Clear(), CancellationToken.None));

        Assert.Equal("NODE_STALE", failure.Code);
        Assert.True(failure.Retryable);
        Assert.DoesNotContain("interactions.fill", h.Fake.Methods());
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Account());
        await session.PerformAsync(await Observed(session, "Account"), new LocatorAction.Clear(), CancellationToken.None);
        Assert.Equal(1, Fills(h));
    }

    [Fact]
    public async Task Uses_the_newest_backend_ref_for_an_unchanged_uniquely_identified_input_without_taking_pixels()
    {
        var h = WithTransition(0);
        var session = await OpenAttempt(h);
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Account());
        var old = await Observed(session, "Account");
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Account(237, field => field with { Ref = "@e42", Focused = true }));

        await session.PerformAsync(old, new LocatorAction.Fill("example", Sensitive: true), CancellationToken.None);

        JsonAssert.Args(Fill("@e42", "example"), Strip(h.Fake.LastArgs("interactions.fill")!));
        Assert.DoesNotContain("capture.screenshot", h.Fake.Methods());
        JsonAssert.Args("""{"interactiveOnly":false}""", h.Fake.LastArgs("capture.snapshot"));
    }

    [Fact]
    public async Task Does_not_silently_accept_changed_security_facts_during_a_transition()
    {
        var h = Harness.Create();
        var session = await OpenAs(h, ActionOrigin.Test);
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Account());
        var old = await Observed(session, "Account");
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Account(126, field => field with { Enabled = false }));
        var captures = Snapshots(h);

        var failure = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(old, new LocatorAction.Fill("example", Sensitive: true), CancellationToken.None));

        Assert.Equal("NODE_STALE", failure.Code);
        Assert.DoesNotContain("interactions.fill", h.Fake.Methods());
        Assert.Equal(captures + 1, Snapshots(h));
    }

    [Fact]
    public async Task Does_not_send_input_if_the_preflight_consumed_the_input_budget()
    {
        var h = WithTransition(0);
        var session = await OpenAttempt(h, actionTimeout: TimeSpan.FromMilliseconds(500));
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Account());
        var old = await Observed(session, "Account");
        h.Fake.RespondAsync(DeviceMethods.CaptureSnapshot, (_, _) =>
        {
            h.Clock.Spend(TimeSpan.FromMilliseconds(600));
            return Task.FromResult<JsonNode?>(Account());
        });

        var failure = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(old, new LocatorAction.Clear(), CancellationToken.None));

        Assert.Equal("OPERATION_TIMEOUT", failure.Code);
        Assert.DoesNotContain("interactions.fill", h.Fake.Methods());
    }

    [Theory]
    [InlineData("fill")]
    [InlineData("clear")]
    public async Task Does_not_capture_or_send_after_its_transition_wait_is_cancelled(string kind)
    {
        var h = WithTransition(300);
        h.Waits.Wait = (_, token) => Task.Delay(Timeout.Infinite, token);
        var session = await OpenAs(h, ActionOrigin.Test);
        var search = await Observed(session, "Search");
        var captures = Snapshots(h);
        using var controller = new CancellationTokenSource();
        controller.CancelAfter(TimeSpan.FromMilliseconds(20));

        var cancelled = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(search, Typing(kind, sensitive: false), controller.Token));

        Assert.Equal("CANCELLED", cancelled.Code);
        Assert.Equal(captures, Snapshots(h));
        Assert.DoesNotContain("interactions.fill", h.Fake.Methods());
    }

    [Fact]
    public async Task Does_not_dispatch_when_aborted_during_the_preflight_capture()
    {
        var h = WithTransition(0);
        var session = await OpenAttempt(h);
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Account());
        var old = await Observed(session, "Account");
        using var controller = new CancellationTokenSource();
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
        {
            controller.Cancel();
            return Account();
        });

        var cancelled = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(old, new LocatorAction.Clear(), controller.Token));

        Assert.Equal("CANCELLED", cancelled.Code);
        Assert.DoesNotContain("interactions.fill", h.Fake.Methods());
    }

    /// <summary>The ref and text of a fill call, the way upstream's <c>toMatchObject</c> reads them: settle options are not part of the assertion.</summary>
    private static JsonObject Strip(JsonObject args) => new() { ["ref"] = args["ref"]?.DeepClone(), ["text"] = args["text"]?.DeepClone() };
}
