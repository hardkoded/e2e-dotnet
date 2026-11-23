// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.NUnit.Tests.Registry;

/// <summary>Upstream's <c>test.fixme('not yet', noop)</c> at the top of a file and inside <c>test.describe('plain group')</c>, registered with <c>[Fixme]</c>.</summary>
[Explicit("Discovered by RegistrationTests, never run")]
public sealed class FixmeRegistrationFixture : E2ETest
{
    [Test]
    [Fixme]
    public void Not_yet() { }
}

/// <summary>A test marked fixme in a serial group (upstream's <c>test.describe('unit', { serial: true }, ...)</c>).</summary>
[Explicit("Discovered by RegistrationTests, never run")]
[Serial]
public sealed class FixmeInSerialGroupFixture : E2ETest
{
    [Test]
    [Fixme]
    public void Fixme_serial_member() { }
}
