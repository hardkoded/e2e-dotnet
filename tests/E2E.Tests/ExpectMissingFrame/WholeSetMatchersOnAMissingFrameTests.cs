// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using E2E.Engine;

namespace E2E.Tests.ExpectMissingFrame;

/// <summary>
/// Upstream's expect-missing-frame.test.ts. The port runs on the real clock where upstream fakes time, so the
/// negated matchers wait out their one-second grace for real. The two failure tests also check the structured <see cref="ErrorDetails.Observed"/>.
/// </summary>
public sealed class WholeSetMatchersOnAMissingFrameTests : IDisposable
{
    private static readonly SemanticNode Pay = new() { Ref = "pay", Role = "button", Name = "Pay", Text = "Pay" };

    private readonly List<Timer> _timers = [];

    public void Dispose()
    {
        foreach (var timer in _timers)
        {
            timer.Dispose();
        }
    }

    /// <summary>A screen whose every locate reports the frame missing until <paramref name="frameAfter"/>, then finds <c>Pay</c>.</summary>
    private Locator ClosedFrame(TimeSpan? frameAfter = null)
    {
        var frame = new StrongBox<bool>(false);
        if (frameAfter is { } after)
        {
            _timers.Add(new Timer(_ => Volatile.Write(ref frame.Value, true), null, after, Timeout.InfiniteTimeSpan));
        }

        var screen = new Screen(
            _ => Task.FromResult(new Observation { Route = "/", Roots = [] }),
            (_, _, _) => Task.CompletedTask,
            () => CancellationToken.None,
            () => { },
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5),
            locate: (_, _, _) => Volatile.Read(ref frame.Value)
                ? Task.FromResult<IReadOnlyList<SemanticNode>>([Pay])
                : throw new EngineException(EngineErrorCodes.FrameNotFound, "no frame matches #checkout", retryable: true));
        return screen.GetByRole("button");
    }

    [Fact]
    public async Task Read_it_as_zero_matches_as_count_and_toBeHidden_do()
    {
        var button = ClosedFrame();
        Assert.Equal(0, await button.CountAsync());
        await Expect.That(button).ToBeHiddenAsync();
        await Expect.That(button).ToHaveCountAsync(0);
        await Expect.That(button).Not.ToHaveCountAsync(2);
        await Expect.That(button).ToHaveTextAsync(Array.Empty<TextMatch>());
        await Expect.That(button).Not.ToContainTextAsync(new TextMatch[] { "Pay" });
    }

    [Fact]
    public async Task Fail_a_positive_count_as_a_count_mismatch_not_a_missing_locator()
    {
        var failure = await Assert.ThrowsAsync<TestException>(() => Expect.That(ClosedFrame()).ToHaveCountAsync(1, TimeSpan.FromMilliseconds(300)));
        Assert.Equal("ASSERTION_FAILED", failure.Code);
        Assert.Contains("observed: count 0", failure.Message);
        Assert.Equal("count 0", failure.Details?.Observed);
    }

    [Fact]
    public async Task Fail_a_text_list_as_a_text_mismatch_not_a_missing_locator()
    {
        var failure = await Assert.ThrowsAsync<TestException>(() => Expect.That(ClosedFrame()).ToHaveTextAsync(new TextMatch[] { "Pay" }, timeout: TimeSpan.FromMilliseconds(300)));
        Assert.Equal("ASSERTION_FAILED", failure.Code);
        Assert.Contains("observed: text []", failure.Message);
        Assert.Equal("text []", failure.Details?.Observed);
    }

    [Fact]
    public async Task Keep_polling_until_the_frame_appears()
    {
        var button = ClosedFrame(TimeSpan.FromMilliseconds(400));
        await Expect.That(button).ToHaveCountAsync(1);
        await Expect.That(button).ToHaveTextAsync(new TextMatch[] { "Pay" });
    }
}
