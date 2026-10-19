// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace E2E;

/// <summary>The agent pin and the <c>agentContext</c> of a test, resolved from its method, its class, and the classes around it, as upstream's <c>resolveOptions</c> does for groups.</summary>
internal static class TestAgentOptions
{
    /// <summary>The closest <see cref="AgentAttribute"/>, or null.</summary>
    public static string? Agent(Type type, MethodBase? method)
    {
        return Layers(type, method).Select(layer => layer.GetCustomAttribute<AgentAttribute>(false)?.Name).LastOrDefault(name => name is not null);
    }

    /// <summary>Every <see cref="AgentContextAttribute"/>, outer to inner, one per line, or null when there is none.</summary>
    public static string? AgentContext(Type type, MethodBase? method)
    {
        var parts = Layers(type, method).Select(layer => layer.GetCustomAttribute<AgentContextAttribute>(false)?.Text).Where(text => !string.IsNullOrWhiteSpace(text)).ToArray();
        return parts.Length == 0 ? null : string.Join('\n', parts);
    }

    // Outermost first: each enclosing class with its base classes, then the test class, then the method.
    private static IEnumerable<MemberInfo> Layers(Type type, MethodBase? method)
    {
        var enclosing = new List<Type>();
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            enclosing.Insert(0, current);
        }

        var seen = new HashSet<Type>();
        foreach (var outer in enclosing)
        {
            var chain = new List<Type>();
            for (var current = outer; current is not null && current != typeof(object); current = current.BaseType)
            {
                chain.Insert(0, current);
            }

            foreach (var layer in chain.Where(seen.Add))
            {
                yield return layer;
            }
        }

        if (method is not null)
        {
            yield return method;
        }
    }

    /// <summary>The run's agent: <c>E2E_AGENT</c>, upstream's <c>--agent</c>. A test's pin wins over it.</summary>
    public static string? RunAgent()
    {
        return Environment.GetEnvironmentVariable("E2E_AGENT")?.Trim() is { Length: > 0 } name ? name : null;
    }
}
