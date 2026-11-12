// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Mobile.XUnit.V3;

namespace E2E.XUnit.V3.Tests.RunSelection;

/// <summary>A mobile fixture whose test the filters leave out. Its test never runs, and the tests call its setup by hand.</summary>
public sealed class LeftOutMobileFixture : MobileAppTest
{
    [Fact(Skip = "only found by reflection")]
    public void Mobile_test_is_left_out()
    {
    }
}

/// <summary>
/// .NET-only (upstream has no mobile adapter): <see cref="MobileAppTest"/> derives from <see cref="E2ETest"/>, so a mobile test the filters
/// left out is skipped, and it is skipped before the run leases a hosted device.
/// </summary>
[Collection(nameof(RunSelectionCollection))]
public sealed class MobileAppTestTests
{
    private static async Task<string?> SkipReasonOfSetupAsync()
    {
        try
        {
            await new LeftOutMobileFixture().InitializeAsync();
            return null;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    [Fact]
    public async Task Skips_a_mobile_test_the_filters_left_out_before_it_leases_a_device()
    {
        var method = Xunit.TestContext.Current.Test!.TestCase.TestMethodName!;
        E2E.XUnit.V3.RunSelection.Set(new Dictionary<string, string> { [$"{typeof(LeftOutMobileFixture).FullName}.{method}"] = "login.cs did not change" });
        var provider = Environment.GetEnvironmentVariable("E2E_MOBILE_PROVIDER");
        // A provider the lease rejects: a test that reached the lease would fail with that error, not with the skip.
        Environment.SetEnvironmentVariable("E2E_MOBILE_PROVIDER", "not-a-provider");
        try
        {
            Assert.Equal("$XunitDynamicSkip$login.cs did not change", await SkipReasonOfSetupAsync());
        }
        finally
        {
            Environment.SetEnvironmentVariable("E2E_MOBILE_PROVIDER", provider);
            E2E.XUnit.V3.RunSelection.Set(new Dictionary<string, string>());
        }
    }

    [Fact]
    public void Finds_the_tests_of_a_mobile_fixture()
    {
        var found = E2E.XUnit.V3.RunSelection.Discover([typeof(LeftOutMobileFixture)], typeof(LeftOutMobileFixture).Assembly);

        Assert.Equal([nameof(LeftOutMobileFixture.Mobile_test_is_left_out)], found.Select(test => test.Title));
    }
}
