// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.NUnit.Tests.SkipAfterFailure;
using NUnit.Framework.Interfaces;

namespace E2E.NUnit.Tests.WebPlatform;

/// <summary>
/// Upstream's <c>web-platform.test.ts</c>, the one <c>it</c> that depends on the test framework's teardown, run through NUnit with a
/// <c>[TearDown]</c> in place of <c>afterEach</c>. The other <c>it</c> are in <c>E2E.Tests/WebPlatform</c>, driven through a session.
/// </summary>
public sealed class WebPlatformTests
{
    [SetUp]
    public void ClearResults() => RemoveResults();

    [OneTimeTearDown]
    public void RemoveResults()
    {
        if (Directory.Exists(Path.GetDirectoryName(LateHandlerFixture.Results)))
        {
            Directory.Delete(Path.GetDirectoryName(LateHandlerFixture.Results)!, recursive: true);
        }
    }

    // Partial: upstream also reads the steps from report.json (browser.route, app.open, app.open) and the failure url. The port checks the
    // verdict, the error, and the screen kept at the failure.
    [Test]
    public void Partial_Takes_the_failure_evidence_of_a_late_handler_assertion_before_a_teardown_navigates_away()
    {
        var result = NestedRun.Run(nameof(LateHandlerFixture.A_route_handler_assertion_that_no_step_follows_then_a_teardown_that_navigates));

        Assert.That(result.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(result.Message, Does.Contain("ASSERTION_FAILED").Or.Contain("expect(received).toBe(expected)"));
        var files = Directory.GetFiles(LateHandlerFixture.Results, "screen-at-failure.txt", SearchOption.AllDirectories);
        Assert.That(files, Has.Length.EqualTo(1));
        var text = File.ReadAllText(files[0]);
        Assert.That(text, Does.Contain("heading \"Flags\""));
        Assert.That(text, Does.Not.Contain("heading \"Home\""));
    }
}
