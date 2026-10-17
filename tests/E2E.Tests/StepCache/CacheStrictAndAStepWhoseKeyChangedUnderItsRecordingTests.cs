// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.StepCache.StepCacheHarness;

namespace E2E.Tests.StepCache;

/// <summary>
/// Upstream's <c>describe('cache.strict and a step whose key changed under its recording')</c> at the session seam. Not ported,
/// as the port has no such case: a truncated recording, and a secret masked in the claimed step (the act masks it before the
/// session sees it, which the pipeline version covers).
/// </summary>
public sealed class CacheStrictAndAStepWhoseKeyChangedUnderItsRecordingTests
{
    private const string OldKey = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    // A file store holding one entry under the old key, and a context whose own key finds nothing in it.
    private static CacheContext Rekeyed(Action<CacheEntry>? change = null, bool strict = true)
    {
        var store = new FileStepCache(CoreTests.TempCache());
        var entry = new CacheEntry
        {
            Schema = FileStepCache.SchemaVersion,
            Actions = [Navigate("/customers")],
            Appeared = [],
            Gone = [],
        };
        entry.SetStep(ExampleStep);
        change?.Invoke(entry);
        store.Write(OldKey, entry);
        return new CacheContext(store) { Write = false, Strict = strict };
    }

    [Fact]
    public async Task Fails_with_REPLAY_STALE_naming_the_old_entry_instead_of_running_the_step_live()
    {
        var session = MakeSession(Rekeyed(), MakeHost(["/"]));

        var error = await Assert.ThrowsAsync<AgentException>(session.BeginAsync);

        Assert.Equal("REPLAY_STALE", error.Code);
        Assert.Contains("sits under another cache key (" + OldKey + ".json)", error.Message, StringComparison.Ordinal);
        Assert.EndsWith("re-record it without cache.strict.", error.Message, StringComparison.Ordinal);
        Assert.Equal(("missed", "no-entry"), (session.CacheInfo?.Mode, session.CacheInfo?.Reason));
    }

    [Fact]
    public async Task Runs_live_against_an_entry_recorded_before_the_occurrence_fields_were_which_could_be_another_call_of_the_instruction()
    {
        var session = MakeSession(
            Rekeyed(entry =>
            {
                entry.Engine = null;
                entry.ParamsDigest = null;
                entry.CallIndex = null;
                entry.Agent = null;
            }),
            MakeHost(["/"]));

        Assert.Null(await session.BeginAsync());
    }

    [Fact]
    public async Task Runs_the_step_live_without_cache_strict()
    {
        var session = MakeSession(Rekeyed(strict: false), MakeHost(["/"]));

        Assert.Null(await session.BeginAsync());

        Assert.Equal(("missed", "no-entry"), (session.CacheInfo?.Mode, session.CacheInfo?.Reason));
    }

    [Theory]
    [InlineData("instruction")]
    [InlineData("params")]
    [InlineData("occurrence")]
    [InlineData("agent")]
    [InlineData("test")]
    [InlineData("target")]
    public async Task Runs_live_a_step_the_store_holds_no_recording_of_another_instruction_params_occurrence_agent_test_or_target(string change)
    {
        var session = MakeSession(
            Rekeyed(entry =>
            {
                switch (change)
                {
                    case "instruction":
                        entry.Instruction = "open the billing page";
                        break;
                    case "params":
                        entry.ParamsDigest = CacheKeys.ParamsDigest(new Dictionary<string, object?> { ["plan"] = "pro" });
                        break;
                    case "occurrence":
                        entry.CallIndex = 1;
                        break;
                    case "agent":
                        entry.Agent = "admin";
                        break;
                    case "test":
                        entry.Test = "tests/example > other";
                        break;
                    case "target":
                        entry.Engine = "ios";
                        break;
                }
            }),
            MakeHost(["/"]));

        Assert.Null(await session.BeginAsync());
    }
}
