// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace E2E;

/// <summary>
/// Starts a test, or every test of a class, from a session a setup test saved. Upstream's <c>session</c> option of <c>test</c> and
/// <c>describe</c>. The session restores before the test body runs; a restore leaves no page open, so the test opens one first. The
/// setup test must run before the test (in NUnit, give it an earlier <c>[Order]</c>); a session no setup produced fails the test with
/// <c>SESSION_UNAVAILABLE</c>, and an engine that cannot restore state with <c>UNSUPPORTED_CAPABILITY</c>. The closest attribute wins:
/// the method, then the class, then a base class. .NET-only name, as the port has no <c>test()</c> options object.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class E2ESessionAttribute(string name) : Attribute
{
    /// <summary>The name a setup test saved the session under.</summary>
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("session must be the name of a saved session", nameof(name)) : name;
}

/// <summary>
/// What a framework adapter's setup attribute says about a test: it is a setup test, and it saves these sessions (upstream's
/// <c>test.setup(title, { sessions }, fn)</c>). .NET-only: the adapter declares the attribute, because the test framework owns what a
/// test is; <c>E2E.NUnit.E2ESetupAttribute</c> is the NUnit one.
/// </summary>
public interface ISessionSetup
{
    /// <summary>The names the setup test saves, each exactly once, with <c>SaveSessionAsync</c>.</summary>
    IReadOnlyList<string> Saves { get; }
}

/// <summary>The session names of a test, read from its method and class.</summary>
internal static class SessionAttributes
{
    /// <summary>The session the test restores: the closest <see cref="E2ESessionAttribute"/>, or null.</summary>
    public static string? Restore(Type type, MethodBase? method)
    {
        if (method?.GetCustomAttribute<E2ESessionAttribute>(false) is { } own)
        {
            return own.Name;
        }

        // The class, its base classes, then the enclosing class with its own bases.
        for (var outer = type; outer is not null; outer = outer.DeclaringType)
        {
            for (var current = outer; current is not null && current != typeof(object); current = current.BaseType)
            {
                if (current.GetCustomAttribute<E2ESessionAttribute>(false) is { } found)
                {
                    return found.Name;
                }
            }
        }

        return null;
    }

    /// <summary>The names the test declares it saves, or null when it is not a setup test.</summary>
    public static IReadOnlyList<string>? Saves(MethodBase? method) =>
        method?.GetCustomAttributes(true).OfType<ISessionSetup>().FirstOrDefault()?.Saves;
}
