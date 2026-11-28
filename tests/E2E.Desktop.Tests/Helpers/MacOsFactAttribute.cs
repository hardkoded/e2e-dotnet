// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Desktop.Tests;

/// <summary>.NET-only: a test that needs a Mac, skipped anywhere else. Upstream's <c>it.skipIf(process.platform !== 'darwin')</c>.</summary>
public sealed class MacOsFactAttribute : FactAttribute
{
    public MacOsFactAttribute()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Skip = "needs macOS and its accessibility API";
        }
    }
}
