// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// How far a secret reached one session. It only rises and is never cleared. The port has the one level upstream calls
/// <c>filled</c>, a secret typed into the app: the app may show it outside a secure field, so no pixels may leave the
/// session. Upstream's <c>engine</c> level (a secret an engine resolves for an option it hands the app) has no port counterpart.
/// </summary>
internal sealed class Exposure
{
    private volatile bool _filled;

    /// <summary>Records that a secret was typed into the app.</summary>
    public void RaiseFilled() => _filled = true;

    /// <summary>Whether no pixels may leave the session: no screenshot is taken, kept, or handed to a model.</summary>
    public bool WithholdsPixels => _filled;
}
