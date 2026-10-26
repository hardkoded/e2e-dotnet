// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace E2E.Tests.TraceIdentity;

/// <summary>Upstream's <c>describe('traceCacheKeyHash')</c> in <c>trace-identity.test.ts</c>.</summary>
public sealed partial class TraceCacheKeyHashTests
{
    private static readonly CacheTargetIdentity Target = new("web", "web", "playwright", 1, new string('a', 64));

    private static readonly TraceCacheKeyParts Parts = new(
        new string('p', 64),
        "billing upgrade",
        Target,
        Identity.TraceCallSignature("act", "open billing", null),
        0,
        new CacheAgentIdentity("default", "The billing period is Monthly."),
        "replay-policy/0");

    [GeneratedRegex(@"^[a-f0-9]{64}\z")]
    private static partial Regex Sha256Hex();

    [Fact]
    public void Is_stable_for_identical_parts()
    {
        var hash = Identity.TraceCacheKeyHash(Identity.BuildTraceCacheKey(Parts));
        Assert.Matches(Sha256Hex(), hash);
        Assert.Equal(hash, Identity.TraceCacheKeyHash(Identity.BuildTraceCacheKey(Parts with { Target = Target with { } })));
    }

    public static TheoryData<string> Labels =>
    [
        "instruction", "params", "call index", "test", "target", "engine name", "engine SPI version", "policy version", "agent", "agent context", "agent context, to none",
    ];

    private static TraceCacheKeyParts Changed(string label) => label switch
    {
        "instruction" => Parts with { Signature = Identity.TraceCallSignature("act", "close billing", null) },
        "params" => Parts with { Signature = Identity.TraceCallSignature("act", "open billing", new JsonObject { ["fast"] = true }) },
        "call index" => Parts with { CallIndex = 1 },
        "test" => Parts with { TestId = "other test" },
        "target" => Parts with { Target = Target with { TargetId = "web-b" } },
        "engine name" => Parts with { Target = Target with { EngineName = "other" } },
        "engine SPI version" => Parts with { Target = Target with { SpiVersion = 2 } },
        "policy version" => Parts with { PolicyVersion = "replay-policy/1" },
        "agent" => Parts with { Agent = Parts.Agent with { Name = "admin" } },
        "agent context" => Parts with { Agent = Parts.Agent with { Context = "The billing period is Daily." } },
        "agent context, to none" => Parts with { Agent = new CacheAgentIdentity("default", null) },
        _ => throw new ArgumentOutOfRangeException(nameof(label)),
    };

    [Theory]
    [MemberData(nameof(Labels))]
    public void Changes_when_the_part_changes(string label)
    {
        var hash = Identity.TraceCacheKeyHash(Identity.BuildTraceCacheKey(Parts));
        Assert.NotEqual(hash, Identity.TraceCacheKeyHash(Identity.BuildTraceCacheKey(Changed(label))));
    }

    // Every committed .e2e/cache entry is filed under this hash, so a change here misses all of them after an upgrade.
    [Fact]
    public void Hashes_a_fixed_key_to_the_same_value_across_releases()
    {
        Assert.Equal("a108ef464ccf8c316bc7ae7766c586eae6a3bb2cc482654246854e58793c070a", Identity.TraceCacheKeyHash(Identity.BuildTraceCacheKey(Parts)));
        var withParams = Parts with
        {
            Signature = Identity.TraceCallSignature("act", "upgrade to {{plan}}", new JsonObject { ["plan"] = "Pro", ["seats"] = 3 }),
            CallIndex = 2,
            Agent = new CacheAgentIdentity("buyer", null),
        };
        Assert.Equal("400b6e2625b0feb2483a87adcd90ab7ddb03a83ef47ed5409e57680cbb1d0caf", Identity.TraceCacheKeyHash(Identity.BuildTraceCacheKey(withParams)));
    }

    [Fact]
    public void Keys_the_agent_context_by_digest_never_by_its_text()
    {
        var key = Identity.BuildTraceCacheKey(Parts with { Agent = new CacheAgentIdentity("buyer", "Pay with <secret:card>.") });
        Assert.Equal("buyer", key.Agent);
        Assert.Matches(Sha256Hex(), key.AgentContextDigest);
        Assert.DoesNotContain("Pay with", key.ToJson().ToJsonString());
    }
}
