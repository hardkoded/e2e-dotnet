// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// Why a vision call fell back to tree-only input. Pixel evidence degrades rather than fails, except under
/// <see cref="VisionMode.Only"/>, which fails with <c>POLICY_DENIED</c> naming the reason.
/// </summary>
public enum VisionDegradation
{
    /// <summary>A secret was filled in this attempt, so the app may show it outside a secure field and no pixels leave the session.</summary>
    PixelTainted,

    /// <summary>The engine masked fewer regions than the tree has secure nodes, so the image cannot be proven redacted.</summary>
    MaskingUnproven,

    /// <summary>The engine captures no pixels.</summary>
    UnsupportedCapability,
}
