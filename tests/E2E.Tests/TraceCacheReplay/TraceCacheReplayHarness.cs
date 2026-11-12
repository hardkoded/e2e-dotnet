// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TraceCacheReplay;

/// <summary>
/// .NET-only: the port has no runner, so the two-targets and repeat-each describes of upstream's
/// <c>trace-cache-replay.test.ts</c> run one test body per session over a shared cache directory. Each body is the
/// billing flow's one act step, in the session <see cref="ConfigTests.StartAsync"/> opens.
/// </summary>
internal static class TraceCacheReplayHarness
{
    /// <summary>Runs the upgrade step once on the named target and returns the act's result and the model calls it made.</summary>
    public static async Task<(ActResult Result, int ModelCalls)> RunTargetAsync(string directory, string? target)
    {
        var calls = 0;
        await using var session = await ConfigTests.StartAsync(directory, CacheMode.ReadWrite, strict: false, "Upgrade to Pro", () => calls++, targetName: target);
        await session.App.OpenAsync("/settings/billing");
        var result = await session.Agent.ActAsync("upgrade the workspace to the Pro plan");
        await session.Agent.AssertAsync("the invoice preview shows a prorated amount");
        session.Complete();
        return (result, calls);
    }
}
