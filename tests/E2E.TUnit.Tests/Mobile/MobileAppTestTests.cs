// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Mobile.TUnit;

namespace E2E.TUnit.Tests.Mobile;

/// <summary>A mobile fixture that shows the engine its base class creates. It never runs as a test.</summary>
public sealed class MobileFixture : E2E.Mobile.TUnit.MobileAppTest
{
    internal IEngine Create() => CreateEngine();
}

/// <summary>
/// .NET-only (upstream has no mobile adapter): <see cref="E2E.Mobile.TUnit.MobileAppTest"/> derives from <see cref="E2E.TUnit.E2ETest"/>, and a
/// hosted run leases its device when the engine is created, before the app is installed. No device is available in this project,
/// so the install and the launch need a run on a simulator or emulator, which the sample tests cover. The xUnit and NUnit tests
/// that skip a test the filters left out have no twin: <c>E2E.TUnit</c> has no run selection yet.
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
}
