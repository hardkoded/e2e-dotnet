// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Desktop;

namespace E2E.Desktop.Tests;

/// <summary>
/// What the <c>nodes</c> and <c>surface</c> tests build their snapshots with. .NET-only: upstream writes the driver's JSON inline and reads the
/// fixtures with <c>JSON.parse</c>; C# has no object literal, so a node is one call.
/// </summary>
internal static class Raw
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>A snapshot fixture, as <c>load</c> reads it in upstream's <c>nodes.test.ts</c>.</summary>
    public static RawSnapshot Load(string name) =>
        JsonSerializer.Deserialize<RawSnapshot>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Snapshots", name + ".json")),
            Json
        )!;

    /// <summary>One node of a driver snapshot.</summary>
    public static RawNode Node(
        string role,
        string? name = null,
        string? refId = null,
        string? value = null,
        string? description = null,
        string? hint = null,
        string? placeholder = null,
        int? level = null,
        string? nativeId = null,
        string nativeIdKind = "ax_identifier",
        string[]? states = null,
        string[]? actions = null,
        string[]? scrollAxes = null,
        Rect? bounds = null,
        int? childrenCount = null,
        bool? subtreeTruncated = null,
        params RawNode[] children
    ) =>
        new()
        {
            Role = role,
            Name = name,
            RefId = refId,
            Value = value,
            Description = description,
            Hint = hint,
            Placeholder = placeholder,
            Level = level,
            NativeId = nativeId is null ? null : new NativeId(nativeIdKind, nativeId),
            States = states,
            AvailableActions = actions,
            ScrollAxes = scrollAxes,
            Bounds = bounds,
            ChildrenCount = childrenCount,
            SubtreeTruncated = subtreeTruncated,
            Children = children.Length == 0 ? null : children,
        };

    /// <summary>A snapshot of <paramref name="tree"/>.</summary>
    public static RawSnapshot Snapshot(
        RawNode tree,
        string id = "s",
        SnapshotWindow? window = null,
        bool? complete = null
    ) =>
        new()
        {
            SnapshotId = id,
            Tree = tree,
            Window = window,
            Complete = complete,
        };

    /// <summary>Ids as the surface hands them out: one per identity, kept across snapshots. Upstream's <c>minter</c>.</summary>
    public static Func<string?, string> Minter()
    {
        var ids = new Dictionary<string, string>();
        var fresh = 0;
        return identity =>
        {
            if (identity is null)
            {
                return $"fresh{++fresh}";
            }

            if (!ids.TryGetValue(identity, out var id))
            {
                ids[identity] = id = $"n{ids.Count + 1}";
            }

            return id;
        };
    }
}
