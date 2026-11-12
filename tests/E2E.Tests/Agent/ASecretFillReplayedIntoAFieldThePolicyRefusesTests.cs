// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.Agent;

/// <summary>
/// .NET-only test (no upstream <c>it</c>): a recorded secret fill replays through the full secret authorization before the provider
/// is asked. Upstream's replay calls the dispatcher's <c>typeSecret</c>, which runs <c>authorizeSecretFill</c> first; the port's replay
/// host did not (issue #1315).
/// </summary>
public sealed class ASecretFillReplayedIntoAFieldThePolicyRefusesTests
{
    // The counter page with a code field, disabled once the app changed.
    private static DocumentWorld World(bool disabled) =>
        new DocumentWorld().Map("/", page =>
        {
            var count = 0;
            var counter = page.Status("Counter");
            counter.Text = "0";
            page.Button("Increment", () => counter.Text = (++count).ToString(CultureInfo.InvariantCulture));
            page.Textbox("Code").Disabled = disabled;
        });

    [Fact]
    public async Task Does_not_ask_the_provider_when_the_replayed_fill_is_refused()
    {
        var directory = Directory.CreateTempSubdirectory("e2e-replay-secret-").FullName;
        try
        {
            var providerCalls = 0;
            var liveRuns = 0;

            async Task<(Exception? Failure, string? Cache)> RunAsync(bool disabled)
            {
                var secret = Secret.Create("totp", _ => Task.FromResult("fresh-" + Interlocked.Increment(ref providerCalls)));
                var executor = new TestExecutor("secret-filler", async (context, token) =>
                {
                    Interlocked.Increment(ref liveRuns);
                    var observation = await context.ObserveAsync(cancellationToken: token);
                    await context.Actions.TypeSecretAsync(TargetFor(observation.Text, "textbox \"Code\""), "totp", token);

                    // The fill leaves nothing on screen, so the counter is the step's visible effect, which makes it recordable.
                    await context.Actions.TapAsync(TargetFor(observation.Text, "button \"Increment\""), token);
                    return Passed("filled");
                })
                {
                    Version = "test",
                };
                await using var session = await E2ESession.StartAsync(new E2ESessionOptions
                {
                    Engine = new DocumentEngine(World(disabled)),
                    AgentExecutor = executor,
                    BaseUrl = "https://counter.test",
                    AppIdentity = "fixture-app",
                    Cache = new FileStepCache(directory),
                    CacheEnabled = true,
                    CacheMode = CacheMode.ReadWrite,
                    TestTitle = "fills the code",
                });
                await session.App.OpenAsync("/");
                Exception? failure = null;
                string? cache = null;
                try
                {
                    var result = await session.Agent.ActAsync("fill the code", new ActOptions { Params = new Dictionary<string, object?> { ["code"] = secret } });
                    cache = result.Cache?.Mode;

                    // A recording is kept once a later assertion verifies the step.
                    await Expect.That(session.Screen.GetByRole("status")).ToHaveTextAsync("1");
                }
                catch (AgentException error)
                {
                    failure = error;
                }

                session.Complete(failure);
                return (failure, cache);
            }

            Assert.Null((await RunAsync(disabled: false)).Failure);
            Assert.Equal((1, 1), (providerCalls, liveRuns));
            Assert.Single(Directory.GetFiles(directory, "*", SearchOption.AllDirectories));

            // The app changed: the field is disabled now. The replayed fill is refused as a live one is, before the provider is asked for
            // a value; the step then hands to the executor, whose own live fill is refused the same way.
            providerCalls = 0;
            var (failure, _) = await RunAsync(disabled: true);
            Assert.Equal((0, 2), (providerCalls, liveRuns));
            var denied = Assert.IsType<AgentException>(failure);
            Assert.Equal("POLICY_DENIED", denied.Code);
            Assert.Contains("the target field is disabled", denied.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
