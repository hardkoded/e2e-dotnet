// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.NUnit;
using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.TestsAgent;

/// <summary>
/// The base of the agentic suite: upstream's <c>apps/web-benchmark/e2e.agent.config.ts</c> over the same app and account as the
/// deterministic suite (<see cref="BenchmarkTest"/>). Every <c>act</c> and <c>assert</c> calls the model, so the suite is real-model.
/// The config is upstream's, in .NET keys: the model is the one the other real-model suites pin, and the context and the step
/// ceiling are upstream's. The replay cache lives in the project's <c>.e2e/cache</c>, as the other suites keep theirs.
/// </summary>
[Category("RealModel")]
public abstract class TestsAgentTest : BenchmarkTest
{
    private static readonly E2EConfig AgentConfig = E2EConfig.Parse(
        """
        {
          "targets": [
            { "name": "web", "platform": "web", "app": { "url": "http://127.0.0.1:4280" } }
          ],
          "timeout": 300000,
          "actionTimeout": 90000,
          "agents": {
            "default": {
              "model": "claude-sonnet-5.5",
              "provider": "copilot",
              "maxSteps": 60,
              "context": "This is the e2e web benchmark: a list of self-contained scenarios, each served at /e/<slug>. A step plays out inside the scenario page it starts on; the \"Benchmark Examples\" link in the header leaves it, so never follow it unless the step says so."
            }
          }
        }
        """,
        E2EConfig.Discover().ProjectRoot);

    protected override E2EConfig Config => AgentConfig;
}
