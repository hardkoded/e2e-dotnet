// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Internal;

namespace E2E;

/// <summary>
/// A test's options once resolved, as selection reads them (upstream's <c>ResolvedTestOptions</c>, <c>collect/select.ts</c>). Not ported:
/// <c>timeout</c>, <c>retries</c>, <c>agentContext</c>, <c>trace</c>, and <c>video</c>, which only a runner reads.
/// </summary>
/// <param name="Platforms">The platforms the test declares; null runs it on any.</param>
/// <param name="Requires">The capabilities the test needs.</param>
/// <param name="Session">The session the test consumes.</param>
/// <param name="Agents">The configured agents the test runs as, one pair each, in order. Never empty; a setup test has exactly one.</param>
/// <param name="SkipReason">Why the test is skipped; null when it is not.</param>
/// <param name="Serial">Whether the test is a member of a serial group.</param>
internal sealed record ResolvedTestOptions(
    IReadOnlyList<string>? Platforms,
    IReadOnlyList<string> Requires,
    string? Session,
    IReadOnlyList<string> Agents,
    string? SkipReason,
    bool Serial);

/// <summary>What a pair does (upstream's <c>disposition</c>): run it, report it skipped, or report it as unselected.</summary>
internal enum Disposition
{
    Run,
    Skip,
    Filtered,
}

/// <summary>
/// Why a pair did not run (upstream's <c>SkipInfo</c>). <see cref="Cause"/> is <c>filtered</c>, <c>explicit</c>, <c>platform-unavailable</c>,
/// <c>capability-unavailable</c>, <c>setup-failed</c>, <c>serial-predecessor-failed</c>, <c>hook-failed</c>, <c>infrastructure-unavailable</c>,
/// <c>failure-limit</c>, or <c>time-limit</c>; only a <c>filtered</c> one can name the test filter that left it out.
/// </summary>
internal sealed record SkipInfo(string Cause, string Reason, string? RelatedId = null, string? Filter = null);

/// <summary>One test on one target as one agent (upstream's <c>TestTargetPair</c>).</summary>
internal sealed record TestTargetPair
{
    public required CollectedTest Test { get; init; }

    public required ResolvedTarget Target { get; init; }

    /// <summary>The configured agent this pair runs as: one of <c>Options.Agents</c>.</summary>
    public required string Agent { get; init; }

    /// <summary>Which run of the test this is under <c>--repeat-each</c>: 0 for the one every run has.</summary>
    public int Repeat { get; init; }

    public required ResolvedTestOptions Options { get; init; }

    public required Disposition Disposition { get; init; }

    public SkipInfo? Skip { get; init; }
}

/// <summary>All pairs for one selected target, in report order (upstream's <c>TargetSelection</c>).</summary>
internal sealed record TargetSelection(ResolvedTarget Target, IReadOnlyList<TestTargetPair> Pairs);

/// <summary>What selection chose (upstream's <c>Selection</c>).</summary>
/// <param name="Pairs">Every pair across all selected targets, in report order.</param>
/// <param name="PerTarget">Selected targets with their pairs, in config order.</param>
internal sealed record Selection(IReadOnlyList<TestTargetPair> Pairs, IReadOnlyList<TargetSelection> PerTarget);

/// <summary>How several <c>--tag</c> values combine: a test carries any of them, or every one (upstream's <c>TagMode</c>).</summary>
internal enum TagMode
{
    Any,
    All,
}

/// <summary>One shard of a run split <paramref name="Total"/> ways, <paramref name="Index"/> counted from 1 (<c>--shard 2/3</c>; upstream's <c>Shard</c>).</summary>
internal sealed record Shard(int Index, int Total);

/// <summary>The flags a selection is made with (upstream's <c>SelectionFilters</c>).</summary>
internal sealed record SelectionFilters
{
    public IReadOnlyList<string>? Tags { get; init; }

    public TagMode? TagMode { get; init; }

    /// <summary>Tags that leave a test out whatever else selects it (<c>--exclude-tag</c>).</summary>
    public IReadOnlyList<string>? ExcludeTags { get; init; }

    /// <summary>Patterns a test's title must match, any of them (<c>--grep</c>); see <c>Selector.GrepText</c>.</summary>
    public IReadOnlyList<Regex>? Grep { get; init; }

    /// <summary>Patterns that leave a test out when its title matches any of them (<c>--grep-invert</c>).</summary>
    public IReadOnlyList<Regex>? GrepInvert { get; init; }

    /// <summary>
    /// The result ids (<c>Ids.ResultId(testId, target, agent)</c>) of the tests the previous run did not pass (<c>--last-failed</c>): only
    /// those run, and the consumers of a setup among them. Empty means nothing failed, so nothing is selected.
    /// </summary>
    public IReadOnlySet<string>? LastFailed { get; init; }

    /// <summary>The one shard of the selection to run, cut once every flag applied and before the test filters (<c>--shard</c>).</summary>
    public Shard? Shard { get; init; }

    /// <summary>Tests the filters left out, by test id. Only the pairs the shard would run are left out, and a serial group with a kept member stays whole.</summary>
    public IReadOnlyDictionary<string, TestExclusion>? Excluded { get; init; }

    public IReadOnlyList<string>? TargetIds { get; init; }
}

/// <summary>Test-target selection (upstream's <c>collect/select.ts</c>). .NET-only name: a C# member cannot share its type's name, and <see cref="Select"/> is upstream's <c>select</c>.</summary>
internal static class Selector
{
    /// <summary>
    /// The reasons a <c>filtered</c> pair carries. <c>ClassifyPair</c> writes them and <c>DescribeNoTests</c> reads them back; the strings
    /// also reach the report on unselected results, which is why they are prose rather than codes.
    /// </summary>
    private static class FilteredReason
    {
        public const string File = "file not selected by a positional argument";
        public const string Line = "not declared at a line a positional named";
        public const string Focus = "not focused by .only";
        public const string Tags = "tag filter did not match";
        public const string ExcludedTag = "carries an excluded tag";
        public const string Grep = "title does not match --grep";
        public const string GrepInvert = "title matches --grep-invert";
        public const string LastFailed = "did not fail in the last run";
        public const string Shard = "outside the shard";
    }

    /// <summary>At most this many file names are spelled out in a <c>NO_TESTS</c> message.</summary>
    private const int MaxNamedFiles = 3;

    /// <summary>Validates target IDs and selects targets once each, in config order (upstream's <c>selectTargets</c>).</summary>
    public static IReadOnlyList<ResolvedTarget> SelectTargets(IReadOnlyList<ResolvedTarget> targets, IReadOnlyList<string>? targetIds)
    {
        if (targetIds is null || targetIds.Count == 0)
        {
            return targets;
        }

        var names = targets.Select(target => target.Name).ToList();
        var known = new HashSet<string>(names, StringComparer.Ordinal);
        foreach (var id in targetIds)
        {
            if (!known.Contains(id))
            {
                throw new ConfigurationException(
                    "UNKNOWN_TARGET",
                    $"unknown target ID \"{id}\"; the config declares {string.Join(", ", names.Select(name => $"\"{name}\""))}{Suggestions.DidYouMean(id, names)}");
            }
        }

        var selected = new HashSet<string>(targetIds, StringComparer.Ordinal);
        return [.. targets.Where(target => selected.Contains(target.Name))];
    }

    /// <summary>
    /// Expands the collection into test-target pairs and applies focus, tag, platform, capability, session, and serial-closure rules
    /// (upstream's <c>select</c>). <paramref name="noTestsHint"/> replaces the advice a <c>NO_TESTS</c> message ends with. Differences:
    /// the glob and positional diagnostics of <c>NO_TESTS</c> have no port (the collection holds no globs or positionals), and the
    /// message for a collection with no test says so in one clause.
    /// </summary>
    public static Selection Select(
        Collection collection,
        ResolvedConfig config,
        SelectionFilters? filters = null,
        bool passWithNoTests = false,
        string? noTestsHint = null)
    {
        filters ??= new SelectionFilters();
        var tagMode = filters.TagMode ?? TagMode.Any;
        var targets = SelectTargets(config.Targets, filters.TargetIds);

        // Focus is decided among the tests positionals selected: a `.only` left in a file the run did not name neither runs nor silences the named files.
        var focused = collection.Tests.Where(test => test.Mode == TestMode.Only && test.Selected).ToList();
        if (focused.Count > 0 && config.Ci)
        {
            throw new ConfigurationException("ONLY_IN_CI", $".only is rejected in CI: {string.Join(", ", focused.Select(test => test.Id))}");
        }

        var optionsByTest = collection.Tests.ToDictionary(test => test.Id, test => test.Options, StringComparer.Ordinal);
        var selectedFiles = new HashSet<string>(collection.Files.Where(file => file.Selected).Select(file => file.File), StringComparer.Ordinal);

        AssertSerialAgentsAgree(collection.Tests, optionsByTest);
        var sessionProducers = CollectSessionProducers(collection.Tests);
        var pairFilters = filters.LastFailed is null
            ? filters
            : filters with { LastFailed = WithSetupConsumers(filters.LastFailed, collection.Tests, targets, optionsByTest, sessionProducers) };

        // One pair per target, test, and agent the test runs as, in that order, so a persona sweep reports its variants side by side.
        var pairs = new List<TestTargetPair>();
        foreach (var target in targets)
        {
            foreach (var test in collection.Tests)
            {
                var options = optionsByTest[test.Id];
                foreach (var agent in options.Agents)
                {
                    pairs.Add(ClassifyPair(test, target, agent, options, focused, selectedFiles, pairFilters, tagMode));
                }
            }
        }

        var withClosure = ApplySerialClosure(pairs);
        var withShard = filters.Shard is null ? withClosure : ApplyShard(withClosure, filters.Shard);

        // The test filters judged the pairs this shard runs, serial groups already whole, so what they left out is taken from those, and a
        // group with a kept member comes back whole. The shard is cut before they decide, so shard machines agree on the cut whatever a filter answers.
        var withExclusions = filters.Excluded is null ? withShard : ApplySerialClosure(ApplyExclusions(withShard, filters.Excluded));
        var withSessions = ApplySessionSelection(withExclusions, sessionProducers, collection.Uncollected);

        if (!passWithNoTests && !withSessions.Any(pair => pair.Disposition == Disposition.Run && pair.Test.Kind == TestKind.Test))
        {
            throw new ConfigurationException(
                "NO_TESTS",
                $"{DescribeNoTests(collection, filters, withSessions)}; {noTestsHint ?? "pass --pass-with-no-tests to allow this"}");
        }

        return new Selection(
            withSessions,
            [.. targets.Select(target => new TargetSelection(target, [.. withSessions.Where(pair => pair.Target.Name == target.Name)]))]);
    }

    /// <summary>
    /// A serial group runs as one unit per agent, so every member must run as the same agents; a member pinned elsewhere would leave its
    /// variant of the flow with a hole (upstream's <c>assertSerialAgentsAgree</c>).
    /// </summary>
    private static void AssertSerialAgentsAgree(IReadOnlyList<CollectedTest> tests, IReadOnlyDictionary<string, ResolvedTestOptions> optionsByTest)
    {
        var agentsByGroup = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var test in tests)
        {
            if (test.SerialId is null)
            {
                continue;
            }

            var agents = optionsByTest[test.Id].Agents;
            if (!agentsByGroup.TryGetValue(test.SerialId, out var groupAgents))
            {
                agentsByGroup[test.SerialId] = agents;
                continue;
            }

            if (groupAgents.SequenceEqual(agents))
            {
                continue;
            }

            throw new ConfigurationException(
                "COLLECTION_ERROR",
                $"serial group \"{test.SerialRoot?.Title ?? test.SerialId}\" in {test.File} runs as agents [{string.Join(", ", groupAgents)}] but its member \"{test.Title}\" pins [{string.Join(", ", agents)}]; pin the agent on the serial describe and name another per call with {{ agent }}");
        }
    }

    /// <summary>Whether a test's declared tags satisfy the filter: any of the filter's tags, or every one under <c>All</c>.</summary>
    private static bool MatchesTags(IReadOnlyList<string> declared, IReadOnlyList<string>? tags, TagMode tagMode)
    {
        if (tags is null || tags.Count == 0)
        {
            return true;
        }

        return tagMode == TagMode.All ? tags.All(declared.Contains) : tags.Any(declared.Contains);
    }

    /// <summary>The text <c>--grep</c> and <c>--grep-invert</c> match: the describe titles and the test title joined by one space. The file is not part of it, nor are tags.</summary>
    private static string GrepText(CollectedTest test) => string.Join(' ', test.TitlePath);

    /// <summary>Whether any of the patterns matches the text; none given matches nothing, so callers guard for the filter being absent.</summary>
    private static bool MatchesAny(IReadOnlyList<Regex> patterns, string text) => patterns.Any(pattern => pattern.IsMatch(text));

    /// <summary>The pattern-shaped filters, present only when they hold at least one pattern.</summary>
    private static (IReadOnlyList<Regex>? Grep, IReadOnlyList<Regex>? GrepInvert) PatternFilters(SelectionFilters filters)
    {
        static IReadOnlyList<Regex>? Present(IReadOnlyList<Regex>? patterns) => patterns is { Count: > 0 } ? patterns : null;
        return (Present(filters.Grep), Present(filters.GrepInvert));
    }

    private static string NameFiles(IReadOnlyList<string> files)
    {
        var shown = string.Join(", ", files.Take(MaxNamedFiles));
        var rest = files.Count - MaxNamedFiles;
        return rest > 0 ? $"{shown} and {rest} more" : shown;
    }

    /// <summary>
    /// Why nothing is runnable: the collection has no test, or every collected test was filtered or skipped (upstream's
    /// <c>describeNoTests</c>, whose glob and positional branches have no port).
    /// </summary>
    private static string DescribeNoTests(Collection collection, SelectionFilters filters, IReadOnlyList<TestTargetPair> pairs)
    {
        if (collection.Tests.Count == 0)
        {
            return "no tests were collected";
        }

        var reasons = new List<(string Reason, HashSet<string> Ids)>();
        void Count(string reason, CollectedTest test)
        {
            var entry = reasons.Find(item => item.Reason == reason);
            if (entry.Ids is null)
            {
                entry = (reason, new HashSet<string>(StringComparer.Ordinal));
                reasons.Add(entry);
            }

            entry.Ids.Add(test.Id);
        }

        // Told once for the run, against every tag the suite declares, and applied only to the pairs the tag filter itself left out:
        // a test a positional or a `.only` excluded is not a tag mismatch.
        var declaredTags = pairs.SelectMany(pair => pair.Test.Tags).Distinct(StringComparer.Ordinal).ToList();
        var tagFilter = filters.Tags is not { Count: > 0 } ? null : DescribeTagFilter(filters.Tags, filters.TagMode ?? TagMode.Any, declaredTags);
        var (grep, grepInvert) = PatternFilters(filters);
        var filterNames = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [FilteredReason.Line] = DescribeLines(collection),
            [FilteredReason.Tags] = tagFilter,
            [FilteredReason.ExcludedTag] = filters.ExcludeTags is null ? null : $"carry one of the excluded tags {NameTags(filters.ExcludeTags, declaredTags)}",
            [FilteredReason.Grep] = grep is null ? null : $"have titles matching none of {string.Join(", ", grep)}",
            [FilteredReason.GrepInvert] = grepInvert is null ? null : $"have titles matching {string.Join(", ", grepInvert)}",
            [FilteredReason.LastFailed] = filters.LastFailed is null ? null : FilteredReason.LastFailed + (filters.LastFailed.Count == 0 ? ", which had no failures" : ""),
            [FilteredReason.Shard] = filters.Shard is null ? null : DescribeShard(filters.Shard, pairs),
        };
        foreach (var pair in pairs)
        {
            if (pair.Disposition == Disposition.Run && pair.Test.Kind == TestKind.Test)
            {
                continue;
            }

            if (pair.Test.Kind == TestKind.Setup)
            {
                Count("setup tests, which run only for the sessions selected tests need", pair.Test);
                continue;
            }

            switch (pair.Skip?.Cause)
            {
                case "explicit":
                    Count("skipped with test.skip", pair.Test);
                    break;
                case "platform-unavailable":
                    Count($"declare platforms other than {pair.Target.Platform}", pair.Test);
                    break;
                case "capability-unavailable":
                    Count("require capabilities the engine lacks", pair.Test);
                    break;
                case "filtered":
                    Count(filterNames.GetValueOrDefault(pair.Skip.Reason) ?? pair.Skip.Reason, pair.Test);
                    break;
                default:
                    Count("unselected", pair.Test);
                    break;
            }
        }

        var summary = string.Join(", ", reasons.Select(item => $"{item.Ids.Count} {item.Reason}"));
        return $"{collection.Tests.Count} tests were collected but none is runnable: {summary}";
    }

    /// <summary>The shard that left nothing runnable, with what it was cut from: <c>fall outside shard 4/4 (3 tests split across 4 shards)</c>.</summary>
    private static string DescribeShard(Shard shard, IReadOnlyList<TestTargetPair> pairs)
    {
        // Told after the cut, so the items are the running pairs and the ones the shard itself left out.
        var items = ShardItems(pairs, pair => pair.Disposition == Disposition.Run || pair.Skip?.Reason == FilteredReason.Shard).Count;
        return $"fall outside shard {shard.Index}/{shard.Total} ({items} {(items == 1 ? "test" : "tests")} split across {shard.Total} shards)";
    }

    /// <summary>The <c>file:line</c> positionals that named no test, each with the lines its file declares tests at; null when every named line found its test.</summary>
    private static string? DescribeLines(Collection collection)
    {
        var missed = new List<string>();
        foreach (var file in collection.Files)
        {
            if (file.Lines is null)
            {
                continue;
            }

            var declared = file.DeclaredLines ?? [];
            var misses = file.Lines.Where(line => !declared.Contains(line)).ToList();
            if (misses.Count == 0)
            {
                continue;
            }

            var where = declared.Count == 0
                ? "the file declares no test itself"
                : $"declared at {(declared.Count == 1 ? "line" : "lines")} {string.Join(", ", declared)}";
            missed.Add($"{string.Join(", ", misses.Select(line => $"{file.File}:{line}"))} {(misses.Count == 1 ? "names" : "name")} no test ({where})");
        }

        return missed.Count == 0 ? null : $"{FilteredReason.Line}: {string.Join("; ", missed)}";
    }

    /// <summary>The tag filter that left nothing runnable, told against the tags the suite declares.</summary>
    private static string DescribeTagFilter(IReadOnlyList<string> tags, TagMode tagMode, IReadOnlyList<string> declared)
    {
        var named = NameTags(tags, declared);
        return tagMode == TagMode.All ? $"do not carry all of the tags {named}" : $"carry none of the tags {named}";
    }

    /// <summary>Filter tags as a list, each one no test declares marked, with the nearest declared tag when one is close.</summary>
    private static string NameTags(IReadOnlyList<string> tags, IReadOnlyList<string> declared) =>
        string.Join(", ", tags.Select(tag =>
        {
            if (declared.Contains(tag))
            {
                return tag;
            }

            var note = Suggestions.SuggestionNote(tag, declared);
            return tag + (note.Length == 0 ? " (no test declares it)" : note);
        }));

    /// <summary>Maps each session name to its unique producer setup test (upstream's <c>collectSessionProducers</c>).</summary>
    private static Dictionary<string, CollectedTest> CollectSessionProducers(IReadOnlyList<CollectedTest> tests)
    {
        var producers = new Dictionary<string, CollectedTest>(StringComparer.Ordinal);
        foreach (var test in tests.Where(test => test.Kind == TestKind.Setup))
        {
            foreach (var session in test.Sessions)
            {
                if (producers.TryGetValue(session, out var existing))
                {
                    throw new ConfigurationException("COLLECTION_ERROR", $"session \"{session}\" has duplicate producers: {existing.Id} and {test.Id}");
                }

                producers[session] = test;
            }
        }

        return producers;
    }

    /// <summary>
    /// <c>--last-failed</c> widened to the consumers of every setup it names. A setup runs only for a selected test that needs its session,
    /// so a setup whose <c>afterAll</c> failed while its consumers passed comes back through them (upstream's <c>withSetupConsumers</c>).
    /// </summary>
    private static HashSet<string> WithSetupConsumers(
        IReadOnlySet<string> lastFailed,
        IReadOnlyList<CollectedTest> tests,
        IReadOnlyList<ResolvedTarget> targets,
        IReadOnlyDictionary<string, ResolvedTestOptions> optionsByTest,
        IReadOnlyDictionary<string, CollectedTest> sessionProducers)
    {
        var widened = new HashSet<string>(lastFailed, StringComparer.Ordinal);
        foreach (var target in targets)
        {
            foreach (var test in tests)
            {
                var options = optionsByTest[test.Id];
                if (options.Session is null || !sessionProducers.TryGetValue(options.Session, out var producer))
                {
                    continue;
                }

                if (optionsByTest[producer.Id].Agents.Any(agent => lastFailed.Contains(Ids.ResultId(producer.Id, target.Name, agent))))
                {
                    foreach (var agent in options.Agents)
                    {
                        widened.Add(Ids.ResultId(test.Id, target.Name, agent));
                    }
                }
            }
        }

        return widened;
    }

    private static TestTargetPair ClassifyPair(
        CollectedTest test,
        ResolvedTarget target,
        string agent,
        ResolvedTestOptions options,
        IReadOnlyList<CollectedTest> focused,
        IReadOnlySet<string> selectedFiles,
        SelectionFilters filters,
        TagMode tagMode)
    {
        var pair = new TestTargetPair { Test = test, Target = target, Agent = agent, Options = options, Disposition = Disposition.Run };
        TestTargetPair Filtered(string reason) => pair with { Disposition = Disposition.Filtered, Skip = new SkipInfo("filtered", reason) };

        if (test.Kind == TestKind.Test)
        {
            // A file no positional named is collected for its setup tests only; its ordinary tests are unselected, exactly as a tag filter
            // leaves them. In a file named as `file:line`, so are the tests at other lines.
            if (!test.Selected)
            {
                return Filtered(selectedFiles.Contains(test.File) ? FilteredReason.Line : FilteredReason.File);
            }

            if (focused.Count > 0 && test.Mode != TestMode.Only)
            {
                return Filtered(FilteredReason.Focus);
            }

            if (!MatchesTags(test.Tags, filters.Tags, tagMode))
            {
                return Filtered(FilteredReason.Tags);
            }

            if (filters.ExcludeTags?.Any(test.Tags.Contains) == true)
            {
                return Filtered(FilteredReason.ExcludedTag);
            }

            var (grep, grepInvert) = PatternFilters(filters);
            if (grep is not null && !MatchesAny(grep, GrepText(test)))
            {
                return Filtered(FilteredReason.Grep);
            }

            if (grepInvert is not null && MatchesAny(grepInvert, GrepText(test)))
            {
                return Filtered(FilteredReason.GrepInvert);
            }

            if (filters.LastFailed is not null && !filters.LastFailed.Contains(Ids.ResultId(test.Id, target.Name, agent)))
            {
                return Filtered(FilteredReason.LastFailed);
            }
        }

        if (options.Platforms is not null && !options.Platforms.Contains(target.Platform))
        {
            return pair with
            {
                Disposition = Disposition.Filtered,
                Skip = new SkipInfo("platform-unavailable", $"test declares platforms [{string.Join(", ", options.Platforms)}]"),
            };
        }

        // Targets are graded from the engine's declared capability set and the kind of app the target declares.
        var capabilities = target.Engine?.Capabilities;
        var kinds = AppKinds(target);
        var missing = options.Requires.Where(capability => capabilities?.Contains(capability) != true && !kinds.Contains(capability)).ToList();
        if (missing.Count > 0)
        {
            return pair with
            {
                Disposition = Disposition.Skip,
                Skip = new SkipInfo("capability-unavailable", $"engine lacks required capabilities: {string.Join(", ", missing)}"),
            };
        }

        if (options.SkipReason is not null)
        {
            return pair with { Disposition = Disposition.Skip, Skip = new SkipInfo("explicit", options.SkipReason) };
        }

        return test.Kind == TestKind.Setup ? Filtered("setup test not required by selection") : pair;
    }

    /// <summary>
    /// Every pair the selection would run of a test the <paramref name="excluded"/> filters left out, reported as left out by the filter
    /// that named it. Skipped and filtered pairs keep their own reason: a filter only takes away what runs (upstream's <c>applyExclusions</c>).
    /// </summary>
    private static List<TestTargetPair> ApplyExclusions(IReadOnlyList<TestTargetPair> pairs, IReadOnlyDictionary<string, TestExclusion> excluded) =>
    [
        .. pairs.Select(pair =>
        {
            if (pair.Disposition != Disposition.Run || pair.Test.Kind != TestKind.Test || !excluded.TryGetValue(pair.Test.Id, out var left))
            {
                return pair;
            }

            return pair with { Disposition = Disposition.Filtered, Skip = new SkipInfo("filtered", left.Reason, Filter: left.Filter) };
        }),
    ];

    private static List<TestTargetPair> ApplySerialClosure(IReadOnlyList<TestTargetPair> pairs)
    {
        // A serial group is one unit per target and agent; members of one variant are selected together and never pull another variant in.
        static string UnitOf(TestTargetPair pair) => $"{pair.Target.Name}::{pair.Agent}::{pair.Test.SerialId}";
        var selectedSerialUnits = new HashSet<string>(
            pairs.Where(pair => pair.Disposition == Disposition.Run && pair.Test.SerialId is not null).Select(UnitOf),
            StringComparer.Ordinal);
        return
        [
            .. pairs.Select(pair =>
            {
                if (pair.Test.SerialId is null || !selectedSerialUnits.Contains(UnitOf(pair)))
                {
                    return pair;
                }

                return pair.Disposition == Disposition.Filtered && pair.Skip?.Cause == "filtered" ? pair with { Disposition = Disposition.Run, Skip = null } : pair;
            }),
        ];
    }

    /// <summary>
    /// What a shard is cut from: the ordinary pairs <paramref name="included"/> (the runnable ones, at the cut), a serial group counting
    /// once per target and agent so it is never split, in report order. The key doubles as the pair's item id (upstream's <c>shardItems</c>).
    /// </summary>
    private static List<string> ShardItems(IReadOnlyList<TestTargetPair> pairs, Func<TestTargetPair, bool>? included = null)
    {
        included ??= pair => pair.Disposition == Disposition.Run;
        var items = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in pairs)
        {
            if (pair.Test.Kind != TestKind.Test || !included(pair))
            {
                continue;
            }

            var item = ShardItem(pair);
            if (seen.Add(item))
            {
                items.Add(item);
            }
        }

        return items;
    }

    private static string ShardItem(TestTargetPair pair) => $"{pair.Target.Name}::{pair.Agent}::{pair.Test.SerialId ?? pair.Test.Id}";

    /// <summary>
    /// Keeps the shard's contiguous slice of the items and leaves the rest unselected. Contiguous rather than dealt round-robin so a
    /// shard's tests come from as few files as possible; the slices differ in size by at most one item, the first shards taking the
    /// remainder. Applied after every flag and the serial closure, before the test filters' exclusions and before the setup tests are
    /// chosen. A skipped test is not an item: the shard holding the item before it in report order reports it, the first shard when none
    /// comes before, so the shards together report it once (upstream's <c>applyShard</c>).
    /// </summary>
    private static List<TestTargetPair> ApplyShard(IReadOnlyList<TestTargetPair> pairs, Shard shard)
    {
        var items = ShardItems(pairs);
        var size = items.Count / shard.Total;
        var remainder = items.Count % shard.Total;
        var from = ((shard.Index - 1) * size) + Math.Min(shard.Index - 1, remainder);
        var to = from + size + (shard.Index - 1 < remainder ? 1 : 0);
        var kept = new HashSet<string>(items.Skip(from).Take(to - from), StringComparer.Ordinal);
        var previousKept = shard.Index == 1;
        var result = new List<TestTargetPair>(pairs.Count);
        foreach (var pair in pairs)
        {
            if (pair.Test.Kind != TestKind.Test || pair.Disposition == Disposition.Filtered)
            {
                result.Add(pair);
            }
            else if (pair.Disposition == Disposition.Skip)
            {
                result.Add(previousKept ? pair : pair with { Disposition = Disposition.Filtered });
            }
            else
            {
                previousKept = kept.Contains(ShardItem(pair));
                result.Add(previousKept ? pair : pair with { Disposition = Disposition.Filtered, Skip = new SkipInfo("filtered", FilteredReason.Shard) });
            }
        }

        return result;
    }

    private static List<TestTargetPair> ApplySessionSelection(
        IReadOnlyList<TestTargetPair> pairs,
        IReadOnlyDictionary<string, CollectedTest> sessionProducers,
        IReadOnlyList<UncollectedFile> uncollected)
    {
        // Each needed setup, by target and producer, with the sessions a selected test consumes from it.
        var neededSetups = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var pair in pairs)
        {
            if (pair.Disposition != Disposition.Run || pair.Options.Session is not { } session)
            {
                continue;
            }

            if (!sessionProducers.TryGetValue(session, out var producer))
            {
                // The setup may be in a file a narrowed run could not collect; its error is then the cause.
                var cause = uncollected.Count == 0
                    ? ""
                    : $"; it may be declared in a file that failed to collect: {NameFiles([.. uncollected.Select(entry => $"{entry.File} ({entry.Reason})")])}";
                var declared = sessionProducers.Keys.ToList();
                var known = declared.Count == 0
                    ? "; no collected setup test declares a session"
                    : $"; setup tests declare {string.Join(", ", declared.Select(name => $"\"{name}\""))}{Suggestions.DidYouMean(session, declared)}";
                throw new ConfigurationException(
                    "COLLECTION_ERROR",
                    $"test \"{string.Join(" > ", pair.Test.TitlePath)}\" in {pair.Test.File} consumes session \"{session}\" but no setup test produces it{known}{cause}");
            }

            var key = $"{pair.Target.Name}::{producer.Id}";
            if (!neededSetups.TryGetValue(key, out var sessions))
            {
                neededSetups[key] = sessions = new HashSet<string>(StringComparer.Ordinal);
            }

            sessions.Add(session);
        }

        var result = new List<TestTargetPair>(pairs.Count);
        foreach (var pair in pairs)
        {
            if (pair.Test.Kind != TestKind.Setup || !neededSetups.TryGetValue($"{pair.Target.Name}::{pair.Test.Id}", out var sessions))
            {
                result.Add(pair);
                continue;
            }

            // A producer the target cannot run - skipped for a missing capability or filtered out by its own platform list - cannot be
            // promoted; a consumer that needs it is a configuration error, not a silent run elsewhere.
            if (pair.Disposition == Disposition.Skip || pair.Skip?.Cause == "platform-unavailable")
            {
                throw new ConfigurationException(
                    "COLLECTION_ERROR",
                    $"setup test {pair.Test.Id} is required by a session consumer but cannot run on target \"{pair.Target.Name}\": {pair.Skip?.Reason ?? ""}");
            }

            // Saving and restoring a session ride the engine's state capability; refused here, before the setup runs its login only to fail at save.
            if (pair.Target.Engine is { } engine && !engine.Capabilities.Contains("state"))
            {
                throw new ConfigurationException(
                    "COLLECTION_ERROR",
                    $"setup test {pair.Test.Id} saves session {string.Join(", ", sessions.Select(name => $"\"{name}\""))} for a selected test, but target \"{pair.Target.Name}\" cannot save or restore a session: engine {engine.Name}, as configured, has no state capability");
            }

            result.Add(pair with { Disposition = Disposition.Run, Skip = null });
        }

        return result;
    }

    /// <summary>The kinds of app a target declares, which <c>requires</c> filters on beside the engine's capabilities: <c>browser</c> for an app at a URL, <c>native-app</c> for an installed app or a build a device launches (upstream's <c>appKinds</c>).</summary>
    private static List<string> AppKinds(ResolvedTarget target)
    {
        var (url, bundleId, appPath) = (target.App.Url, target.App.BundleId, target.App.AppPath);
        return [.. (url is null ? [] : new[] { "browser" }), .. (bundleId is null && appPath is null ? [] : new[] { "native-app" })];
    }
}
