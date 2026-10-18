// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using static E2E.Tests.AiTrace.AiTraceTestSupport;

namespace E2E.Tests.AiTrace;

/// <summary>Upstream's <c>stringify</c> describe.</summary>
public sealed class StringifyTests
{
    [Fact]
    public void Replaces_binary_payloads_with_a_size_note()
    {
        var text = Internal.AiTrace.Stringify(O(("data", new byte[3]), ("nested", A(new byte[8])), ("n", new BigInteger(1))));

        Assert.True(Same(O(("data", "[binary 3 bytes omitted]"), ("nested", A("[binary 8 bytes omitted]")), ("n", "1")), text));
    }
}
