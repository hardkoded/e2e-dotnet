// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.NUnit.Tests.McpTestRun;

/// <summary>
/// <c>describe('the replay cache')</c> of <c>tests/integration/mcp-test-run.test.ts</c> (tester-army/e2e#1315). The one <c>it</c> runs
/// <see cref="NUnitSessionTestRunner"/> with the host's config (a scripted model and a cache directory), the seam <c>e2e mcp</c> hands the
/// config it loaded through, and not <c>SessionHost</c> itself. Its agent step reads the test's title, its steps are recorded, the next session
/// replays them with no model call, and so does <c>e2e run</c>, through <c>Runner</c>, over an in-memory test of the same title: the title of a
/// session's test is its NUnit full name, where upstream's is the <c>test()</c> title.
/// </summary>
public sealed class TheReplayCacheTests
{
    private const string Instruction = "increment the counter twice";

    [Test]
    [NonParallelizable]
    public async Task Records_a_sessions_agent_steps_for_the_next_session_and_for_e2e_run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "e2e-session-replay-cache", Guid.NewGuid().ToString("n"));
        var name = typeof(ReplayCacheFixture).FullName + "." + nameof(ReplayCacheFixture.Increments_the_counter);
        var model = new ScriptedModel(request =>
        {
            var tapped = request.Messages.Count(message => message.Role == "tool" && message.Content!.Contains("Tapped", StringComparison.Ordinal));
            return tapped >= 2 ? ModelResponses.Done("passed", "The counter shows 2.") : ModelResponses.Tap("button", "Increment");
        });
        var host = E2EConfig.Parse("""{ "cache": { "mode": "read-write", "dir": ".e2e/cache" } }""", directory, _ => null).WithModel(model);
        Directory.CreateDirectory(directory);
        try
        {
            await RunAndCloseAsync(name, host);
            var modelCalls = model.CallCount;
            Assert.That(modelCalls, Is.GreaterThan(0));

            // Its agent steps read the test's title, as a run's do.
            Assert.That(model.Requests[0].Messages[0].Content, Does.Contain("Test: " + name));
            Assert.That(Directory.GetFiles(Path.Combine(directory, ".e2e", "cache")), Has.Length.EqualTo(1));

            // The next session replays what the first recorded, with no model call.
            await RunAndCloseAsync(name, host);
            Assert.That(model.CallCount, Is.EqualTo(modelCalls));

            // And so does e2e run: the key is the run's.
            var run = await Runner.RunAsync(new RunOptions
            {
                Cwd = directory,
                RawConfig = $$"""{ "cache": { "mode": "read-write", "dir": ".e2e/cache" }, "targets": [{ "name": "web", "app": { "url": "{{E2E.Playground.Testbed.Url}}" } }] }""",
                Tests = new InMemoryTests("tests/replay-cache.e2e", [new InMemoryTest(name, async session =>
                {
                    await session.App.OpenAsync("/speed");
                    await session.Agent.ActAsync(Instruction);
                    await Expect.That(session.Screen.GetByLabel("Count")).ToHaveTextAsync("2");
                })]),
                Env = new Dictionary<string, string?> { ["CI"] = "" },
                Agents = new Dictionary<string, AgentOptions> { [host.AgentNames[0]] = host.Agent.CreateOptions() },
            });
            Assert.That(run.ExitCode, Is.EqualTo(0));
            var act = run.Report.Run.Results.Single(result => result.TitlePath.SequenceEqual([name])).Attempts[^1].Steps.Single(step => step.Api == "agent.act");
            Assert.That(act.Status, Is.EqualTo(E2E.Reports.StepStatus.Passed));
            Assert.That((act.Cache?.Mode, act.Cache?.ReplayedActions, act.Cache?.TotalActions), Is.EqualTo((E2E.Reports.CacheMode.SelfFinalized, 2, 2)));
            Assert.That(act.Metrics?.ModelCalls, Is.EqualTo(0));
            Assert.That(model.CallCount, Is.EqualTo(modelCalls));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>Runs the named test as a session on the host's config and closes it as <c>e2e mcp</c>'s <c>close_session</c> does.</summary>
    private static async Task RunAndCloseAsync(string name, E2EConfig host)
    {
        var outcome = await new NUnitSessionTestRunner().RunAsync(typeof(ReplayCacheFixture).Assembly, name, CancellationToken.None, host);

        Assert.That(outcome, Is.Not.Null);
        await using var closing = outcome!;
        var session = outcome!.Session!;
        Assert.That(outcome.Failure, Is.Null);
        session.Complete(outcome.Failure);
    }

    /// <summary>The test <see cref="TheReplayCacheTests"/> runs: its own config turns the cache off and names no model, so only the host's applies.</summary>
    [Explicit("Run by TheReplayCacheTests")]
    public sealed class ReplayCacheFixture : E2ETest
    {
        private static readonly E2EConfig FixtureConfig = E2EConfig.Parse("""{ "cache": { "mode": "off" } }""", Path.GetTempPath(), _ => null);

        protected override E2EConfig Config => FixtureConfig;

        protected override string? BaseUrl => E2E.Playground.Testbed.Url;

        [Test]
        public async Task Increments_the_counter()
        {
            await App.OpenAsync("/speed");
            await Agent.ActAsync(Instruction);
            await Expect.That(Screen.GetByLabel("Count")).ToHaveTextAsync("2");
        }
    }
}
