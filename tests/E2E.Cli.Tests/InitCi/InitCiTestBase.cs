// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.InitCi;

/// <summary>.NET-only test helper standing for the top of upstream's <c>init-ci.test.ts</c>: the <c>base</c> context and a throwaway directory.</summary>
public abstract class InitCiTestBase : IDisposable
{
    protected InitCiTestBase()
    {
        Dir = Directory.CreateTempSubdirectory("e2e-init-ci-").FullName;
    }

    protected string Dir { get; }

    /// <summary>The web job alone, for a project at the repository root, with the model key from secrets.</summary>
    private protected static WorkflowContext Base { get; } =
        new("MyApp.E2E/MyApp.E2E.csproj", "10.0", ["AI_GATEWAY_API_KEY"], new CiJobs(new CiWebJob()));

    /// <summary>Expo on both phones beside a web target: upstream's <c>expoJobs</c>.</summary>
    private protected static readonly CiJobs AllJobs = new(
        new CiWebJob(["web"]),
        new CiPhoneJob<IosBuild>(["ios"], new IosBuild.Expo()),
        new CiPhoneJob<AndroidBuild>(["android"], new AndroidBuild.Expo(true))
    );

    /// <summary>The phone jobs with a build of their own to write: upstream's <c>kind: 'custom'</c>.</summary>
    private protected static readonly CiJobs CustomJobs = new(
        null,
        new CiPhoneJob<IosBuild>(["ios"], new IosBuild.Custom()),
        new CiPhoneJob<AndroidBuild>(["android"], new AndroidBuild.Custom())
    );

    /// <summary>The phone jobs of <see cref="AllJobs"/> alone, for a mobile-only config.</summary>
    private protected static readonly CiJobs PhoneJobs = AllJobs with { Web = null };

    public void Dispose()
    {
        Directory.Delete(Dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    protected string Path_(string file) => Path.Combine(Dir, file);

    protected void Write(string file, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path_(file))!);
        File.WriteAllText(Path_(file), content);
    }

    /// <summary>The job names of a pipeline, in order: the top-level keys of a GitLab file, or the keys under jobs: of a workflow.</summary>
    protected static string[] Names(string pipeline, string pattern) =>
        [
            .. System
                .Text.RegularExpressions.Regex.Matches(
                    pipeline,
                    pattern,
                    System.Text.RegularExpressions.RegexOptions.Multiline
                )
                .Select(m => m.Groups[1].Value),
        ];
}
