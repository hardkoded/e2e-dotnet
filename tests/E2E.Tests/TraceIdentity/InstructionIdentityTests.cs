// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TraceIdentity;

/// <summary>Upstream's <c>describe('instruction identity')</c> in <c>trace-identity.test.ts</c>.</summary>
public sealed class InstructionIdentityTests
{
    [Fact]
    public void Normalizes_line_endings_NFC_and_outer_whitespace_only()
    {
        Assert.Equal("open billing", Identity.NormalizeInstruction("  open billing\r\n"));
        Assert.NotEqual(Identity.InstructionDigest("open billing"), Identity.InstructionDigest("open  billing"));
        Assert.Equal(Identity.InstructionDigest("café"), Identity.InstructionDigest("café"));
    }
}
