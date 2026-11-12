// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TraceCacheReplay;

// Upstream's trace-cache-replay.test.ts, describe 'trace cache: two targets keep separate entries'.
// The port has no runner, so each target is one session over the shared cache directory.
public sealed class TraceCacheTwoTargetsKeepSeparateEntriesTests
{
    [Fact]
    public async Task Records_one_entry_per_target_and_lists_both()
    {
        var directory = ConfigTests.TempCache();
        foreach (var target in new[] { "web", "web-b" })
        {
            var (result, calls) = await TraceCacheReplayHarness.RunTargetAsync(directory, target);
            Assert.True(calls > 0);
            Assert.Equal("missed", result.Cache?.Mode);
            Assert.Equal("no-entry", result.Cache?.Reason);
        }

        var recorded = Directory.GetFiles(directory)
            .Select(path => TargetOf(File.ReadAllText(path)))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(["web", "web-b"], recorded);
        var listing = CacheLs(directory);
        Assert.Matches(@"billing > upgrades\s+web\s+", listing);
        Assert.Matches(@"billing > upgrades\s+web-b\s+", listing);
    }

    /// <summary>Upstream's <c>cacheCli(project, 'ls')</c>: <c>e2e cache ls</c> over the cache directory, from a project whose config names no other.</summary>
    private static string CacheLs(string directory)
    {
        var project = Directory.CreateTempSubdirectory("e2e-cache-ls-").FullName;
        try
        {
            var config = Path.Combine(project, "e2e.config.json");
            File.WriteAllText(config, """{ "targets": [{ "name": "web", "platform": "web", "app": { "url": "http://127.0.0.1:3000" } }] }""");
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var code = E2E.Cli.CacheCommand.Run(["ls", "--config", config, "--cache-dir", directory], stdout, stderr, interactive: false, project);
            Assert.Equal(0, code);
            return stdout.ToString();
        }
        finally
        {
            Directory.Delete(project, recursive: true);
        }
    }

    private static string TargetOf(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("targetId", out var target) ? target.GetString() ?? "" : "";
    }

    [Fact]
    public async Task Replays_on_both_targets_without_a_model_call()
    {
        var directory = ConfigTests.TempCache();
        foreach (var target in new[] { "web", "web-b" })
        {
            await TraceCacheReplayHarness.RunTargetAsync(directory, target);
        }

        foreach (var target in new[] { "web", "web-b" })
        {
            var (step, calls) = await TraceCacheReplayHarness.RunTargetAsync(directory, target);
            Assert.Equal(0, calls);
            Assert.Equal("self-finalized", step.Cache?.Mode);
            Assert.Equal(0, step.ModelCalls);
            Assert.Equal(1, step.Actions);
        }

        Assert.Equal(2, Directory.GetFiles(directory).Length);
    }
}
