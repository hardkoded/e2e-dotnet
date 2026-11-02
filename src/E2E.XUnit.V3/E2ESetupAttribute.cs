// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using Xunit;

namespace E2E.XUnit.V3;

/// <summary>
/// Marks a test of an <see cref="E2ETest"/> class as a setup test that saves sessions. Upstream's <c>test.setup(title, { sessions }, fn)</c>.
/// It is a test: it takes the place of <c>[Fact]</c>, and <see cref="Title"/> is the test's display name. Its body signs in and calls
/// <c>SaveSessionAsync</c> once for each name in <see cref="Saves"/>; a name saved twice, a name not declared, or a declared name left
/// unsaved fails the test with <c>SESSION_CONTRACT</c>. The sessions are saved when the test passes. A test that restores one is marked
/// <see cref="E2ESessionAttribute"/>.
/// <para>
/// xUnit decides the order, not the attribute's dependents: <see cref="E2ETest"/> runs the setup tests of a class before its other tests
/// (<see cref="SetupTestMethodOrderer"/>), and no earlier than that. Put the setup tests and the tests that use their sessions in one class,
/// or order the classes yourself: xUnit runs classes in different collections in parallel, so a test in another class may restore a session
/// before it is saved. A test that restores a session nobody has saved yet fails with <c>SESSION_UNAVAILABLE</c>. Unlike upstream's runner,
/// xUnit runs a setup test even when no selected test needs its session.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class E2ESetupAttribute : FactAttribute, ISessionSetup
{
    /// <summary>Creates a setup test named <paramref name="title"/>.</summary>
    public E2ESetupAttribute(string title, [CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        Title = string.IsNullOrWhiteSpace(title) ? throw new ArgumentException("a setup test needs a title", nameof(title)) : title;
        DisplayName = title;
    }

    /// <summary>The test's name.</summary>
    public string Title { get; }

    /// <summary>The names of the sessions this test saves. At least one.</summary>
    public string[] Saves { get; init; } = [];

    IReadOnlyList<string> ISessionSetup.Saves => Saves;
}
