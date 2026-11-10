// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace E2E.NUnit.Tests.SerialSession;

/// <summary>
/// .NET-only: <c>[Serial]</c> has no upstream <c>it</c> (upstream's serial groups belong to its runner, tester-army/e2e#1056, and the port
/// leaves them to NUnit; hardkoded/e2e-dotnet#1241). Runs <see cref="SerialFixture"/> in a runner of its own and reads each member's result.
/// </summary>
public sealed class SerialSessionTests
{
    [Test]
    public void Members_keep_one_session_and_the_later_ones_skip_after_a_failure()
    {
        var results = Task.Run(() =>
        {
            var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
            runner.Load(typeof(SerialFixture).Assembly, new Dictionary<string, object>());
            var run = runner.Run(TestListener.NULL, TestFilter.FromXml($"<filter><class>{typeof(SerialFixture).FullName}</class></filter>"));
            return Leaves(run).ToDictionary(result => result.Test.MethodName!);
        }).GetAwaiter().GetResult();

        Assert.That(results[nameof(SerialFixture.A_opens_the_page)].ResultState.Status, Is.EqualTo(TestStatus.Passed));
        Assert.That(results[nameof(SerialFixture.B_sees_the_page_the_first_opened)].ResultState.Status, Is.EqualTo(TestStatus.Passed), results[nameof(SerialFixture.B_sees_the_page_the_first_opened)].Message);
        Assert.That(results[nameof(SerialFixture.C_fails)].ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(results[nameof(SerialFixture.D_is_skipped_after_the_failure)].ResultState.Status, Is.EqualTo(TestStatus.Skipped));
        Assert.That(SerialFixture.DRan, Is.False);
    }

    private static IEnumerable<ITestResult> Leaves(ITestResult result) =>
        result.HasChildren ? result.Children.SelectMany(Leaves) : [result];
}

[Explicit("Run by SerialSessionTests")]
[Serial]
public sealed class SerialFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    public static bool DRan;

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [Test]
    public async Task A_opens_the_page()
    {
        await App.OpenAsync("/wizard");
        await Expect.That(Screen.GetByLabel("Workspace name")).ToBeVisibleAsync();
    }

    [Test, DependsOnTest(nameof(A_opens_the_page))]
    public async Task B_sees_the_page_the_first_opened()
    {
        await Expect.That(Screen.GetByLabel("Workspace name")).ToBeVisibleAsync();
    }

    [Test, DependsOnTest(nameof(B_sees_the_page_the_first_opened))]
    public void C_fails() => Assert.Fail("the serial group fails here");

    [Test]
    public void D_is_skipped_after_the_failure() => DRan = true;
}
