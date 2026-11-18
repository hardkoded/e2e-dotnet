// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Mobile;

/// <summary>
/// What a session on a <see cref="MobileEngine"/> holds beside <c>Screen</c> and <c>App</c>: the <see cref="Device"/> fixture. Read it from
/// <see cref="Session{TFixtures}.Fixtures"/> on a host launched through <see cref="Launch.LaunchAsync{TFixtures}"/>. Upstream's <c>MobileFixtures</c>
/// (<c>mobile/fixtures.ts</c>).
/// </summary>
public sealed class MobileFixtures
{
    internal MobileFixtures(Device device) => Device = device;

    /// <summary>Deterministic device management for the session.</summary>
    public Device Device { get; }
}
