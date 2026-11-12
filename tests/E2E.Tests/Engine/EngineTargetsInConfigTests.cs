// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.Engine;

/// <summary>
/// Upstream's <c>describe('engine targets in config')</c> (<c>engine.test.ts</c>). Not ported: upstream's <c>rejects a non-handle engine value</c>.
/// C# types a target's engine as <see cref="IEngine"/>, so a raw object does not compile and nothing is left to check at run time.
/// </summary>
public sealed class EngineTargetsInConfigTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "e2e-engine-config");

    [Fact]
    public void Rejects_an_empty_platform_declaration()
    {
        var error = Assert.Throws<ConfigurationException>(() =>
            EngineManifest.DefineEngine(new EngineSpec { Name = "x", Version = "1", SpiVersion = 1, Platform = " " }));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains("platform must be a non-empty string when declared", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolves_an_engine_target_on_any_platform_without_an_app_url()
    {
        var config = E2EConfig.Parse("{}", Root).WithTarget("ios-simulator", new ToyEngine("ios"));
        var target = ResolvedConfig.Resolve(config).Targets.Single(item => item.Name == "ios-simulator");
        Assert.Equal("toy", target.Engine?.Name);
        Assert.Equal("ios", target.Platform);
        Assert.Null(target.App.Url);
        Assert.Null(target.App.Identity);
    }

    [Fact]
    public void Accepts_a_video_mode_on_a_target_without_an_engine_the_runner_grades_it_later()
    {
        var config = E2EConfig.Parse("""{ "targets": [{ "name": "ios", "platform": "ios", "video": "on" }] }""", Root);
        Assert.Equal(new ResolvedRecording(RecordingMode.On, RecordingSource.Target), config.Targets.Single(item => item.Name == "ios").Video);
    }

    /// <summary>
    /// Partial: upstream reads the options through <c>resolveConfig</c> (<c>config.agent</c>). The port resolves an agent with
    /// <see cref="ResolvedAgent.From"/>, and the config file cannot hold an executor, so the test builds one in code.
    /// </summary>
    [Fact]
    public void Partial_Accepts_agent_options_alongside_an_executor()
    {
        var executor = new CustomBrain();
        var agent = ResolvedAgent.From("default", new AgentOptions { Executor = executor, MaxModelCalls = 40, Context = "device hints" });
        Assert.Same(executor, agent.Executor);
        Assert.Equal(40, agent.MaxModelCalls);
        Assert.Equal("device hints", agent.Context);
    }

    private sealed class ToyEngine(string platform) : IEngine
    {
        public string Name => "toy";

        public string Platform => platform;

        public string Version => "1.0.0";

        public EngineCapabilities Capabilities => EngineCapabilities.None;

        public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class CustomBrain : IStepExecutor
    {
        public string Name => "custom-brain";

        public Task<StepVerdict> RunStepAsync(StepExecutorContext context, CancellationToken cancellationToken) =>
            Task.FromResult(new StepVerdict(StepVerdictStatus.Passed, "ok"));
    }
}
