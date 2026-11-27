// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop;
using static E2E.Desktop.Tests.Driver.DriverTestSupport;

namespace E2E.Desktop.Tests.Driver;

/// <summary>Ported from <c>describe('call: onCutOff')</c> in upstream's <c>packages/desktop/tests/unit/driver.test.ts</c>.</summary>
public sealed class CallOnCutOffTests
{
    [Fact]
    public async Task Runs_when_the_budget_or_the_signal_cuts_the_call_off_and_not_when_the_driver_answers()
    {
        var cutOff = 0;
        void OnCutOff() => cutOff++;
        DriverCall Options(bool input, int timeoutMs, CancellationToken signal = default) =>
            new()
            {
                Label = "type",
                Input = input,
                Signal = signal,
                TimeoutMs = timeoutMs,
                OnCutOff = OnCutOff,
            };
        var timedOut = await Assert.ThrowsAsync<EngineException>(() =>
            DesktopDriverCalls.CallAsync(
                () => Later(Envelope<Nothing>.Success(Nothing.Value), 2_000),
                Options(true, 100)
            )
        );
        Assert.Equal("ACTION_MAY_HAVE_COMMITTED", timedOut.Code);
        using var controller = new CancellationTokenSource();
        controller.CancelAfter(50);
        var cancelled = await Assert.ThrowsAsync<EngineException>(() =>
            DesktopDriverCalls.CallAsync(
                () => Later(Envelope<Nothing>.Success(Nothing.Value), 2_000),
                Options(true, 5_000, controller.Token)
            )
        );
        Assert.Equal("CANCELLED", cancelled.Code);
        await DesktopDriverCalls.CallAsync(
            () => Task.FromResult(Envelope<Nothing>.Success(Nothing.Value)),
            Options(true, 1_000)
        );
        Assert.Equal(2, cutOff);
    }
}
