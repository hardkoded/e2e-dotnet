// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Units;

/// <summary>The fixtures of <c>tests/unit/units.test.ts</c> (<c>makeTest</c>, <c>makePair</c>, <c>makeFile</c>).</summary>
internal static class UnitsKit
{
    public static readonly ResolvedTarget Target = new("web", "web", new AppConfig());

    private static readonly ResolvedTestOptions DefaultOptions = new(null, [], null, ["default"], null, false);

    public static CollectedTest MakeTest(
        string file,
        string title,
        int declarationIndex,
        TestKind kind = TestKind.Test,
        IReadOnlyList<string>? sessions = null
    ) =>
        new()
        {
            Kind = kind,
            Title = title,
            TitlePath = [title],
            DeclarationIndex = declarationIndex,
            Sessions = sessions ?? [],
            File = file,
            Id = $"test::{file}::{title}",
            Options = DefaultOptions,
        };

    public static TestTargetPair MakePair(
        CollectedTest test,
        ResolvedTarget? target = null,
        Disposition disposition = Disposition.Run,
        SkipInfo? skip = null,
        int repeat = 0
    ) =>
        new()
        {
            Test = test,
            Target = target ?? Target,
            Agent = "default",
            Repeat = repeat,
            Options = DefaultOptions,
            Disposition = disposition,
            Skip = skip,
        };

    public static CollectedFile MakeFile(string file) => new() { File = file };
}
