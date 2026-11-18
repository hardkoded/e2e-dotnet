// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Disagreement = E2E.Tests.Crosscheck.Disagreement;
using Expectations = E2E.Tests.Crosscheck.CrosscheckExpectations;

namespace E2E.Tests.CrosscheckExpectations;

/// <summary>Ported from upstream's <c>crosscheck-expectations.test.ts</c>: the cross-check's committed expectations (keys, the diff, and the rewrite that keeps reasons).</summary>
public sealed class CrossCheckExpectationsTests : IDisposable
{
    private static readonly Disagreement FileRole = new(
        "chrome",
        "role",
        "textbox",
        "button",
        "textbox \"Attachments\""
    );
    private static readonly Disagreement Inert = new("chrome", "unknown", "button", "no AX node", "button \"Inert\"");

    // Temp dirs the cases here create, removed after each so a run leaves nothing behind.
    private readonly List<string> _tempDirs = [];

    public void Dispose()
    {
        foreach (var directory in _tempDirs)
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private string File(string content)
    {
        var root = Directory.CreateTempSubdirectory("crosscheck-").FullName;
        _tempDirs.Add(root);
        var target = Path.Combine(root, "expected.txt");
        System.IO.File.WriteAllText(target, content);
        return target;
    }

    private static string Key(string page, Disagreement disagreement) => Expectations.KeyOf(page, disagreement);

    [Fact]
    public void Reports_what_is_new_and_what_no_longer_happens_only_for_the_pages_that_ran()
    {
        var expected = Expectations.ReadExpectations(
            File(
                string.Join(
                    '\n',
                    "# header",
                    $"{Key("fixture: inputs", FileRole)} | a bug",
                    $"{Key("fixture: hidden", Inert)} | another",
                    $"{Key("benchmark: other", Inert)} | not run",
                    ""
                )
            )
        );
        var seen = new HashSet<string> { Key("fixture: inputs", FileRole), Key("fixture: inputs", Inert) };
        var (unexpected, stale) = Expectations.DiffExpectations(
            expected,
            seen,
            new HashSet<string> { "fixture: inputs", "fixture: hidden" }
        );
        Assert.Equal([Key("fixture: inputs", Inert)], unexpected);
        Assert.Equal([Key("fixture: hidden", Inert)], stale);
    }

    [Fact]
    public void Rewrites_the_pages_that_ran_keeps_surviving_reasons_and_pages_that_did_not_run_and_marks_new_lines_TODO()
    {
        var target = File($"{Key("fixture: inputs", FileRole)} | a bug\n{Key("benchmark: other", Inert)} | not run\n");
        var expected = Expectations.ReadExpectations(target);
        Expectations.WriteExpectations(
            target,
            "# header",
            expected,
            new HashSet<string> { Key("fixture: inputs", FileRole), Key("fixture: inputs", Inert) },
            new HashSet<string> { "fixture: inputs" }
        );
        Assert.Equal(
            string.Join(
                '\n',
                "# header",
                $"{Key("benchmark: other", Inert)} | not run",
                $"{Key("fixture: inputs", FileRole)} | a bug",
                $"{Key("fixture: inputs", Inert)} | {Expectations.TodoReason}",
                ""
            ),
            System.IO.File.ReadAllText(target)
        );
    }
}
