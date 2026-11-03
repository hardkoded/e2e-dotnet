// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.NUnit.Tests.SkipAfterFailure;
using E2E.WinApp.NUnit;
using NUnit.Framework.Interfaces;

namespace E2E.NUnit.Tests.RunSelection;

/// <summary>The test a nested run starts, which the filters left out of a Windows app run.</summary>
[Explicit("Run by WinAppTestTests")]
public sealed class LeftOutWinAppFixture : WinAppTest
{
    [Test]
    public void Windows_test_is_left_out() => Assert.Fail("A test the filters left out must not run.");
}

/// <summary>
/// .NET-only (upstream has no Windows adapter): <see cref="WinAppTest"/> derives from <see cref="E2ETest"/>, so a Windows app test the filters
/// left out is skipped before the base setup that opens the app, which has no window to open.
/// </summary>
public sealed class WinAppTestTests
{
    [Test]
    public void Skips_a_windows_test_the_filters_left_out_before_the_app_is_opened()
    {
        E2E.NUnit.RunSelection.Set(new Dictionary<string, string> { [$"{typeof(LeftOutWinAppFixture).FullName}.{nameof(LeftOutWinAppFixture.Windows_test_is_left_out)}"] = "login.cs did not change" });
        try
        {
            var result = NestedRun.Run(nameof(LeftOutWinAppFixture.Windows_test_is_left_out));

            Assert.That(result.ResultState.Status, Is.EqualTo(TestStatus.Skipped));
            Assert.That(result.Message, Does.EndWith("login.cs did not change"));
        }
        finally
        {
            E2E.NUnit.RunSelection.Set(new Dictionary<string, string>());
        }
    }

    [Test]
    public void Finds_the_tests_of_a_windows_fixture()
    {
        var found = E2E.NUnit.RunSelection.Discover([typeof(LeftOutWinAppFixture)], typeof(LeftOutWinAppFixture).Assembly);

        Assert.That(found.Select(test => test.Title), Is.EqualTo(new[] { nameof(LeftOutWinAppFixture.Windows_test_is_left_out) }));
    }
}
