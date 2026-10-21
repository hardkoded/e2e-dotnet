// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.NUnit.Tests.SkipAfterFailure;
using NUnit.Framework.Interfaces;

namespace E2E.NUnit.Tests.FailureEvidence;

/// <summary>
/// Upstream's <c>describe('failure evidence')</c> (<c>failure-evidence.test.ts</c>), the one <c>it</c> that depends on the test
/// framework's teardown, run through NUnit with a <c>[TearDown]</c> in place of <c>afterEach</c>. The other <c>it</c> are in
/// <c>E2E.Tests/FailureEvidence</c>, driven through a session. <c>Looks_before_teardown_runs_when_an_Expect_Soft_failed</c> is
/// .NET-only: upstream closes its soft failures in the runner, and NUnit holds them on the result.
/// </summary>
public sealed class FailureEvidenceTests
{
    [SetUp]
    public void ClearResults()
    {
        if (Directory.Exists(Path.GetDirectoryName(TeardownFixture.Results)))
        {
            Directory.Delete(Path.GetDirectoryName(TeardownFixture.Results)!, recursive: true);
        }
    }

    [OneTimeTearDown]
    public void RemoveResults()
    {
        if (Directory.Exists(Path.GetDirectoryName(TeardownFixture.Results)))
        {
            Directory.Delete(Path.GetDirectoryName(TeardownFixture.Results)!, recursive: true);
        }
    }

    [Test]
    public void Looks_at_the_screen_before_teardown_runs_so_an_afterEach_that_navigates_away_cannot_replace_the_evidence()
    {
        var result = NestedRun.Run(nameof(TeardownFixture.Fails_then_tears_down));

        Assert.That(result.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        var screen = ScreenAtFailure();
        Assert.That(screen, Does.Contain("Loads"));
        Assert.That(screen, Does.Not.Contain("The playground, described."));
    }

    [Test]
    public void Looks_before_teardown_runs_when_an_Expect_Soft_failed()
    {
        var result = NestedRun.Run(nameof(TeardownFixture.Soft_fails_then_tears_down));

        Assert.That(result.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        var screen = ScreenAtFailure();
        Assert.That(screen, Does.Contain("Loads"));
        Assert.That(screen, Does.Not.Contain("The playground, described."));
    }

    private static string ScreenAtFailure()
    {
        var files = Directory.GetFiles(TeardownFixture.Results, "screen-at-failure.txt", SearchOption.AllDirectories);
        Assert.That(files, Has.Length.EqualTo(1));
        return File.ReadAllText(files[0]);
    }
}
