// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.CacheModelProvenance;

namespace E2E.Tests.StepCache;

/// <summary>
/// "says a confirmed recording the store did not write was not written, rather than saved" runs <c>E2ESession.Flush</c> (upstream's <c>settleStagedTraces</c>) over a file store with a 16-byte cap.
/// </summary>
public sealed class FlushPipelineTests
{
    [Fact]
    public async Task Leaves_an_entry_the_same_flow_re_recorded_untouched_and_replaces_it_when_the_actions_change_through_the_pipeline()
    {
        var directory = BillingSession.TempCache();
        await using (var recording = await BillingSession.StartAsync(directory, new NamedModel(BillingSession.UpgradeModel(() => { }), "test/A"), attempt: 1))
        {
            await BillingSession.UpgradeAsync(recording);
            recording.Complete();
        }

        var file = Assert.Single(BillingSession.Entries(directory));
        var written = await File.ReadAllTextAsync(file);
        Assert.Contains("\"recordedBy\": \"test/A\"", written, StringComparison.Ordinal);
        var stamp = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(file, stamp);

        // A retry under another model runs live and records the same flow again: nothing a replay reads changed.
        await using (var session = await BillingSession.StartAsync(directory, new NamedModel(BillingSession.UpgradeModel(() => { }), "test/B"), attempt: 2))
        {
            await BillingSession.UpgradeAsync(session);
            session.Complete();
        }

        Assert.Equal(stamp, File.GetLastWriteTimeUtc(file));
        Assert.Equal(written, await File.ReadAllTextAsync(file));

        // One more tap is a different flow, and the entry follows it.
        await using (var session = await BillingSession.StartAsync(directory, TapsTwice(), attempt: 2))
        {
            await BillingSession.UpgradeAsync(session);
            session.Complete();
        }

        var replaced = await File.ReadAllTextAsync(Assert.Single(BillingSession.Entries(directory)));
        Assert.NotEqual(written, replaced);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Count(replaced, "\"kind\":\\s*\"tap\""));
    }

    [Fact]
    public async Task Leaves_an_entry_untouched_when_only_the_rule_that_flagged_a_typed_value_differs()
    {
        var directory = BillingSession.TempCache();
        var key = new string('d', 64);

        CacheEntry Recording(DerivedReason? derived) => new()
        {
            Test = "tests/a.e2e.ts::a",
            Instruction = "apply the coupon",
            Engine = "web",
            ParamsDigest = new string('d', 64),
            CallIndex = 0,
            Agent = "default",
            Route = "/e/iframe-form",
            EndRoute = "/e/iframe-form",
            Actions =
            [
                new RecordedAction { Kind = "tool", Text = "type (run-time value)", Derived = derived },
                new RecordedAction { Kind = "tap", Role = "button", Name = "Apply" },
            ],
        };

        void Flush(DerivedReason? derived) => E2ESession.Flush(
            new FileStepCache(directory, FileStepCache.MaxCacheWireBytes, true),
            [new PendingAct { Key = key, StepIndex = 0, Completed = true, Verified = true, Entry = Recording(derived) }],
            preserve: false);

        // An entry recorded before gaps carried their rule, then live runs whose agent read the value off pixels once and off a node
        // the next time: a replay stops at the gap whatever the rule, so none of them is a new flow.
        Flush(null);
        var file = Assert.Single(BillingSession.Entries(directory));
        var written = await File.ReadAllTextAsync(file);
        Flush(DerivedReason.Pixels);
        Flush(DerivedReason.MintedToken);

        Assert.Equal(written, await File.ReadAllTextAsync(file));
    }

    [Fact]
    public void Says_a_confirmed_recording_the_store_did_not_write_was_not_written_rather_than_saved()
    {
        var directory = BillingSession.TempCache();
        var key = new string('e', 64);
        var recording = new CacheEntry
        {
            Test = "tests/a.e2e.ts::a",
            Instruction = "tap Save",
            Engine = "web",
            ParamsDigest = new string('e', 64),
            CallIndex = 0,
            Agent = "default",
            Route = "/",
            EndRoute = "/",
            Actions = [new RecordedAction { Kind = "tap", Role = "button", Name = "Save" }],
        };

        CacheWrite? Outcome(int maxBytes) => E2ESession
            .Flush(new FileStepCache(directory, maxBytes, true), [new PendingAct { Key = key, StepIndex = 0, Completed = true, Verified = true, Entry = recording }], preserve: false)
            .GetValueOrDefault(0);

        Assert.Equal(CacheWrite.NotWritten, Outcome(16));
        Assert.Equal(CacheWrite.Saved, Outcome(FileStepCache.MaxCacheWireBytes));
    }

    /// <summary>A model that taps Upgrade to Pro twice before it concludes, and passes the judgment.</summary>
    private static ScriptedModel TapsTwice()
    {
        return new ScriptedModel(request =>
        {
            var text = string.Join('\n', request.Messages.Select(message => message.Content));
            if (text.Contains("Statement:", StringComparison.Ordinal))
            {
                return ModelResponses.Done("passed", "The invoice is prorated.");
            }

            var taps = request.Messages.Count(message => message.Role == "tool" && (message.Content ?? "").StartsWith("Tapped", StringComparison.Ordinal));
            return taps >= 2 ? ModelResponses.Done("passed", "Upgraded to Pro.") : ModelResponses.Tap("button", "Upgrade to Pro");
        });
    }
}
