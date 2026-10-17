// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// The debug buckets every phase of an agent step feeds, so the timings of the observe, model, and action phases cannot
/// drift apart between the paths that run them. Upstream <c>agent/phases.ts</c>, where <c>instrumentPhase</c> also records the
/// step's events; the port has no step recorder yet.
/// </summary>
internal static class Phases
{
    public const string Observe = "agent.observe";

    public const string Model = "agent.model";

    public const string Action = "agent.action";
}
