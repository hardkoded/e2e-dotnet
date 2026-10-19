// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace E2E;

/// <summary>
/// Which hook a step ran in. Upstream's runner moves the recorder into <c>beforeEach</c> and <c>afterEach</c> itself
/// (<c>enter</c> in <c>run/execute.ts</c>). The port has no runner: a test framework calls the hooks. So the step's call stack
/// answers instead. The first method outside the library that carries a test framework's mark names it: NUnit's <c>[SetUp]</c> is
/// <c>beforeEach</c> and its <c>[TearDown]</c> is <c>afterEach</c>, and a test method (<c>[Test]</c>, <c>[Fact]</c>) is the body.
/// With xUnit, which has no such marks, an <c>InitializeAsync</c> or <c>DisposeAsync</c> of a class that implements <c>IAsyncLifetime</c>, or of an <see cref="E2EFixture"/>, is the same pair. A <c>[OneTimeSetUp]</c> or a constructor names no hook. The
/// stack shows only the methods a call is still running in, so a step called after an <c>await</c> in a helper the hook called
/// belongs to the body, as the hook's own frame is gone by then.
/// </summary>
internal static class StepHooks
{
    private static readonly ConcurrentDictionary<MethodBase, Role> Roles = new();

    private enum Role
    {
        None,
        Body,
        BeforeEach,
        AfterEach,
    }

    /// <summary>The hook the call on <paramref name="stack"/> runs in; null for the body, or when no method says.</summary>
    public static StepPhase? PhaseOf(StackTrace stack)
    {
        var runner = typeof(StepHooks).Assembly;
        foreach (var frame in stack.GetFrames())
        {
            if (frame.GetMethod() is not { } method || method.DeclaringType?.Assembly == runner)
            {
                continue;
            }

            switch (Roles.GetOrAdd(method, Read))
            {
                case Role.BeforeEach:
                    return StepPhase.BeforeEach;
                case Role.AfterEach:
                    return StepPhase.AfterEach;
                case Role.Body:
                    return null;
            }
        }

        return null;
    }

    // A frame whose marks cannot be read (an attribute from an assembly that does not load) names no hook.
    private static Role Read(MethodBase frameMethod)
    {
        try
        {
            return RoleOf(frameMethod);
        }
        catch (Exception)
        {
            return Role.None;
        }
    }

    private static Role RoleOf(MethodBase frameMethod)
    {
        var method = Declared(frameMethod);
        var marks = method.GetCustomAttributes(inherit: true).SelectMany(static mark => Lineage(mark.GetType())).ToHashSet(StringComparer.Ordinal);
        if (marks.Contains("NUnit.Framework.SetUpAttribute"))
        {
            return Role.BeforeEach;
        }

        if (marks.Contains("NUnit.Framework.TearDownAttribute"))
        {
            return Role.AfterEach;
        }

        if (new[] { "NUnit.Framework.TestAttribute", "NUnit.Framework.TestCaseAttribute", "NUnit.Framework.TestCaseSourceAttribute", "NUnit.Framework.TheoryAttribute", "Xunit.FactAttribute" }.Any(marks.Contains))
        {
            return Role.Body;
        }

        // An xUnit test class sets up and tears down through IAsyncLifetime, which no attribute marks.
        if (method.DeclaringType is { } type && (typeof(E2EFixture).IsAssignableFrom(type) || type.GetInterfaces().Any(static contract => contract.FullName == "Xunit.IAsyncLifetime")))
        {
            return method.Name switch
            {
                "InitializeAsync" => Role.BeforeEach,
                "DisposeAsync" => Role.AfterEach,
                _ => Role.None,
            };
        }

        return Role.None;
    }

    // The method the compiler built a state machine for, when the frame is the state machine's.
    private static MethodBase Declared(MethodBase frameMethod)
    {
        if (frameMethod.DeclaringType is { DeclaringType: { } owner } machine && typeof(IAsyncStateMachine).IsAssignableFrom(machine))
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            return owner.GetMethods(all).FirstOrDefault(candidate => candidate.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType == machine) ?? frameMethod;
        }

        return frameMethod;
    }

    private static IEnumerable<string> Lineage(Type? type)
    {
        for (; type is not null; type = type.BaseType)
        {
            yield return type.FullName ?? type.Name;
        }
    }
}
