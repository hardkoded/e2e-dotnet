// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Desktop.Tests.MacOsDriver;

/// <summary>A test that calls the macOS frameworks: skipped on any other operating system. .NET-only test support.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class MacOnlyFactAttribute : FactAttribute
{
    public MacOnlyFactAttribute()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Skip = "calls the macOS frameworks";
        }
    }
}
