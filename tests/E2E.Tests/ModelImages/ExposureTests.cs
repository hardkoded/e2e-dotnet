// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.ModelImages;

/// <summary>What one attempt knows about how far a secret reached it. A .NET-only test: upstream proves it through its runner.</summary>
public sealed class ExposureTests
{
    [Fact]
    public void Withholds_pixels_only_after_a_secret_is_filled()
    {
        var exposure = new Exposure();
        Assert.False(exposure.WithholdsModelPixels);
        exposure.RaiseFilled();
        Assert.True(exposure.WithholdsModelPixels);
    }
}
