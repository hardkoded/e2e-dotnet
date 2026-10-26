// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Tests.TraceIdentity;

/// <summary>Upstream's <c>describe('createCallIndexer')</c> in <c>trace-identity.test.ts</c>.</summary>
public sealed class CreateCallIndexerTests
{
    [Fact]
    public void Counts_occurrences_per_signature_not_per_attempt()
    {
        var index = Identity.CreateCallIndexer();
        var open = Identity.TraceCallSignature("act", "open billing", null);
        var upgrade = Identity.TraceCallSignature("act", "upgrade the plan", null);
        Assert.Equal(0, index(open));
        Assert.Equal(0, index(upgrade));
        Assert.Equal(1, index(open));
        Assert.Equal(0, index(Identity.TraceCallSignature("act", "open billing", new JsonObject { ["fast"] = true })));
        Assert.Equal(1, index(upgrade));
    }
}
