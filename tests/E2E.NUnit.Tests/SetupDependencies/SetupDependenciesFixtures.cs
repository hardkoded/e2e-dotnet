// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.NUnit.Tests.EngineContract;

namespace E2E.NUnit.Tests.SetupDependencies;

/// <summary>A setup test and the test that restores its session, in separate fixtures so NUnit's own order cannot run the setup first.</summary>
[Explicit("Run by SetupDependenciesTests")]
public sealed class DependencySetupFixture : SessionFixtureBase
{
    public static int Runs;

    [E2ESetup("save the dependency session", Saves = ["dependency"])]
    public async Task SaveDependencySession()
    {
        Interlocked.Increment(ref Runs);
        await SignInAsync("dependent");
        await SaveSessionAsync("dependency");
    }
}

/// <summary>The consumer of <see cref="DependencySetupFixture"/>. Its class comes first by name, so NUnit would run it before the setup.</summary>
[Explicit("Run by SetupDependenciesTests")]
public sealed class AConsumerFixture : SessionFixtureBase
{
    public static string? Seen;

    [Test]
    [E2ESession("dependency")]
    public async Task Consumes_the_dependency_session() => Seen = await SignedInAsync();

    [Test]
    [E2ESession("dependency")]
    public async Task Consumes_the_dependency_session_again() => Seen = await SignedInAsync();
}

/// <summary>A setup that fails before it saves, and a consumer of its session.</summary>
[Explicit("Run by SetupDependenciesTests")]
public sealed class BrokenSetupFixture : SessionFixtureBase
{
    public static int Runs;

    [E2ESetup("fail before saving", Saves = ["broken"])]
    public Task FailBeforeSaving()
    {
        Interlocked.Increment(ref Runs);
        throw new InvalidOperationException("sign-in is down");
    }

    [Test]
    [E2ESession("broken")]
    public Task Consumes_the_broken_session() => Task.CompletedTask;

    [Test]
    [E2ESession("broken")]
    public Task Consumes_the_broken_session_again() => Task.CompletedTask;
}
