// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Select;

/// <summary>
/// One test as an upstream test file declares it with <c>test()</c>, <c>test.setup()</c>, and <c>test.describe()</c>, and as the port's
/// tests build it by hand, since the port has no registry to collect it from.
/// </summary>
internal sealed record Decl(string Title)
{
    /// <summary>The describe titles around the test, outermost first.</summary>
    public string[] Describe { get; init; } = [];

    /// <summary>The tags of the describe chain, outermost first.</summary>
    public string[] DescribeTags { get; init; } = [];

    public string[] Tags { get; init; } = [];

    public string? Session { get; init; }

    public string[]? Platforms { get; init; }

    public string[] Requires { get; init; } = [];

    public bool Skip { get; init; }

    public bool Only { get; init; }

    /// <summary>The title of the outermost serial describe the test sits in.</summary>
    public string? Serial { get; init; }

    /// <summary>The sessions a setup test saves; non-null makes the declaration a setup test.</summary>
    public string[]? Sessions { get; init; }

    public string[] Agents { get; init; } = ["default"];
}

/// <summary>The fixtures of <c>tests/unit/select.test.ts</c> and <c>tests/unit/test-filters.test.ts</c>.</summary>
internal static class SelectKit
{
    public static readonly string Root = Path.Combine(Path.GetTempPath(), "e2e-select-no-such-root");

    /// <summary>An engine that saves and restores state, so a test may use a session (upstream's <c>ENGINE</c>).</summary>
    public static readonly ResolvedEngine StateEngine = new("toy", new HashSet<string> { "state" });

    /// <summary>One collected file of the declarations, in order (upstream's <c>collection</c>).</summary>
    public static Collection CollectionOf(string file, params Decl[] declarations)
    {
        var tests = declarations.Select((decl, index) => Collected(file, decl, index)).ToList();
        return new Collection { Files = [new CollectedFile { File = file }], Tests = tests };
    }

    /// <summary>Several files collected together (upstream's <c>{ ...a, files: [...], tests: [...] }</c>).</summary>
    public static Collection Merge(params Collection[] collections) =>
        new() { Files = [.. collections.SelectMany(entry => entry.Files)], Tests = [.. collections.SelectMany(entry => entry.Tests)] };

    private static CollectedTest Collected(string file, Decl decl, int index)
    {
        string[] titlePath = [.. decl.Describe, decl.Title];
        var id = file + "::" + string.Join("::", titlePath.Select(AttemptReport.EncodeTitle));
        var setup = decl.Sessions is not null;
        return new CollectedTest
        {
            Kind = setup ? TestKind.Setup : TestKind.Test,
            Title = decl.Title,
            TitlePath = titlePath,
            DeclarationIndex = index,
            Sessions = decl.Sessions ?? [],
            Tags = [.. decl.DescribeTags.Concat(decl.Tags).Distinct()],
            File = file,
            Id = setup ? "setup::" + id : id,
            SerialId = decl.Serial is null ? null : file + "::serial::" + AttemptReport.EncodeTitle(decl.Serial),
            SerialRoot = decl.Serial is null ? null : new GroupNode(decl.Serial),
            Mode = decl.Only ? TestMode.Only : decl.Skip ? TestMode.Skip : TestMode.Normal,
            Options = new ResolvedTestOptions(decl.Platforms, decl.Requires, decl.Session, decl.Agents, decl.Skip ? "skipped" : null, decl.Serial is not null),
        };
    }

    /// <summary>A config on web targets (upstream's <c>config()</c>); every target takes the engine given, if any.</summary>
    public static ResolvedConfig ConfigOf(IReadOnlyList<ITestFilter>? filters = null, params ResolvedTarget[] targets) =>
        new()
        {
            ProjectRoot = Root,
            Targets = targets.Length == 0 ? [Web("web")] : targets,
            Filters = filters ?? [],
        };

    public static ResolvedTarget Web(string name, ResolvedEngine? engine = null, string platform = "web") =>
        new(name, platform, new AppConfig(), engine);

    /// <summary>The titles of the pairs <paramref name="selection"/> runs, in order (upstream's <c>running</c>).</summary>
    public static string[] Running(Selection selection) =>
        [.. selection.Pairs.Where(pair => pair.Disposition == Disposition.Run).Select(pair => pair.Test.Title)];

    /// <summary><c>excluded</c> as <c>applyTestFilters</c> hands it on: each test title left out by <c>filter</c> with <c>reason</c> (upstream's <c>leftOut</c>).</summary>
    public static Dictionary<string, TestExclusion> LeftOut(Collection collection, params (string Title, string Filter, string Reason)[] entries) =>
        entries.ToDictionary(entry => collection.Tests.First(test => test.Title == entry.Title).Id, entry => new TestExclusion(entry.Filter, entry.Reason));
}
