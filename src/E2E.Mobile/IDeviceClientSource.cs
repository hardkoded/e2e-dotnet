// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Mobile;

/// <summary>
/// A provider whose devices are not driven through an agent-device daemon: it makes the <see cref="IDeviceClient"/>
/// for each session itself. <see cref="MobileEngine"/> asks an <see cref="MobileOptions.Provider"/> that implements
/// this for its clients, and builds the agent-device client for any other.
/// <para>
/// There is no upstream counterpart: upstream's provider always names an agent-device daemon.
/// </para>
/// </summary>
/// <remarks>
/// A worker may run in another process than the one that leased the device. It builds its engine from the same
/// options, so its provider object makes the client there. What the client needs to find the leased device travels
/// in <see cref="DeviceConnection.Client"/>, which is the <see cref="DeviceLease.Client"/> the provider returned.
/// </remarks>
public interface IDeviceClientSource
{
    /// <summary>The client for one session. <paramref name="connection"/> is the slot's binding, or null for a device the pool did not bind.</summary>
    IDeviceClient CreateClient(string session, DeviceConnection? connection);
}
