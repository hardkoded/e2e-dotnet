// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision;

/// <summary>
/// The pixels a decision request carries beside its state. Upstream's <c>Screenshot</c> (<c>overlay.ts</c>), the type of <c>DecisionRequest.screenshot</c>. The rest of that file comes with the decision executor (#1770).
/// </summary>
public sealed record Screenshot(string MediaType, byte[] Data);
