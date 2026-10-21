// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.EngineLocate;

/// <summary>.NET-only: a screen whose engine resolves locators itself, as upstream's engines all do. Upstream has no <c>it</c> for core's side of it.</summary>
public sealed class EngineLocateTests
{
    private static readonly SemanticNode Banner = new() { Ref = "banner", Role = "alert", Name = "Saved" };

    /// <summary>
    /// A deadline no loaded runner outruns: a test that waits for an answer after N retries needs N poll sleeps to fit inside it,
    /// and a sleep can last far longer than its interval on a busy machine.
    /// </summary>
    private static readonly TimeSpan Patient = TimeSpan.FromSeconds(60);

    private static Screen ScreenOver(Func<int, IReadOnlyList<SemanticNode>> locate, out Func<int> calls, out Func<int> observes, TimeSpan? timeout = null)
    {
        var located = 0;
        var observed = 0;
        calls = () => located;
        observes = () => observed;
        return new Screen(
            _ =>
            {
                observed++;
                return Task.FromResult(new Observation { Route = "/", Roots = [] });
            },
            (_, _, _) => Task.CompletedTask,
            () => CancellationToken.None,
            () => { },
            timeout ?? ScreenFixture.Timeout,
            timeout ?? ScreenFixture.Timeout,
            locate: (_, _, _) => Task.FromResult(locate(++located)));
    }

    [Fact]
    public async Task Resolves_through_the_engine_and_never_observes()
    {
        var screen = ScreenOver(_ => [Banner], out _, out var observes);
        Assert.Equal(1, await screen.GetByRole("alert").CountAsync());
        Assert.Equal(0, observes());
    }

    [Fact]
    public async Task Retries_a_retryable_engine_error_until_the_locate_answers()
    {
        var screen = ScreenOver(
            attempt => attempt < 3 ? throw new EngineException("NODE_STALE", "the screen moved", retryable: true) : [Banner],
            out var calls,
            out _,
            Patient);
        await screen.GetByRole("alert").WaitForAsync();
        Assert.Equal(3, calls());
    }

    [Fact]
    public async Task Fails_with_the_engine_error_when_the_deadline_passes_on_a_retryable_one()
    {
        var screen = ScreenOver(_ => throw new EngineException("NODE_STALE", "the screen moved", retryable: true), out var calls, out _, TimeSpan.FromSeconds(1));
        var failure = await Assert.ThrowsAsync<EngineException>(() => screen.GetByRole("alert").CountAsync());
        Assert.Equal("NODE_STALE", failure.Code);
        Assert.True(calls() > 1);
    }

    [Fact]
    public async Task Fails_at_once_on_an_engine_error_that_is_not_retryable()
    {
        var screen = ScreenOver(_ => throw new EngineException("ENGINE_FAILURE", "the device went away"), out var calls, out _);
        var failure = await Assert.ThrowsAsync<EngineException>(() => screen.GetByRole("alert").CountAsync());
        Assert.Equal("ENGINE_FAILURE", failure.Code);
        Assert.Equal(1, calls());
    }

    [Fact]
    public async Task Holds_a_visible_query_to_its_predicate_whatever_the_engine_answers()
    {
        var hidden = new SemanticNode { Ref = "gone", Role = "alert", Name = "Saved", States = new NodeStates { Hidden = true } };
        var screen = ScreenOver(_ => [hidden, Banner], out _, out _);
        Assert.Equal(1, await screen.GetByRole("alert", new RoleOptions { Visible = true }).CountAsync());
    }
}
