// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.StepCache.StepCacheHarness;
using Recorder = E2E.Cache.TraceRecorder;

namespace E2E.Tests.StepCache;

/// <summary>
/// Upstream's <c>describe('cache.strict and a step whose key changed under its recording')</c> at the session seam. Not ported,
/// as the port has no such case: a secret masked in the claimed step (the act masks it before the
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
        Assert.Contains(
            "holds it under another cache key (" + OldKey + ".json)",
            error.Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "re-record it with a read-write run with cache.strict set to false",
            error.Message,
            StringComparison.Ordinal
        );
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
            MakeHost(["/"])
        );

        Assert.Null(await session.BeginAsync());
    }

    [Fact]
    public async Task Runs_live_when_the_only_recording_is_truncated_or_records_no_step()
    {
        Action<CacheEntry>[] changes =
        [
            entry =>
            {
                entry.Actions = [.. Enumerable.Range(0, 50).Select(_ => Tap("button", "Upgrade"))];
                entry.Truncated = true;
            },
            entry =>
            {
                entry.Test = null;
                entry.Instruction = null;
                entry.Engine = null;
                entry.ParamsDigest = null;
                entry.CallIndex = null;
                entry.Agent = null;
            },
        ];
        foreach (var change in changes)
        {
            var session = MakeSession(Rekeyed(change), MakeHost(["/"]));

            Assert.Null(await session.BeginAsync());
        }
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
    public async Task Runs_live_a_step_the_store_holds_no_recording_of_another_instruction_params_occurrence_agent_test_or_target(
        string change
    )
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
                        entry.ParamsDigest = CacheKeys.ParamsDigest(
                            new Dictionary<string, object?> { ["plan"] = "pro" }
                        );
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
            MakeHost(["/"])
        );

        Assert.Null(await session.BeginAsync());
    }

    private static readonly string Long = string.Concat(
        Enumerable.Repeat("checkout for a signed in customer with a saved card ", 6)
    );

    // The step as the session claims it, before redaction: the session records its provenance, as upstream's does.
    private static CacheEntry StepWith(string field, string value)
    {
        var step = ExampleStep;
        step.TargetId = "web";
        switch (field)
        {
            case "testId":
                step.Test = value;
                break;
            case "targetId":
                step.TargetId = value;
                break;
            default:
                step.Agent = value;
                break;
        }

        return step;
    }

    private static CacheEntry Stored(CacheEntry step) => Recorder.RecordedProvenance(step, text => text);

    private static string FieldOf(CacheEntry step, string field) =>
        (
            field switch
            {
                "testId" => step.Test,
                "targetId" => step.TargetId,
                _ => step.Agent,
            }
        )!;

    private static StepTraceSession SessionFor(CacheEntry stored, CacheEntry step) =>
        MakeSession(Rekeyed(entry => entry.SetStep(stored)), MakeHost(["/"]), step: step);

    [Theory]
    [InlineData("testId")]
    [InlineData("targetId")]
    [InlineData("agent")]
    public async Task Runs_live_when_only_another_step_that_starts_the_same_way_was_recorded(string field)
    {
        var session = SessionFor(Stored(StepWith(field, Long + " first")), StepWith(field, Long + " second"));

        Assert.Null(await session.BeginAsync());
        Assert.Equal(("missed", "no-entry"), (session.CacheInfo?.Mode, session.CacheInfo?.Reason));
    }

    [Theory]
    [InlineData("testId")]
    [InlineData("targetId")]
    [InlineData("agent")]
    public async Task Fails_with_REPLAY_STALE_when_its_own_recording_sits_under_another_key(string field)
    {
        var step = StepWith(field, Long + " first");
        Assert.Equal(Trace.MaxTraceDescriptorChars, FieldOf(Stored(step), field).Length);

        var error = await Assert.ThrowsAsync<AgentException>(SessionFor(Stored(step), step).BeginAsync);

        Assert.Equal("REPLAY_STALE", error.Code);
    }

    [Theory]
    [InlineData("testId")]
    [InlineData("targetId")]
    [InlineData("agent")]
    public async Task Runs_live_against_an_entry_that_cut_it_before_entries_kept_an_identity_digest(string field)
    {
        var step = StepWith(field, Long + " first");
        var legacy = Stored(step);
        legacy.IdentityDigest = null;

        Assert.Null(await SessionFor(legacy, step).BeginAsync());
    }

    [Theory]
    [InlineData("testId")]
    [InlineData("targetId")]
    [InlineData("agent")]
    public async Task Runs_live_when_its_whole_name_equals_the_cut_name_of_another_step_stored_without_an_identity_digest(
        string field
    )
    {
        var legacy = Stored(StepWith(field, Long + " first"));
        legacy.IdentityDigest = null;
        var step = StepWith(field, FieldOf(legacy, field));

        Assert.Null(await SessionFor(legacy, step).BeginAsync());
    }

    [Theory]
    [InlineData("testId")]
    [InlineData("targetId")]
    [InlineData("agent")]
    public async Task Fails_with_REPLAY_STALE_when_its_whole_name_ends_like_a_cut_and_its_own_recording_sits_under_another_key(
        string field
    )
    {
        foreach (var length in new[] { Trace.MaxTraceDescriptorChars - 1, Trace.MaxTraceDescriptorChars })
        {
            var step = StepWith(field, new string('x', length - 1) + "…");
            Assert.Matches("^[a-f0-9]{64}$", Stored(step).IdentityDigest);

            var error = await Assert.ThrowsAsync<AgentException>(SessionFor(Stored(step), step).BeginAsync);

            Assert.Equal("REPLAY_STALE", error.Code);
        }
    }

    [Theory]
    [InlineData("testId")]
    [InlineData("targetId")]
    [InlineData("agent")]
    public async Task Fails_with_REPLAY_STALE_at_exactly_300_characters_which_an_entry_keeps_whole(string field)
    {
        var step = StepWith(field, new string('x', Trace.MaxTraceDescriptorChars));
        Assert.Equal(new string('x', Trace.MaxTraceDescriptorChars), FieldOf(Stored(step), field));
        Assert.Null(Stored(step).IdentityDigest);

        var error = await Assert.ThrowsAsync<AgentException>(SessionFor(Stored(step), step).BeginAsync);

        Assert.Equal("REPLAY_STALE", error.Code);
    }
}
