// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.WinApp.XUnit.V3;

namespace E2E.XUnit.V3.Tests.RunSelection;

/// <summary>A Windows app fixture whose test the filters leave out. Its test never runs, and the tests call its setup by hand.</summary>
public sealed class LeftOutWinAppFixture : WinAppTest
{
    [Fact(Skip = "only found by reflection")]
    public void Windows_test_is_left_out()
    {
    }
}

/// <summary>
/// .NET-only (upstream has no Windows adapter): <see cref="WinAppTest"/> derives from <see cref="E2ETest"/>, so a Windows app test the filters
/// left out is skipped before the app opens.
/// </summary>
[Collection(nameof(RunSelectionCollection))]
public sealed class WinAppTestTests
{
    [Fact]
    public async Task Skips_a_windows_test_the_filters_left_out_before_the_app_opens()
    {
        var method = Xunit.TestContext.Current.Test!.TestCase.TestMethodName!;
        E2E.XUnit.V3.RunSelection.Set(new Dictionary<string, string> { [$"{typeof(LeftOutWinAppFixture).FullName}.{method}"] = "login.cs did not change" });
        try
        {
            // The xUnit assertions drop an exception that skips a test, so it is caught by hand.
            // A test that went on would fail at the engine it has no session for, not with the skip.
            string? reason = null;
            try
            {
                await new LeftOutWinAppFixture().InitializeAsync();
            }
            catch (InvalidOperationException ex)
            {
                reason = ex.Message;
            }

            Assert.Equal("$XunitDynamicSkip$login.cs did not change", reason);
        }
        finally
        {
            E2E.XUnit.V3.RunSelection.Set(new Dictionary<string, string>());
        }
    }

    [Fact]
    public void Finds_the_tests_of_a_windows_fixture()
    {
        var found = E2E.XUnit.V3.RunSelection.Discover([typeof(LeftOutWinAppFixture)], typeof(LeftOutWinAppFixture).Assembly);

        Assert.Equal([nameof(LeftOutWinAppFixture.Windows_test_is_left_out)], found.Select(test => test.Title));
    }
}
