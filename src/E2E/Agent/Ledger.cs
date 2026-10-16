// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// Prior-step context for agent prompts. Upstream's <c>serializeLedger</c> reads step records; the port keeps only the
/// summary of each completed step, so it lists those, oldest first.
/// </summary>
internal static class Ledger
{
    internal static string SerializeLedger(IEnumerable<string> steps) => string.Join('\n', steps.Select(step => "- " + step));
}
