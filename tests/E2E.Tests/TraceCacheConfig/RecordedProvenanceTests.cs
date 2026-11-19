// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using Recorder = E2E.Cache.TraceRecorder;

namespace E2E.Tests.TraceCacheConfig;

/// <summary>Upstream's <c>describe('recorded provenance')</c>, over <see cref="Recorder.RecordedProvenance"/>.</summary>
public sealed class RecordedProvenanceTests
{
    private static CacheEntry Step() =>
        new()
        {
            Test = "tests/a.e2e.ts::a",
            TargetId = "web",
            Instruction = "open billing",
            Engine = "web",
            ParamsDigest = new string('e', 64),
            CallIndex = 0,
            Agent = "default",
        };

    private static Redactor Secret(string value) => Redactor.For([("password", value)]);

    [Fact]
    public void Takes_the_identity_digest_over_the_redacted_names_so_it_never_depends_on_a_secret_value()
    {
        var longName = Step().Test + new string('x', 400);
        string? DigestWith(string value)
        {
            var step = Step();
            step.Test = longName + " as " + value;
            return Recorder.RecordedProvenance(step, Secret(value).Redact).IdentityDigest;
        }

        Assert.Matches("^[a-f0-9]{64}$", DigestWith("hunter2"));
        Assert.Equal(DigestWith("hunter2"), DigestWith("swordfish"));
    }

    [Theory]
    [InlineData("targetId")]
    [InlineData("agent")]
    public void Stores_the_field_redacted_and_takes_the_digest_over_it_redacted(string field)
    {
        CacheEntry Recorded(string value)
        {
            var step = Step();
            step.Test += new string('x', 400);
            if (field == "targetId")
            {
                step.TargetId = "staging-" + value;
            }
            else
            {
                step.Agent = "staging-" + value;
            }

            return Recorder.RecordedProvenance(step, Secret(value).Redact);
        }

        var recorded = Recorded("hunter2");
        Assert.DoesNotContain(
            "hunter2",
            field == "targetId" ? recorded.TargetId : recorded.Agent,
            StringComparison.Ordinal
        );
        Assert.Matches("^[a-f0-9]{64}$", recorded.IdentityDigest);
        Assert.Equal(recorded.IdentityDigest, Recorded("swordfish").IdentityDigest);
    }

    [Fact]
    public void Measures_the_redacted_names_so_a_name_a_secret_makes_longer_than_an_entry_keeps_gets_a_digest()
    {
        var secrets = Secret("hunter2");

        // Under 300 characters as written, over 300 once `hunter2` is redacted.
        CacheEntry Variant(string title)
        {
            var step = Step();
            step.Test = new string('x', 280) + " hunter2 " + title;
            return Recorder.RecordedProvenance(step, secrets.Redact);
        }

        var first = Variant("first");
        var second = Variant("second");

        Assert.Equal(first.Test, second.Test);
        Assert.Matches("^[a-f0-9]{64}$", first.IdentityDigest);
        Assert.Matches("^[a-f0-9]{64}$", second.IdentityDigest);
        Assert.NotEqual(first.IdentityDigest, second.IdentityDigest);
    }
}
