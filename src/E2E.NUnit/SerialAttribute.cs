// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.NUnit;

/// <summary>
/// Makes the tests of an <see cref="E2ETest"/> fixture a serial group, as upstream's <c>test.describe(..., { serial: true })</c>: the members
/// share one session, so the page state of one member is still there in the next, and the fixture's fields carry values between them.
/// .NET-only shape (the port has no serial groups, the runner owns them): NUnit runs the members, so give them
/// <c>[DependsOnTest]</c> to fix their sequence. After a member fails, the later members are skipped. The session starts with the first member,
/// commits the replay cache at the end of the group, and is the first member's, so its title, <c>[Agent]</c> and <c>[AgentContext]</c> apply to all.
/// Do not combine it with <c>[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]</c> or with tests running in parallel in the fixture.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class SerialAttribute : Attribute;
