// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Acp;
using E2E.NUnit;

namespace AcpSample;

/// <summary>
/// Runs the steps of the TodoMVC demo on a coding agent the developer is signed in to, not on a model: Claude Code, or Codex when
/// <c>ACP_AGENT</c> is <c>codex</c>. The first run records each step, and the next run replays it without the agent.
/// </summary>
[Category("RealModel")]
[Category("AcpAgent")]
public sealed class AcpTodoTests : E2ETest
{
    // Built on first use, so a missing adapter fails the test with its own message, not a type initializer error.
    private static readonly Lazy<IStepExecutor> Executor = new(() => Environment.GetEnvironmentVariable("ACP_AGENT") == "codex"
        ? AcpExecutor.Codex()
        : AcpExecutor.ClaudeCode(new AcpAgentOptions { Model = Environment.GetEnvironmentVariable("ACP_MODEL") ?? "sonnet" }));

    protected override IStepExecutor? AgentExecutor => Executor.Value;

    [SetUp]
    public Task OpenTodoMvcAsync() => App.OpenAsync();

    [Test]
    public async Task Adds_a_todo_through_the_coding_agent()
    {
        await Agent.ActAsync("add a todo named Buy milk");

        await Expect.That(Screen.GetByTestId("todo-item").Filter("Buy milk")).ToBeVisibleAsync();
        await Agent.AssertAsync("the list shows Buy milk, and it is not completed");
    }

    [Test]
    public async Task Completes_a_todo_through_the_coding_agent()
    {
        await Agent.ActAsync("add a todo named Walk the dog, then mark it as completed");

        await Expect.That(Screen.GetByText("0 items left")).ToBeVisibleAsync();
        await Agent.AssertAsync("Walk the dog is marked as completed");
    }
}
