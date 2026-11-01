// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Mobile.DevFlow;

/// <summary>
/// What a device client needs to start, stop, and install the app around the agent: the launcher of its device, the
/// discovery that finds the agent of a launched app, and the waits the options allow. It also keeps the Android port
/// forward of the session: one at a time, removed when the agent moves and when the session closes. Without one the
/// client only attaches to an app that is running.
/// </summary>
internal sealed class AppControl(Func<MobilePlatform, CancellationToken, Task<IAppLauncher>> launcher, AgentDiscovery discovery, TimeSpan connectTimeout, TimeSpan probe)
{
    private IAppLauncher? _launcher;
    private int? _forwarded;

    public AppControl(IAppLauncher launcher, AgentDiscovery discovery, TimeSpan connectTimeout, TimeSpan probe)
        : this((_, _) => Task.FromResult(launcher), discovery, connectTimeout, probe)
    {
        _launcher = launcher;
    }

    /// <summary>The launcher of the session's device, found on first use when the binding did not name one.</summary>
    public async Task<IAppLauncher> LauncherAsync(MobilePlatform platform, CancellationToken cancellationToken) =>
        _launcher ??= await launcher(platform, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Whether the app's agent answers now, within a short probe that finds the agent again when it moved: null when
    /// nothing listens, which is an app that is not running. A port that accepts but never answers is not running either.
    /// </summary>
    public async Task<JsonNode?> ProbeAsync(DevFlowAgentClient agent, IAppLauncher device, CancellationToken cancellationToken)
    {
        try
        {
            return await discovery.WaitForStatusAsync(agent, probe, cancellationToken, Forward(device), rediscover: true).ConfigureAwait(false);
        }
        catch (DevFlowAgentException failure) when (failure.Status is null && failure.Code is "unreachable" or "timeout")
        {
            return null;
        }
    }

    /// <summary>
    /// Waits for the agent of an app that was just launched: the client's endpoint is resolved again, since the app
    /// registers with the broker as it starts, the port is forwarded on Android, and the status is polled until it answers.
    /// </summary>
    public async Task<JsonNode?> WaitForAgentAsync(DevFlowAgentClient agent, IAppLauncher device, CancellationToken cancellationToken)
    {
        agent.Reset();
        return await discovery.WaitForStatusAsync(agent, connectTimeout, cancellationToken, Forward(device), rediscover: true).ConfigureAwait(false);
    }

    /// <summary>Removes the session's forward, when it holds one. Best effort: the next session forwards again at its boot.</summary>
    public async Task ReleaseForwardAsync(CancellationToken cancellationToken)
    {
        if (_forwarded is { } port && _launcher is { } device)
        {
            _forwarded = null;
            try
            {
                await device.RemoveForwardAsync(port, cancellationToken).ConfigureAwait(false);
            }
            catch (AgentDeviceError)
            {
                // A device that is gone has no forward to remove.
            }
        }
    }

    /// <summary>Forwards each endpoint the wait resolves, and removes the one before it, so one forward is held at a time.</summary>
    private Func<Uri, CancellationToken, Task> Forward(IAppLauncher device) => async (endpoint, cancellationToken) =>
    {
        if (_forwarded == endpoint.Port)
        {
            return;
        }

        await ReleaseForwardAsync(cancellationToken).ConfigureAwait(false);
        await device.ForwardAsync(endpoint.Port, cancellationToken).ConfigureAwait(false);
        _forwarded = endpoint.Port;
    };
}
