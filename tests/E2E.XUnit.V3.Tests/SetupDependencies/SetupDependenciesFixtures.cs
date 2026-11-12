// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.XUnit.V3.Tests.EngineContract;

namespace E2E.XUnit.V3.Tests.SetupDependencies;

/// <summary>A setup test, in a class of its own: the consumer runs without it, and the setup must run for the consumer.</summary>
public sealed class DependencySetupFixture : SessionFixtureBase
{
    private static readonly StateSpyEngine Engine = new(new WebEngine(new WebEngineOptions()));

    internal override StateSpyEngine Spy => Engine;

    [E2ESetup("save the dependency session", Saves = ["dependency"], Explicit = true)]
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

    [Fact(Explicit = true)]
    [E2ESession("dependency")]
    public async Task Consumes_the_dependency_session() => Assert.Equal("dependent", await SignedInAsync());

    [Fact(Explicit = true)]
    [E2ESession("dependency")]
    public async Task Consumes_the_dependency_session_again() => Assert.Equal("dependent", await SignedInAsync());
}

/// <summary>A setup that fails before it saves, and a consumer of its session.</summary>
public sealed class BrokenSetupFixture : SessionFixtureBase
{
    private static readonly StateSpyEngine Engine = new(new WebEngine(new WebEngineOptions()));

    internal override StateSpyEngine Spy => Engine;

    [E2ESetup("fail before saving", Saves = ["broken"], Explicit = true)]
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

    [Fact(Explicit = true)]
    [E2ESession("broken")]
    public Task Consumes_the_broken_session() => Task.CompletedTask;

    [Fact(Explicit = true)]
    [E2ESession("broken")]
    public Task Consumes_the_broken_session_again() => Task.CompletedTask;
}
