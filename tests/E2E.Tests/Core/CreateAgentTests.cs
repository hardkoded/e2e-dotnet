// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.Core;

/// <summary>Upstream's <c>describe('createAgent')</c> (<c>tests/integration/core.test.ts</c>): an agent over a core session, and the replay cache it keeps by <c>cacheKey</c>.</summary>
[Collection(E2E.Tests.SessionSecrecy.SharedSecretsCollection.Name)]
public sealed class CreateAgentTests : IAsyncLifetime
{
    private static readonly LaunchApp FakeApp = new() { Url = "http://127.0.0.1:4500" };

    private readonly string _dir = Directory.CreateTempSubdirectory("e2e-core-agent-tests-").FullName;

    private readonly List<Host> _hosts = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var opened in _hosts)
        {
            try
            {
                await opened.CloseAsync();
            }
            catch (Exception)
            {
                // Closing is checked by the tests that look at it.
            }
        }

        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // A directory a process still holds is the system's to clean.
        }
    }

    /// <summary>Upstream's <c>shop()</c>: a product page whose button leads to a cart with a coupon and a payment button.</summary>
    private static DocumentWorld Shop() => new DocumentWorld().Map("/", page =>
        {
            var paid = new DocumentElement
            {
                Role = "status",
                Name = "Paid",
                Hidden = true,
            };
            var coupon = new DocumentElement { Role = "checkbox", Name = "Apply coupon SAVE10" };
            var pay = new DocumentElement
            {
                Role = "button",
                Name = "Pay now",
                OnTap = () => paid.Hidden = false,
            };
            var cart = new DocumentElement
            {
                Role = "region",
                Name = "Cart",
                Hidden = true,
            };
            cart.Children.Add(coupon);
            cart.Children.Add(pay);
            page.Roots.Add(
                new DocumentElement
                {
                    Role = "button",
                    Name = "Add to cart",
                    OnTap = () => cart.Hidden = false,
                }
            );
            page.Roots.Add(
                new DocumentElement
                {
                    Role = "paragraph",
                    Text = "39,000",
                    TestId = "sale-price",
                }
            );
            page.Roots.Add(cart);
            page.Roots.Add(paid);
        });

    /// <summary>Upstream's <c>scripted()</c>: a brain that adds to cart, checks the coupon, and pays.</summary>
    private static (TestExecutor Brain, Func<int> Calls) Scripted()
    {
        var calls = 0;
        var brain = new TestExecutor("scripted", async (context, _) =>
        {
            Interlocked.Increment(ref calls);
            await Tap(context, "button \"Add to cart\"");
            var observation = await context.ObserveAsync();
            await context.Actions.CheckAsync(TargetFor(observation.Text, "checkbox \"Apply coupon SAVE10\""), true);
            await Tap(context, "button \"Pay now\"");
            return Passed("paid");
        }) { Version = "1" };
        return (brain, () => Volatile.Read(ref calls));
    }

    private static async Task Tap(StepExecutorContext context, string description)
    {
        var observation = await context.ObserveAsync();
        await context.Actions.TapAsync(TargetFor(observation.Text, description));
    }

    private static string[] Recordings(string cacheDir) =>
        Directory.Exists(cacheDir)
            ? [.. Directory.EnumerateFiles(cacheDir, "*.json", SearchOption.AllDirectories)]
            : [];

    private async Task<Host> HostOf(IEngine engine)
    {
        var opened = await Launch.LaunchAsync(
            engine,
            new LaunchOptions
            {
                App = FakeApp,
                Cwd = _dir,
                Env = new Dictionary<string, string?>(),
            }
        );
        _hosts.Add(opened);
        return opened;
    }

    [Fact]
    public async Task Records_a_flow_under_its_cacheKey_and_replays_it_with_no_model()
    {
        var (brain, calls) = Scripted();
        var cacheDir = Path.Combine(_dir, "cache");
        var opened = await HostOf(new DocumentEngine(Shop()));
        foreach (var round in new[] { 1, 2 })
        {
            var session = await opened.NewSessionAsync();
            await session.App.OpenAsync("/");
            var agent = CoreAgents.CreateAgent(
                session,
                new CreateAgentOptions
                {
                    Executor = brain,
                    Cache = new AgentCacheOptions { Dir = cacheDir },
                }
            );
            var result = await agent.ActAsync(
                "checkout with the coupon",
                new CachingActOptions { CacheKey = "checkout" }
            );
            Assert.True(await session.Screen.GetByRole("checkbox").IsCheckedAsync());
            // A check after the step confirms its recording.
            await session.Screen.GetByRole("status", "Paid").WaitForAsync();
            Assert.Equal(round == 1 ? "missed" : "self-finalized", result.Cache?.Mode);
            await session.CloseAsync();
            Assert.Equal(1, calls());
        }

        Assert.Single(Recordings(cacheDir));
    }

    [Fact]
    public async Task Keeps_a_confirmed_recording_when_the_host_closes_the_session_and_after_a_failure_the_program_caught()
    {
        var (brain, _) = Scripted();
        var cacheDir = Path.Combine(_dir, "cache");
        var opened = await HostOf(new DocumentEngine(Shop()));
        var session = await opened.NewSessionAsync();
        await session.App.OpenAsync("/");
        var agent = CoreAgents.CreateAgent(
            session,
            new CreateAgentOptions
            {
                Executor = brain,
                Cache = new AgentCacheOptions { Dir = cacheDir },
            }
        );
        await agent.ActAsync("checkout with the coupon", new CachingActOptions { CacheKey = "checkout" });
        await session.Screen.GetByRole("button", "Pay now").WaitForAsync();
        var caught = await Assert.ThrowsAnyAsync<E2EException>(() =>
            session
                .Screen.GetByRole("button", "Missing")
                .WaitForAsync(new LocatorWaitForOptions { Timeout = TimeSpan.FromMilliseconds(100) })
        );
        Assert.Equal("LOCATOR_NOT_FOUND", caught.Code);
        await opened.CloseAsync();

        Assert.Single(Recordings(cacheDir));
    }

    [Fact]
    public async Task Drops_only_what_a_caught_agent_failure_found_unconfirmed_and_evicts_nothing_when_a_session_closes_before_its_check()
    {
        var (brain, calls) = Scripted();
        var flaky = new TestExecutor("scripted", (context, token) =>
            context.Step.Instruction.StartsWith("find", StringComparison.Ordinal)
                ? Task.FromResult(Failed("there is no sale"))
                : brain.RunStepAsync(context, token)
        )
        {
            Version = "1",
        };
        var cacheDir = Path.Combine(_dir, "cache");
        var opened = await HostOf(new DocumentEngine(Shop()));
        CoreAgent AgentOn(Session session) =>
            CoreAgents.CreateAgent(
                session,
                new CreateAgentOptions
                {
                    Executor = flaky,
                    Cache = new AgentCacheOptions { Dir = cacheDir },
                }
            );
        async Task Round(Func<CoreAgent, Session, Task> steps)
        {
            var session = await opened.NewSessionAsync();
            await session.App.OpenAsync("/");
            await steps(AgentOn(session), session);
            await session.CloseAsync();
        }

        async Task Fails(CoreAgent agent) =>
            Assert.Equal(
                "ACTION_FAILED",
                (
                    await Assert.ThrowsAsync<AgentException>(() =>
                        agent.ActAsync("find the sale", new CachingActOptions { CacheKey = "sale" })
                    )
                ).Code
            );

        // A recording made after a failure the program caught is confirmed by the check after it.
        await Round(
            async (agent, session) =>
            {
                await Fails(agent);
                Assert.Equal(
                    "missed",
                    (await agent.ActAsync("checkout with the coupon", new CachingActOptions { CacheKey = "checkout" }))
                        .Cache
                        ?.Mode
                );
                await session.Screen.GetByRole("button", "Pay now").WaitForAsync();
            }
        );
        Assert.Single(Recordings(cacheDir));
        // A session that closes between a replay and its check leaves the recording alone.
        await Round(
            async (agent, _) =>
                Assert.Equal(
                    "self-finalized",
                    (await agent.ActAsync("checkout with the coupon", new CachingActOptions { CacheKey = "checkout" }))
                        .Cache
                        ?.Mode
                )
        );
        Assert.Single(Recordings(cacheDir));
        // So does a replay that a check confirms after a failure.
        await Round(
            async (agent, session) =>
            {
                await Fails(agent);
                Assert.Equal(
                    "self-finalized",
                    (await agent.ActAsync("checkout with the coupon", new CachingActOptions { CacheKey = "checkout" }))
                        .Cache
                        ?.Mode
                );
                await session.Screen.GetByRole("button", "Pay now").WaitForAsync();
            }
        );
        Assert.Single(Recordings(cacheDir));
        // A recording still unconfirmed when a step fails is dropped, whatever passes later.
        await Round(
            async (agent, session) =>
            {
                Assert.Equal(
                    "missed",
                    (await agent.ActAsync("checkout with the coupon", new CachingActOptions { CacheKey = "again" }))
                        .Cache
                        ?.Mode
                );
                await Fails(agent);
                await session.Screen.GetByRole("button", "Pay now").WaitForAsync();
            }
        );
        Assert.Single(Recordings(cacheDir));
        Assert.Equal(2, calls());
    }

    [Fact]
    public async Task Requires_a_cacheKey_on_an_agent_with_a_cache_refuses_one_without_and_fails_a_call_with_no_model_as_MODEL_UNAVAILABLE()
    {
        var opened = await HostOf(new DocumentEngine(Shop()));
        var session = await opened.NewSessionAsync();
        var caching = CoreAgents.CreateAgent(
            session,
            new CreateAgentOptions
            {
                Executor = Scripted().Brain,
                Cache = new AgentCacheOptions { Dir = Path.Combine(_dir, "cache") },
            }
        );
        // An agent with a cache names its recording.
        Assert.Equal(
            "INVALID_ARGUMENT",
            (await Assert.ThrowsAsync<TestException>(() => caching.ActAsync("checkout"))).Code
        );
        var live = CoreAgents.CreateAgent(session, new CreateAgentOptions { Executor = Scripted().Brain });
        // CacheKey needs an agent with a cache.
        Assert.Equal(
            "INVALID_ARGUMENT",
            (
                await Assert.ThrowsAsync<TestException>(() =>
                    live.ActAsync("checkout", new CachingActOptions { CacheKey = "checkout" })
                )
            ).Code
        );
        var noModel = await Assert.ThrowsAsync<AgentException>(() =>
            CoreAgents.CreateAgent(session, new CreateAgentOptions()).ActAsync("anything")
        );
        Assert.Equal("MODEL_UNAVAILABLE", noModel.Code);
        Assert.Contains("pass one to CreateAgent", noModel.Message, StringComparison.Ordinal);
        // The C# types hold both forms of act, so the runtime refuses the wrong one, as it does for options held in a variable upstream.
        Assert.Equal(
            "INVALID_ARGUMENT",
            (await Assert.ThrowsAsync<TestException>(() => caching.ActAsync("checkout", new CachingActOptions()))).Code
        );
        Assert.Equal(
            "INVALID_ARGUMENT",
            Assert.Throws<TestException>(() => CoreAgents.CreateAgent(null!, new CreateAgentOptions())).Code
        );
        await session.CloseAsync();
    }
}
