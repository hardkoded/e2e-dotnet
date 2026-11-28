// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop.MacOs;

namespace E2E.Desktop.Tests.MacOsDriver;

/// <summary>Ported from the <c>tests</c> module of <c>crates/e2e-desktop-napi/src/ax.rs</c>.</summary>
public sealed class AxTests
{
    [Fact]
    public void Fails_a_batch_read_on_a_stalled_or_gone_slot_and_reads_any_other_error_as_no_value()
    {
        var read = Element.Slots([
            AxResult<AxValue>.Ok(new AxValue.TextValue("Name")),
            AxResult<AxValue>.Err(AxError.NoValue),
            AxResult<AxValue>.Err(AxError.AttributeUnsupported),
        ]);
        Assert.True(read.IsOk);
        Assert.IsType<AxValue.TextValue>(read.Value![0]);
        Assert.Null(read.Value[1]);
        Assert.Null(read.Value[2]);

        var stalled = Element.Slots([
            AxResult<AxValue>.Ok(new AxValue.TextValue("Name")),
            AxResult<AxValue>.Err(AxError.CannotComplete),
        ]);
        Assert.Equal(AxError.CannotComplete, stalled.Error);

        var gone = Element.Slots([
            AxResult<AxValue>.Err(AxError.InvalidElement),
            AxResult<AxValue>.Ok(new AxValue.BoolValue(true)),
        ]);
        Assert.Equal(AxError.InvalidElement, gone.Error);
    }

    [Fact]
    public void Shows_a_number_in_its_shortest_form()
    {
        Assert.Equal("1", AxFormat.Number(1.0));
        Assert.Equal("0.1", AxFormat.Number(0.1));
        Assert.Equal("0.00001", AxFormat.Number(0.00001));
        Assert.Equal("1000000000000000000000", AxFormat.Number(1e21));
        Assert.Equal("-2.5", AxFormat.Number(-2.5));
    }
}
