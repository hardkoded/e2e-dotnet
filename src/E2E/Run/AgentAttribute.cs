// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// Pins the agent that the calls of a test, or of every test in a class, use when they name none. Upstream's
/// <c>agent</c> option of <c>test</c> and <c>describe</c>. The closest pin wins: a call's <see cref="ActOptions.Agent"/>,
/// then the method, then the class, then an enclosing or base class, then the <c>E2E_AGENT</c> environment variable. Base and enclosing classes count although
/// the attribute is not inherited: <c>TestAgentOptions</c> reads each layer itself.
/// An unknown name fails the test with <c>INVALID_ARGUMENT</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AgentAttribute(string name) : Attribute
{
    /// <summary>The name of an <c>agents.&lt;name&gt;</c> entry.</summary>
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("agent must be the name of a configured agent", nameof(name)) : name;
}

/// <summary>
/// Appends facts only this test knows to the context of every agent, after the agent's own <c>context</c>. Upstream's
/// <c>agentContext</c> option of <c>test</c> and <c>describe</c>. Layers join outer to inner, one per line. Keep instructions
/// out of it: it reaches the judges too.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AgentContextAttribute(string text) : Attribute
{
    /// <summary>The text to append.</summary>
    public string Text { get; } = text;
}
