// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using static E2E.Cli.Tests.ReportList.ListReporterKit;

namespace E2E.Cli.Tests.ReportList;

/// <summary>Upstream: packages/e2e/tests/unit/report-list.test.ts, describe('ListReporter goldens').</summary>
public sealed class GoldensTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("e2e-list-golden-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // Partial: only the failure entry of `list-fail.txt` is compared, from its error to its video row. The port has no golden files, no
    // timed-out, interrupted, or flaky results in the same run, and upstream's fixture has since grown a closing summary of failures
    // (declared lines) that the port does not print. The error's location is `ReportError.Source`, where upstream reads its stack.
    [Fact]
    public void Partial_Prints_a_failing_run_with_the_failure_code_frame_and_evidence()
    {
        var source = Path.Combine(_root, "tests", "login.e2e.ts");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllText(source, string.Join('\n', "import { test } from '@e2e-dev/web';", "", "await app.open();", "await expect(status).toContainText(\"Welcome\");", "await done();", "", "export {};"));
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(RunStarted(projectRoot: _root));
        reporter.Handle(Plan(("tests/login.e2e.ts", 1)));
        var failed = Attempt(error: new ReportError("ASSERTION_FAILED", "expect.toContainText failed\nexpected \"Welcome\", got \"Sign in\"", Source: new SourceLocation("tests/login.e2e.ts", 4, 22)), status: AttemptStatus.Failed) with
        {
            Failure = new FailureEvidence { Url = "https://app.test/login", Candidates = ["heading \"Sign in\""], Screen = "attempt-1:artifact:2" },
            Artifacts =
            [
                new ReportArtifact("attempt-1:artifact:0", "video", "chromium/login/attempt-0/video/video.webm"),
                new ReportArtifact("attempt-1:artifact:1", "download", "chromium/login/attempt-0/downloads/export.csv"),
                new ReportArtifact("attempt-1:artifact:2", "log", "chromium/login/attempt-0/screen.txt"),
            ],
        };
        reporter.Handle(Finished(Result(ResultStatus.Failed, id: "welcomes", file: "tests/login.e2e.ts", title: ["login", "welcomes"], attempts: [failed])));
        reporter.Handle(RunFinished(ReportRunStatus.Failed, 1, reportPath: Path.Combine(_root, ".e2e", "report.json")));
        var start = output.Lines.IndexOf(" FAIL  |chromium| tests/login.e2e.ts > login > welcomes");
        Assert.True(start > -1);
        Assert.Equal(
            [
                " FAIL  |chromium| tests/login.e2e.ts > login > welcomes",
                "ASSERTION_FAILED: expect.toContainText failed",
                "expected \"Welcome\", got \"Sign in\"",
                " ❯ tests/login.e2e.ts:4:22",
                "      2|",
                "      3| await app.open();",
                "      4| await expect(status).toContainText(\"Welcome\");",
                "       |                      ^",
                "      5| await done();",
                "      6|",
                " ❯ at https://app.test/login",
                " ❯ on screen heading \"Sign in\"",
                " ❯ screen .e2e/artifacts/chromium/login/attempt-0/screen.txt",
                " ❯ video .e2e/artifacts/chromium/login/attempt-0/video/video.webm",
            ],
            output.Lines.Skip(start).Take(14).Select(line => line.Replace(_root, "/project", StringComparison.Ordinal)));
    }
}
