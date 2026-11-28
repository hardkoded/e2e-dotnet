// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop;
using static E2E.Desktop.Tests.Driver.DriverTestSupport;

namespace E2E.Desktop.Tests.Driver;

/// <summary>Ported from <c>describe('call')</c> in upstream's <c>packages/desktop/tests/unit/driver.test.ts</c>.</summary>
public sealed class CallTests
{
    private static DriverCall Options(
        string label,
        bool input,
        int timeoutMs,
        CancellationToken signal = default,
        Action? onCutOff = null
    ) =>
        new()
        {
            Label = label,
            Input = input,
            Signal = signal,
            TimeoutMs = timeoutMs,
            OnCutOff = onCutOff,
        };

    [Fact]
    public async Task Returns_the_envelopes_data_and_throws_the_failure_the_driver_reported_untranslated()
    {
        Assert.Equal(
            7,
            await DesktopDriverCalls.CallAsync(
                () => Task.FromResult(Envelope<int>.Success(7)),
                Options("read", false, 1_000)
            )
        );
        var thrown = await Assert.ThrowsAsync<DriverFailure>(() =>
            DesktopDriverCalls.CallAsync(
                () =>
                    Task.FromResult(
                        Envelope<Nothing>.Failure(
                            new DriverError("STALE_REF", "gone", new Disposition("not_delivered"))
                        )
                    ),
                Options("click", true, 1_000)
            )
        );
        Assert.Equal("STALE_REF", thrown.Code);
        Assert.True(thrown.NotDelivered);
    }

    [Fact]
    public async Task Reports_an_input_cut_off_by_the_budget_as_possibly_committed_and_a_read_as_timed_out()
    {
        var input = await Assert.ThrowsAsync<EngineException>(() =>
            DesktopDriverCalls.CallAsync(
                () => Later(Envelope<Nothing>.Success(Nothing.Value), 2_000),
                Options("click", true, 100)
            )
        );
        Assert.Equal("ACTION_MAY_HAVE_COMMITTED", input.Code);
        var read = await Assert.ThrowsAsync<EngineException>(() =>
            DesktopDriverCalls.CallAsync(
                () => Later(Envelope<Nothing>.Success(Nothing.Value), 2_000),
                Options("read", false, 100)
            )
        );
        Assert.Equal("OPERATION_TIMEOUT", read.Code);
    }

    [Fact]
    public async Task Gives_an_input_at_least_half_a_second_once_started_so_one_that_lands_is_not_reported_cut_off_and_a_read_exactly_its_budget()
    {
        Assert.Equal(
            "landed",
            await DesktopDriverCalls.CallAsync(
                () => Later(Envelope<string>.Success("landed"), 300),
                Options("click", true, 50)
            )
        );
        var read = await Assert.ThrowsAsync<EngineException>(() =>
            DesktopDriverCalls.CallAsync(() => Later(Envelope<string>.Success("late"), 300), Options("read", false, 50))
        );
        Assert.Equal("OPERATION_TIMEOUT", read.Code);
    }

    [Fact]
    public async Task Cancels_with_the_signal_and_starts_nothing_once_it_aborted()
    {
        using var controller = new CancellationTokenSource();
        controller.CancelAfter(50);
        var cancelled = await Assert.ThrowsAsync<EngineException>(() =>
            DesktopDriverCalls.CallAsync(
                () => Later(Envelope<Nothing>.Success(Nothing.Value), 2_000),
                Options("read", false, 5_000, controller.Token)
            )
        );
        Assert.Equal("CANCELLED", cancelled.Code);
        var started = false;
        var after = await Assert.ThrowsAsync<EngineException>(() =>
            DesktopDriverCalls.CallAsync(
                () =>
                {
                    started = true;
                    return Later(Envelope<Nothing>.Success(Nothing.Value), 10);
                },
                Options("read", false, 5_000, controller.Token)
            )
        );
        Assert.Equal("CANCELLED", after.Code);
        Assert.False(started);
    }

    [Fact]
    public async Task Reports_a_native_call_that_threw_as_an_engine_failure()
    {
        var crashed = await Assert.ThrowsAsync<EngineException>(() =>
            DesktopDriverCalls.CallAsync<Nothing>(
                () => Task.FromException<Envelope<Nothing>>(new InvalidOperationException("addon crashed")),
                Options("read", false, 1_000)
            )
        );
        Assert.Equal("ENGINE_FAILURE", crashed.Code);
        Assert.Contains("addon crashed", crashed.Message);
    }
}
