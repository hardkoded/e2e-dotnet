// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using E2E.Cache;
using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>How a relocation failed. Upstream's <c>RelocationFailure</c>.</summary>
internal static class RelocationFailure
{
    public const string TargetNotFound = "target-not-found";

    public const string TargetAmbiguous = "target-ambiguous";
}

/// <summary>
/// Upstream's <c>RelocationResult</c>: the node a descriptor was re-found as, or why it was not. The id is the node's reference in the
/// observation (upstream's per-observation node id).
/// </summary>
internal abstract record RelocationResult
{
    /// <summary>Exactly one node matched, or the recorded position picked one of several.</summary>
    public sealed record Found(string Id) : RelocationResult;

    /// <summary>
    /// No node matched (<see cref="RelocationFailure.TargetNotFound"/>), or several share the matched identity
    /// (<see cref="RelocationFailure.TargetAmbiguous"/>, with their ids): a caller with other evidence (a recorded point) may still tell them apart.
    /// </summary>
    public sealed record Failed(string Failure, IReadOnlyList<string>? Candidates = null) : RelocationResult;
}

/// <summary>The fields of a descriptor a tier compares. Upstream's <c>DescriptorField</c>, which names them by key.</summary>
internal enum DescriptorField
{
    Role,
    Name,
    TestId,
    Placeholder,
    InputPurpose,
    Text,
}

/// <summary>The rung of the matching ladder a <see cref="DescriptorTier"/> is. Upstream types it as the strings <c>strict</c>, <c>semantic</c>, and <c>shape</c>.</summary>
internal enum DescriptorTierKind
{
    Strict,
    Semantic,
    Shape,
}

/// <summary>One rung of the matching ladder (<see cref="Relocate.DescriptorTiers"/>): the fields it compares, and how it reads a candidate (<see cref="Relocate.TierView"/>).</summary>
internal sealed record DescriptorTier(DescriptorTierKind Kind, RecordedTarget Descriptor);

/// <summary>
/// Upstream's <c>cache/relocate.ts</c>: deterministic target relocation. Re-finds a recorded target descriptor in a fresh observation:
/// exactly one node must match, or replay diverges. This is the conservative public ReplayPolicy (<see cref="CacheKeys.ReplayPolicyVersion"/>):
/// no scoring, no vision, no fuzzy matching beyond reading a minted test id by its shape (<see cref="DescriptorTiers"/>). The fail-closed
/// contract (one match or hand off) is not tunable.
/// Candidates are compared through the same <see cref="TraceRecorder.DescribeTarget"/> projection the recorder used, so redaction, whitespace
/// collapsing, and bounding cannot make a node unequal to its own recording. The matching vocabulary, which tiers a descriptor is tried in and
/// what "equal on the recorded fields" means, is exposed so end anchors are checked by the same rules and can never drift from relocation.
/// </summary>
internal static class Relocate
{
    /// <summary>Beyond this many twins a description is not a control set but a list; a position there would be noise. Upstream's <c>MAX_POSITIONED_TWINS</c>.</summary>
    public const int MaxPositionedTwins = 1000;

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Identity fields that must match whenever the recording captured them.</summary>
    private static readonly DescriptorField[] IdentityFields = [DescriptorField.Role, DescriptorField.Name, DescriptorField.TestId, DescriptorField.Placeholder, DescriptorField.InputPurpose];

    /// <summary>Text is identity too when neither a test id nor a name was recorded.</summary>
    private static readonly DescriptorField[] IdentityFieldsWithText = [.. IdentityFields, DescriptorField.Text];

    /// <summary>The uuid a test id part may spell.</summary>
    private static readonly Regex Uuid = new("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);

    private static readonly Regex TestIdSeparators = new("([-_:./]+)", RegexOptions.CultureInvariant, MatchTimeout);

    /// <summary>A test id part an app mints: a hex id or hash, a cuid, a recent epoch time in seconds or milliseconds.</summary>
    private static readonly Regex[] MintedTestIdPart =
    [
        new(@"\A(?=.*[0-9])(?=.*[a-f])[0-9a-f]{8,}\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout),
        new(@"\A(?=(?:[^0-9]*[0-9]){2})(?=.*[A-Za-z])[A-Za-z0-9]{8,}\z", RegexOptions.CultureInvariant, MatchTimeout),
        new(@"\A1[5-9][0-9]{8}(?:[0-9]{3})?\z", RegexOptions.CultureInvariant, MatchTimeout),
    ];

    /// <summary>A node's descriptor projection alongside its node.</summary>
    private sealed record DescribedNode(SemanticNode Node, RecordedTarget Descriptor);

    /// <summary>
    /// The projection of one observation under one redactor, shared by every lookup into it. Upstream's <c>projections</c> <c>WeakMap</c>.
    /// The port keeps raw nodes, so the projection also depends on the redactor it was made under: a lookup under another one projects again.
    /// </summary>
    private sealed class Projection(Redactor redactor, IReadOnlyList<DescribedNode> described, IReadOnlyList<SemanticNode> roots)
    {
        private IReadOnlyDictionary<SemanticNode, SemanticNode>? _parents;

        public Redactor Redactor { get; } = redactor;

        public IReadOnlyList<DescribedNode> Described { get; } = described;

        public IReadOnlyDictionary<SemanticNode, SemanticNode> Parents => _parents ??= AgentActions.ParentsOf(roots);
    }

    private static readonly ConditionalWeakTable<Observation, Projection> Projections = new();

    /// <summary>
    /// A descriptor with nothing to identify the control by: no test id, name, text, or placeholder, only a role. An icon button and a form
    /// built without labels are made of these. Such a descriptor relocates by its place among the unnamed controls of its kind, and it is matched
    /// strictly (<see cref="FieldsIdentical"/>): an unnamed textbox must never stand in for a named one.
    /// </summary>
    private static bool IsAnonymous(RecordedTarget descriptor) =>
        descriptor.TestId is null && descriptor.Name is null && descriptor.Text is null && descriptor.Placeholder is null;

    /// <summary>
    /// Whether a descriptor can identify a node at all. Role or selector alone cannot: a wrong match acts on the wrong control, so such a
    /// descriptor is not relocatable and, as an anchor, would prove nothing. An anonymous one is relocatable by its recorded place only when that
    /// place means something on the next screen: it was the one unnamed control of its role, or it sits in a named container (<c>within</c>) that
    /// tells it from its twins. A place counted among twins with no container is an order, and rows that reorder would hand the action to the
    /// wrong one, so such a target hands off.
    /// </summary>
    public static bool IsRelocatableDescriptor(RecordedTarget descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return !IsAnonymous(descriptor) || (descriptor.Role is not null && IsPlaced(descriptor));
    }

    /// <summary>Whether a recorded place tells the target apart: it was counted alone, or sits in a named container.</summary>
    private static bool IsPlaced(RecordedTarget descriptor) => descriptor.Position is { } position && (position.Of == 1 || descriptor.Within is not null);

    /// <summary>The semantic tier of a descriptor: every identity field but the test id.</summary>
    private static RecordedTarget WithoutTestId(RecordedTarget descriptor)
    {
        var semantic = descriptor.Copy();
        semantic.TestId = null;
        return semantic;
    }

    /// <summary>
    /// The tiers a recorded descriptor is matched in, strictest first. <b>Strict</b>: every identity field the recording captured must match.
    /// <b>Semantic</b>: only when a test id was recorded and the remaining fields still identify the node, the same descriptor without it. Test
    /// ids are the strongest discriminator when stable, but some apps mint them per render; a node the semantic fields still identify has not
    /// moved, its label has not changed, and refusing it would fail on cosmetics. <b>Shape</b>: only when the test id is all that identifies the
    /// node and holds a minted part: the test id with each such part read as <c>#</c> (<c>rf__node-#</c>, <c>row-#-delete</c>), compared with
    /// candidates read the same way. A shape names no node alone, so a target matches by it only where a place means something
    /// (<see cref="RelocateDescriptor"/>). Empty for a descriptor that identifies nothing.
    /// </summary>
    public static IReadOnlyList<DescriptorTier> DescriptorTiers(RecordedTarget descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!IsRelocatableDescriptor(descriptor))
        {
            return [];
        }

        var strict = new DescriptorTier(DescriptorTierKind.Strict, descriptor);
        if (descriptor.TestId is null)
        {
            return [strict];
        }

        var semantic = WithoutTestId(descriptor);
        // A test id that churned is forgiven only when the semantic fields still identify the node. Dropping it must not leave an anonymous
        // descriptor: a position counted among test-id twins says nothing about the unnamed controls of that role, so it would relocate to an
        // unrelated one.
        if (IsRelocatableDescriptor(semantic) && !IsAnonymous(semantic))
        {
            return [strict, new DescriptorTier(DescriptorTierKind.Semantic, semantic)];
        }

        if (TestIdShape(descriptor.TestId) is not { } shape)
        {
            return [strict];
        }

        var shaped = descriptor.Copy();
        shaped.TestId = shape;
        return [strict, new DescriptorTier(DescriptorTierKind.Shape, shaped)];
    }

    /// <summary>A candidate as a tier compares it: as described, or by its test id shape, null when it has none.</summary>
    public static RecordedTarget? TierView(DescriptorTier tier, RecordedTarget candidate)
    {
        ArgumentNullException.ThrowIfNull(tier);
        ArgumentNullException.ThrowIfNull(candidate);
        return tier.Kind != DescriptorTierKind.Shape ? candidate : DescriptorTiers(candidate).FirstOrDefault(rung => rung.Kind == DescriptorTierKind.Shape)?.Descriptor;
    }

    /// <summary>Whether a tier tells nodes apart only by their count and place: an unnamed control, or a test id shape.</summary>
    private static bool MatchesByPlace(DescriptorTier tier) => tier.Kind == DescriptorTierKind.Shape || IsAnonymous(tier.Descriptor);

    /// <summary>The test id with each uuid and minted part read as <c>#</c>, null when it has none.</summary>
    private static string? TestIdShape(string testId)
    {
        var parts = TestIdSeparators.Split(Uuid.Replace(testId, "#"));
        for (var index = 0; index < parts.Length; index += 2)
        {
            if (MintedTestIdPart.Any(pattern => pattern.IsMatch(parts[index])))
            {
                parts[index] = "#";
            }
        }

        var shape = string.Concat(parts);
        return string.Equals(shape, testId, StringComparison.Ordinal) ? null : shape;
    }

    private static string? Field(RecordedTarget descriptor, DescriptorField field) => field switch
    {
        DescriptorField.Role => descriptor.Role,
        DescriptorField.Name => descriptor.Name,
        DescriptorField.TestId => descriptor.TestId,
        DescriptorField.Placeholder => descriptor.Placeholder,
        DescriptorField.InputPurpose => descriptor.InputPurpose,
        DescriptorField.Text => descriptor.Text,
        _ => null,
    };

    /// <summary>Every listed field the recording captured must be present and equal on the candidate.</summary>
    public static bool FieldsEqual(RecordedTarget recorded, RecordedTarget candidate, IEnumerable<DescriptorField> fields)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(candidate);
        return fields.All(field => Field(recorded, field) is not { } value || string.Equals(Field(candidate, field), value, StringComparison.Ordinal));
    }

    /// <summary>Like <see cref="FieldsEqual"/>, but a field the recording lacks must be absent on the candidate too.</summary>
    private static bool FieldsIdentical(RecordedTarget recorded, RecordedTarget candidate, IEnumerable<DescriptorField> fields) =>
        fields.All(field => string.Equals(Field(candidate, field), Field(recorded, field), StringComparison.Ordinal));

    /// <summary>Whether the projection of a node names anything at all. Upstream's <c>describeTarget</c> returns nothing for such a node.</summary>
    internal static bool NamesAnything(RecordedTarget described) =>
        described.Role is not null || described.Name is not null || described.Text is not null || described.TestId is not null || described.Placeholder is not null || described.InputPurpose is not null;

    /// <summary>
    /// The node of an observation a relocation named by its reference. No upstream counterpart: upstream's result names a key of the map of
    /// nodes it was given (<c>nodes.get(id)</c>); the port names nodes by their <see cref="SemanticNode.Ref"/>.
    /// </summary>
    internal static SemanticNode? NodeOf(Observation observation, string id)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return LocatorResolver.Walk(observation.Roots).FirstOrDefault(node => string.Equals(node.Ref, id, StringComparison.Ordinal));
    }

    /// <summary>Projects every node of an observation the way the recorder described its targets, once per observation and redactor. Hidden nodes are not candidates.</summary>
    private static Projection Project(Observation observation, Redactor redactor)
    {
        if (Projections.TryGetValue(observation, out var cached) && ReferenceEquals(cached.Redactor, redactor))
        {
            return cached;
        }

        var described = new List<DescribedNode>();
        foreach (var node in LocatorResolver.Walk(observation.Roots))
        {
            if (node.States.Hidden || node.States.AriaHidden)
            {
                continue;
            }

            var descriptor = TraceRecorder.DescribeTarget(node, redactor);
            if (NamesAnything(descriptor))
            {
                described.Add(new DescribedNode(node, descriptor));
            }
        }

        var projection = new Projection(redactor, described, observation.Roots);
        Projections.AddOrUpdate(observation, projection);
        return projection;
    }

    /// <summary>
    /// Relocates one descriptor against the nodes of a fresh observation, tier by tier (<see cref="DescriptorTiers"/>), each exactly-one-or-diverge.
    /// Ambiguity at any tier diverges immediately: two candidates sharing the matched identity cannot be told apart by waiting, and acting on
    /// either would be a guess. The one exception is a recorded <c>position</c>: the recording itself found the same twins and noted which one it
    /// acted on, so the same count of twins resolves to the same one; any other count diverges as before.
    /// Upstream takes a map of redacted nodes; the port takes the live observation and the redactor its nodes are read through, and the result
    /// names nodes by their reference.
    /// </summary>
    public static RelocationResult RelocateDescriptor(RecordedTarget descriptor, Observation observation, Redactor redactor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var (matches, tier) = MatchingIds(descriptor, observation, redactor);
        if (tier is null)
        {
            return new RelocationResult.Failed(RelocationFailure.TargetNotFound);
        }

        var position = descriptor.Position;
        // A lone match is the node for a tier with an identity. One that matches by place has only the count: one twin where the recording
        // counted two is as likely the other one.
        if (matches.Count == 1 && (!MatchesByPlace(tier) || position?.Of == 1))
        {
            return new RelocationResult.Found(matches[0]);
        }

        return position is { } place && place.Of == matches.Count && place.Index >= 0 && place.Index < matches.Count
            ? new RelocationResult.Found(matches[place.Index])
            : new RelocationResult.Failed(RelocationFailure.TargetAmbiguous, matches);
    }

    /// <summary>
    /// The ids a descriptor matches in a fresh observation, in document order, and the tier that matched: the strictest tier
    /// (<see cref="DescriptorTiers"/>) that matches anything decides, so a churned test id still falls back to the semantic fields, and a minted
    /// one to its shape. No tier when nothing matches. The recorder uses the same projection to notice, before it writes a target, that the
    /// description alone would not tell the target from its twins.
    /// </summary>
    private static (IReadOnlyList<string> Ids, DescriptorTier? Tier) MatchingIds(RecordedTarget descriptor, Observation observation, Redactor redactor)
    {
        var keyed = CandidatesWithin(descriptor.Within, observation, redactor);
        foreach (var tier in DescriptorTiers(descriptor))
        {
            // A shape is tried only where a place means something, as for an unnamed control: alone when recorded (DescribePosition), or in a
            // named container.
            if (tier.Kind == DescriptorTierKind.Shape && !IsPlaced(descriptor))
            {
                continue;
            }

            var matches = TierMatches(tier, keyed);
            if (matches.Count > 0)
            {
                return (matches, tier);
            }
        }

        return ([], null);
    }

    /// <summary>The candidates in the recorded container.</summary>
    private static IReadOnlyList<DescribedNode> CandidatesWithin(string? within, Observation observation, Redactor redactor)
    {
        var projection = Project(observation, redactor);
        if (within is null)
        {
            return projection.Described;
        }

        // A recorded container key must hold: the same "Delete" in another row is a different control. Checked against the tree the candidates
        // came from, never guessed.
        return [.. projection.Described.Where(candidate => string.Equals(AgentActions.ContainerKey(candidate.Node, projection.Parents, redactor), within, StringComparison.Ordinal))];
    }

    private static List<string> TierMatches(DescriptorTier tier, IReadOnlyList<DescribedNode> candidates)
    {
        var recorded = tier.Descriptor;
        var identical = IsAnonymous(recorded);
        var fields = recorded.TestId is null && recorded.Name is null ? IdentityFieldsWithText : IdentityFields;
        return
        [
            .. candidates
                .Where(candidate => TierView(tier, candidate.Descriptor) is { } view && (identical ? FieldsIdentical(recorded, view, fields) : FieldsEqual(recorded, view, fields)))
                .Select(candidate => candidate.Node.Ref),
        ];
    }

    /// <summary>
    /// Where <paramref name="node"/> stands among the controls its own description matches on the screen it was acted on, or null when the
    /// description already names it alone. Recorded with the target so a replay can tell one "Set up" button per card apart without an app
    /// change; a distinct label makes it unnecessary. Upstream takes a map of redacted nodes; the port takes the live observation and the
    /// redactor its nodes are read through.
    /// </summary>
    public static TracePosition? DescribePosition(SemanticNode node, string? within, Observation observation, Redactor redactor)
    {
        ArgumentNullException.ThrowIfNull(node);
        var described = TraceRecorder.DescribeTarget(node, redactor);
        if (!NamesAnything(described) || (described.Role is null && IsAnonymous(described)))
        {
            return null;
        }

        // A target whose loosest tier matches by place, an unnamed control or a test id shape, has no identity to match alone; its position is
        // what makes it relocatable, so it is counted among its twins on that tier even when it is the only one, and described with a placeholder
        // position to do so.
        var probe = described.Copy();
        probe.Within = within;
        probe.Position = new TracePosition { Index = 0, Of = 1 };
        var tiers = DescriptorTiers(probe);
        if (tiers.Count == 0)
        {
            return null;
        }

        var loosest = tiers[^1];
        var byPlace = MatchesByPlace(loosest);
        var ids = byPlace ? TierMatches(loosest, CandidatesWithin(within, observation, redactor)) : [.. MatchingIds(probe, observation, redactor).Ids];
        if (ids.Count < (byPlace ? 1 : 2) || ids.Count > MaxPositionedTwins)
        {
            return null;
        }

        var index = ids.IndexOf(node.Ref);
        return index == -1 ? null : new TracePosition { Index = index, Of = ids.Count };
    }
}
