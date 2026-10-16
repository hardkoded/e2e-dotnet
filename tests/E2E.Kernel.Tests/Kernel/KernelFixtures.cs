// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Kernel.Tests.Kernel;

/// <summary>What the tests of <see cref="KernelProvider"/> share: the run's environment, a request, a release context, and a provider over a <see cref="FakeKernel"/>.</summary>
internal static class KernelFixtures
{
    public static readonly IReadOnlyDictionary<string, string?> Env = new Dictionary<string, string?> { ["KERNEL_API_KEY"] = "k-test" };

    public static BrowserRequest Request(List<string>? lines = null, int slot = 0, string? attemptId = null, IReadOnlyDictionary<string, string?>? env = null) => new()
    {
        RunId = "run-1",
        TargetName = "web",
        Slot = slot,
        Slots = 2,
        AttemptId = attemptId,
        Env = env ?? Env,
        Log = line => lines?.Add(line),
    };

    public static BrowserReleaseContext ReleaseContext() => new() { RunId = "run-1", TargetName = "web", Env = Env, Log = _ => { } };

    public static BrowserLease Lease(string id = "b1") => new() { Id = id, CdpEndpoint = $"wss://kernel/{id}" };

    public static KernelProvider Provider(FakeKernel fake, KernelOptions? options = null) => new(options, fake.Client);

    /// <summary>Asserts two JSON values are equal, naming both when they are not.</summary>
    public static void AssertJson(JsonNode expected, JsonNode? actual) =>
        Assert.True(JsonNode.DeepEquals(expected, actual), $"expected {expected.ToJsonString()} but was {actual?.ToJsonString()}");
}
