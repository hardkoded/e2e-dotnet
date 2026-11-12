// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.TUnit.Tests.EngineContract;
using TUnit.Core;

namespace E2E.TUnit.Tests.E2ETest;

/// <summary>
/// .NET-only: every [Retry] attempt starts its own session, and every one of them is ended, so a failed attempt leaves no browser behind.
/// </summary>
[NotInParallel(nameof(RetrySessionsTests))]
public sealed class RetrySessionsTests : BrowserTest
{
    private static readonly List<StateSpyEngine> Spies = [];
    private static int _attempts;

    protected override IEngine CreateEngine()
    {
        var spy = new StateSpyEngine(base.CreateEngine());
        lock (Spies)
        {
            Spies.Add(spy);
        }

        return spy;
    }

    [Test]
    [Retry(2)]
    public async Task Fails_the_first_attempt_then_passes()
    {
        await Expect.That(Screen.GetByLabel("Loads")).ToBeVisibleAsync();
        if (Interlocked.Increment(ref _attempts) == 1)
        {
            throw new InvalidOperationException("the first attempt fails");
        }
    }

    [Test]
    [DependsOn(nameof(Fails_the_first_attempt_then_passes))]
    public async Task Ends_the_session_of_every_attempt()
    {
        await Assert.That(_attempts).IsEqualTo(2);
        StateSpyEngine[] spies;
        lock (Spies)
        {
            // This test's own session is the third.
            spies = [.. Spies.Take(2)];
        }

        await Assert.That(spies.Length).IsEqualTo(2);
        await Assert.That(spies.Select(spy => spy.Started).ToArray()).IsEquivalentTo(new[] { 1, 1 });
        await Assert.That(spies.Select(spy => spy.Ended).ToArray()).IsEquivalentTo(new[] { 1, 1 });
    }
}
