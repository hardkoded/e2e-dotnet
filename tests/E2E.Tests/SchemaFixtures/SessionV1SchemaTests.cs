// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.SchemaFixtures;

/// <summary>Upstream's <c>describe.each(schemas)('%s schema')</c> for <c>session-v1</c> (<c>tests/unit/schema-fixtures.test.ts</c>).</summary>
public sealed class SessionV1SchemaTests
{
    [Fact]
    public void Accepts_the_valid_fixture() => SessionSchema.AssertValidSessionEnvelope(ReportSchema.ReadJson("fixtures", "session-v1.valid.json"));

    [Fact]
    public void Rejects_the_invalid_fixture() => Assert.False(SessionSchema.IsValid(ReportSchema.ReadJson("fixtures", "session-v1.invalid.json")));
}
