// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.TUnit.Tests.EngineContract;

namespace E2E.TUnit.Tests.SetupDependencies;

/// <summary>A setup test, in a class of its own: the consumer runs without it, and the setup must run for the consumer.</summary>
public sealed class DependencySetupFixture : SessionFixtureBase
{
    private static readonly StateSpyEngine Engine = new(new WebEngine(new WebEngineOptions()));

    internal override StateSpyEngine Spy => Engine;

    [Test, Explicit, E2ESetup("save the dependency session", Saves = ["dependency"])]
    public async Task SaveDependencySession()
    {
        Console.Out.WriteLine("setup-ran");
        await SignInAsync("dependent");
        await SaveSessionAsync("dependency");
    }
}

/// <summary>The consumer of <see cref="DependencySetupFixture"/>; a run of this class alone has no setup test in it.</summary>
public sealed class ConsumerFixture : SessionFixtureBase
{
    private static readonly StateSpyEngine Engine = new(new WebEngine(new WebEngineOptions()));

    internal override StateSpyEngine Spy => Engine;

    [Test, Explicit]
    [E2ESession("dependency")]
    public async Task Consumes_the_dependency_session() => await Assert.That(await SignedInAsync()).IsEqualTo("dependent");

    [Test, Explicit]
    [E2ESession("dependency")]
    public async Task Consumes_the_dependency_session_again() => await Assert.That(await SignedInAsync()).IsEqualTo("dependent");
}

/// <summary>A setup that fails before it saves, and a consumer of its session.</summary>
public sealed class BrokenSetupFixture : SessionFixtureBase
{
    private static readonly StateSpyEngine Engine = new(new WebEngine(new WebEngineOptions()));

    internal override StateSpyEngine Spy => Engine;

    [Test, Explicit, E2ESetup("fail before saving", Saves = ["broken"])]
    public Task FailBeforeSaving()
    {
        Console.Out.WriteLine("broken-setup-ran");
        throw new InvalidOperationException("sign-in is down");
    }
}

/// <summary>The consumer of <see cref="BrokenSetupFixture"/>.</summary>
public sealed class BrokenConsumerFixture : SessionFixtureBase
{
    private static readonly StateSpyEngine Engine = new(new WebEngine(new WebEngineOptions()));

    internal override StateSpyEngine Spy => Engine;

    [Test, Explicit]
    [E2ESession("broken")]
    public Task Consumes_the_broken_session() => Task.CompletedTask;

    [Test, Explicit]
    [E2ESession("broken")]
    public Task Consumes_the_broken_session_again() => Task.CompletedTask;
}
