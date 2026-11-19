// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.Elements;

/// <summary>The helpers <c>elements.test.ts</c> declares at its top.</summary>
internal static class ElementsHelpers
{
    public static ExecutorNode Node(
        string id,
        string? role = null,
        string? name = null,
        string? text = null,
        string? value = null,
        NodeStates? states = null,
        IReadOnlyDictionary<string, string>? attributes = null,
        BoundingBox? rect = null,
        string? inputPurpose = null,
        IReadOnlyList<ExecutorNode>? children = null
    ) =>
        new()
        {
            Id = id,
            Role = role,
            Name = name,
            Text = text,
            Value = value,
            States = states ?? new NodeStates(),
            Attributes = attributes,
            Rect = rect,
            InputPurpose = inputPurpose,
            Children = children ?? [],
        };

    public static HashSet<ExecutorVerb> VerbSet(params ExecutorVerb[] verbs) => [.. verbs];

    public static ActionSpace SpaceFor(
        IReadOnlyList<ExecutorNode> children,
        IReadOnlySet<ExecutorVerb>? verbs = null,
        bool? keyboardVisible = null
    )
    {
        var fixture = Context(new ContextOptions { Verbs = verbs });
        return global::E2E.Decision.Elements.ActionSpace(
            fixture.Ctx,
            new SpaceObservation
            {
                Path = "/form",
                Viewport = new TraceViewport(800, 600),
                Tree = new ExecutorNode { Id = "root", Children = children },
                KeyboardVisible = keyboardVisible,
            },
            true
        );
    }

    public static IReadOnlyList<string> Keys(ActionSpace space, Operation operation) =>
        space.Targets.TryGetValue(operation, out var group) ? [.. group.Keys] : [];

    public static IReadOnlyList<string> Names(IEnumerable<Operation> operations) =>
        [.. operations.Select(WireNames.Of).Order(StringComparer.Ordinal)];
}
