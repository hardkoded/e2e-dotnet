// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Mobile.TUnit;
using TUnit.Core.Exceptions;
using TUnitContext = global::TUnit.Core.TestContext;

namespace E2E.TUnit.Tests.Mobile;

/// <summary>A mobile fixture that shows the engine its base class creates. It never runs as a test.</summary>
public sealed class MobileFixture : E2E.Mobile.TUnit.MobileAppTest
{
    internal IEngine Create() => CreateEngine();
}

/// <summary>A mobile fixture whose test the filters leave out. Its test never runs, and the tests call its setup by hand.</summary>
public sealed class LeftOutMobileFixture : E2E.Mobile.TUnit.MobileAppTest
{
    [Test]
    [Skip("only found by reflection")]
    public void Mobile_test_is_left_out()
    {
    }
}

/// <summary>
/// .NET-only (upstream has no mobile adapter): <see cref="E2E.Mobile.TUnit.MobileAppTest"/> derives from <see cref="E2E.TUnit.E2ETest"/>, so a
/// mobile test the filters left out is skipped, and it is skipped before the run leases a hosted device. A hosted run leases its device
/// when the engine is created, before the app is installed. No device is available in this project, so the install and the launch
/// need a run on a simulator or emulator, which the sample tests cover.
/// </summary>
public sealed class MobileAppTestTests
{
    [Test]
    public async Task Derives_from_the_TUnit_E2ETest() =>
        await Assert.That(typeof(E2E.Mobile.TUnit.MobileAppTest).BaseType).IsEqualTo(typeof(E2E.TUnit.E2ETest));

    private static T Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T ex)
        {
            return ex;
        }

        throw new InvalidOperationException("Nothing was thrown.");
    }

    [Test]
    [NotInParallel("E2E_MOBILE_PROVIDER")]
    public async Task Leases_the_hosted_device_when_the_engine_is_created()
    {
        var provider = Environment.GetEnvironmentVariable("E2E_MOBILE_PROVIDER");
        // A provider the lease rejects: an engine that reached the lease fails with that error.
        Environment.SetEnvironmentVariable("E2E_MOBILE_PROVIDER", "not-a-provider");
        try
        {
            var ex = Throws<InvalidOperationException>(() => new MobileFixture().Create());

            await Assert.That(ex.Message).Contains("E2E_MOBILE_PROVIDER must be local");
        }
        finally
        {
            Environment.SetEnvironmentVariable("E2E_MOBILE_PROVIDER", provider);
        }
    }

    [Test]
    [NotInParallel(["E2E_MOBILE_PROVIDER", "RunSelectionTests"])]
    public async Task Skips_a_mobile_test_the_filters_left_out_before_it_leases_a_device(CancellationToken cancellationToken)
    {
        var context = TUnitContext.Current!;
        var method = context.Metadata.TestDetails.MethodName;
        E2E.TUnit.RunSelection.Set(new Dictionary<string, string> { [$"{typeof(LeftOutMobileFixture).FullName}.{method}"] = "login.cs did not change" });
        var provider = Environment.GetEnvironmentVariable("E2E_MOBILE_PROVIDER");
        // A provider the lease rejects: a test that reached the lease would fail with that error, not with the skip.
        Environment.SetEnvironmentVariable("E2E_MOBILE_PROVIDER", "not-a-provider");
        try
        {
            // The assertions would take the skip for a failure, so the skip is read by hand.
            string? reason = null;
            try
            {
                await new LeftOutMobileFixture().StartE2ESessionAsync(context, cancellationToken);
            }
            catch (SkipTestException ex)
            {
                reason = ex.Message;
            }

            await Assert.That(reason).IsEqualTo("login.cs did not change");
        }
        finally
        {
            Environment.SetEnvironmentVariable("E2E_MOBILE_PROVIDER", provider);
            E2E.TUnit.RunSelection.Set(new Dictionary<string, string>());
        }
    }

    [Test]
    public async Task Finds_the_tests_of_a_mobile_fixture()
    {
        var found = E2E.TUnit.RunSelection.Discover([typeof(LeftOutMobileFixture)], typeof(LeftOutMobileFixture).Assembly);

        await Assert.That(found.Select(test => test.Title).ToArray()).IsEquivalentTo([nameof(LeftOutMobileFixture.Mobile_test_is_left_out)]);
    }
}
