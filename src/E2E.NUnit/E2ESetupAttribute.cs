// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace E2E.NUnit;

/// <summary>
/// Marks a test of an <see cref="E2ETest"/> class as a setup test that saves sessions. Upstream's <c>test.setup(title, { sessions }, fn)</c>.
/// It is a test: it takes the place of <c>[Test]</c>, and <see cref="Title"/> is the test's name. Its body signs in and calls
/// <c>SaveSessionAsync</c> once for each name in <see cref="Saves"/>; a name saved twice, a name not declared, or a declared name left
/// unsaved fails the test with <c>SESSION_CONTRACT</c>. The sessions are saved when the test passes. A test that restores one is marked
/// <see cref="E2ESessionAttribute"/>.
/// <para>
/// NUnit decides the order, not the attribute's dependents: a setup test runs before the other tests of its own fixture, and
/// no earlier than that. Put the setup tests and the tests that use their sessions in one fixture, or give the fixtures an <c>[Order]</c>
/// that runs the setup fixture first. A test that restores a session nobody has saved yet fails with <c>SESSION_UNAVAILABLE</c>. Unlike upstream's
/// runner, NUnit runs a setup test even when no selected test needs its session.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class E2ESetupAttribute : TestAttribute, ISimpleTestBuilder, IApplyToTest, ISessionSetup
{
    // Before the default order (0 for an unordered test is last, as NUnit runs ordered tests first).
    private const int SetupOrder = int.MinValue / 2;

    /// <summary>Creates a setup test named <paramref name="title"/>.</summary>
    public E2ESetupAttribute(string title)
    {
        Title = string.IsNullOrWhiteSpace(title) ? throw new ArgumentException("a setup test needs a title", nameof(title)) : title;
    }

    /// <summary>The test's name.</summary>
    public string Title { get; }

    /// <summary>The names of the sessions this test saves. At least one.</summary>
    public string[] Saves { get; init; } = [];

    IReadOnlyList<string> ISessionSetup.Saves => Saves;

    /// <summary>Names the test and builds it. Only the name is the title: the full name stays the method's, so it is unique and filters keep working.</summary>
    public new TestMethod BuildFrom(IMethodInfo method, Test? suite)
    {
        var built = base.BuildFrom(method, suite);
        built.Name = Title;
        return built;
    }

    /// <summary>Gives the test an early order unless it has one, and refuses a setup test that saves nothing.</summary>
    public new void ApplyToTest(Test test)
    {
        ArgumentNullException.ThrowIfNull(test);
        base.ApplyToTest(test);
        if (Saves.Length == 0 || Saves.Any(string.IsNullOrWhiteSpace))
        {
            test.RunState = RunState.NotRunnable;
            test.Properties.Set(PropertyNames.SkipReason, "[E2ESetup] needs Saves with at least one session name");
        }

        if (!test.Properties.ContainsKey(PropertyNames.Order))
        {
            test.Properties.Set(PropertyNames.Order, SetupOrder);
        }
    }
}
