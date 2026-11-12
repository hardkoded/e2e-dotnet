// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using TUnit.Core;
using TUnit.Core.Enums;
using TUnit.Core.Interfaces;

namespace E2E.TUnit;

/// <summary>
/// Marks a test of an <see cref="E2ETest"/> class as a setup test that saves sessions. Upstream's <c>test.setup(title, { sessions }, fn)</c>.
/// It goes beside <c>[Test]</c> (TUnit does not let a library define a test attribute), and <see cref="Title"/> becomes the test's display name. Its body signs in and calls
/// <c>SaveSessionAsync</c> once for each name in <see cref="Saves"/>; a name saved twice, a name not declared, or a declared name left
/// unsaved fails the test with <c>SESSION_CONTRACT</c>. The sessions are saved when the test passes. A test that restores one is marked
/// <see cref="E2ESessionAttribute"/>.
/// <para>
/// A test that restores a session nobody has saved yet runs the setup test that declares it first, once per run, so a dependent can run
/// alone (upstream's runner schedules the setup too). The dependent runs it on a new instance of the class (parameterless constructor), in a
/// session of its own and without the class's own <c>[Before(Test)]</c> and <c>[After(Test)]</c> hooks; exactly one setup test of the assembly must
/// declare the name, or the restore fails with <c>SESSION_UNAVAILABLE</c>, as it does when the setup fails. TUnit then skips that setup test
/// when its turn comes, and still runs one that no selected test needs, which upstream's runner does not. The setup test gets the highest
/// execution priority, so TUnit schedules it before the other tests.
/// </para>
/// </summary>
public sealed class E2ESetupAttribute : Attribute, ISessionSetup, ITestDiscoveryEventReceiver
{
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

    /// <inheritdoc />
    public int Order => 0;

    /// <summary>Names the test and schedules it first.</summary>
    public ValueTask OnTestDiscovered(DiscoveredTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.SetDisplayName(Title);
        context.SetPriority(Priority.Critical);
        return default;
    }
}
