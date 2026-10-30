// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Mobile;

/// <summary>The agent-device this package pins exactly: the version every client speaks.</summary>
public static class AgentDeviceVersion
{
    /// <summary>The agent-device release this client runs against. The wire format was read from 0.21.22 and rechecked in part on 0.21.25, 0.21.29 and 0.21.33 (see COMPATIBILITY.md).</summary>
    public const string Pinned = "0.21.33";

    /// <summary>The daemon's JSON-RPC protocol version the client speaks, as <c>GET /health</c> reports it.</summary>
    public const int RpcProtocol = 2;

    /// <summary>The oldest Node the agent-device daemon runs on.</summary>
    public static readonly Version MinimumNode = new(22, 12);
}
