// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Mobile;

/// <summary>
/// The device a worker slot drives, kept across the attempts that run on it: the agent-device client, the session and device it
/// booted, and the recording still marked on the device. Upstream's <c>AgentDeviceSurface</c> holds these fields for the whole worker
/// (<c>client</c>, <c>bound</c>, <c>recording</c>). The port's attempt is a <see cref="DeviceSession"/>, so the engine holds this for
/// the slot. .NET-only holder, see <see cref="MobileEngine.WorkerOf"/>.
/// </summary>
internal sealed class DeviceWorker
{
    /// <summary>The binding the worker's device came from. Its lease, when it has one, is what a reusable session keeps (upstream's <c>device.leaseId</c>).</summary>
    public SlotBinding? Binding { get; init; }

    /// <summary>The agent-device client the worker's session runs on (upstream's <c>client</c>). Null once the worker is disposed.</summary>
    public IDeviceClient? Client { get; set; }

    /// <summary>The client wrapped in the in-flight tracking the attempts share, built on first use.</summary>
    public IDeviceClient? Tracked { get; set; }

    /// <summary>The agent-device session and device the boot opened under it (upstream's <c>bound</c>).</summary>
    public BoundDevice? Bound { get; set; }

    /// <summary>The recording still marked on the device, retained across attempts until a stop or native session reset is confirmed (upstream's <c>recording</c>).</summary>
    public DeviceSession.Recording? Recording { get; set; }
}
