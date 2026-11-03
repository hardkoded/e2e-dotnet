// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Mobile.NUnit;
using E2E.NUnit.Tests.SkipAfterFailure;
using NUnit.Framework.Interfaces;

namespace E2E.NUnit.Tests.RunSelection;

/// <summary>The test a nested run starts, which the filters left out of a mobile run.</summary>
[Explicit("Run by MobileAppTestTests")]
public sealed class LeftOutMobileFixture : MobileAppTest
{
    [Test]
    public void Mobile_test_is_left_out() => Assert.Fail("A test the filters left out must not run.");
}

/// <summary>
/// .NET-only (upstream has no mobile adapter): <see cref="MobileAppTest"/> derives from <see cref="E2ETest"/>, so a mobile test the filters
/// left out is skipped before the base setup of the app, which has no device to install on.
/// </summary>
public sealed class MobileAppTestTests
{
    [Test]
    public void Skips_a_mobile_test_the_filters_left_out_before_the_app_is_installed()
    {
        E2E.NUnit.RunSelection.Set(new Dictionary<string, string> { [$"{typeof(LeftOutMobileFixture).FullName}.{nameof(LeftOutMobileFixture.Mobile_test_is_left_out)}"] = "login.cs did not change" });
        try
        {
            var result = NestedRun.Run(nameof(LeftOutMobileFixture.Mobile_test_is_left_out));

            Assert.That(result.ResultState.Status, Is.EqualTo(TestStatus.Skipped));
            Assert.That(result.Message, Does.EndWith("login.cs did not change"));
        }
        finally
        {
            E2E.NUnit.RunSelection.Set(new Dictionary<string, string>());
        }
    }

    [Test]
    public void Finds_the_tests_of_a_mobile_fixture()
    {
        var found = E2E.NUnit.RunSelection.Discover([typeof(LeftOutMobileFixture)], typeof(LeftOutMobileFixture).Assembly);

        Assert.That(found.Select(test => test.Title), Is.EqualTo(new[] { nameof(LeftOutMobileFixture.Mobile_test_is_left_out) }));
    }
}
