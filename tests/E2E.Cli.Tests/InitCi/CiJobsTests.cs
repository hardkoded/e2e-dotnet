// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.InitCi;

/// <summary>Upstream's <c>describe('ciJobs')</c>.</summary>
public sealed class CiJobsTests
{
    private static readonly IosBuild Ios = new IosBuild.Expo();
    private static readonly AndroidBuild Android = new AndroidBuild.Expo(true);

    /// <summary>A job set as text, since a record holding a list does not compare by its items: <c>web=all</c> or <c>web=a,b</c>, and <c>ios=names</c>.</summary>
    private static string Shape(CiJobs jobs) =>
        string.Join(
            ";",
            new[]
            {
                jobs.Web is null
                    ? null
                    : "web=" + (jobs.Web.Targets is null ? "all" : string.Join(",", jobs.Web.Targets)),
                jobs.Ios is null ? null : "ios=" + string.Join(",", jobs.Ios.Targets),
                jobs.Android is null ? null : "android=" + string.Join(",", jobs.Android.Targets),
            }.Where(part => part is not null)
        );

    [Fact]
    public void Gives_each_phone_platform_its_job_and_runs_everything_else_in_the_web_job()
    {
        Assert.Equal("web=all", Shape(Ci.GetCiJobs([new("web", "web")], Ios, Android)));
        var both = Ci.GetCiJobs(
            [new("chromium", "web"), new("api", "custom"), new("iphone", "ios"), new("pixel", "android")],
            Ios,
            Android
        );
        Assert.Equal("web=chromium,api;ios=iphone;android=pixel", Shape(both));
        Assert.Equal(Ios, both.Ios!.Build);
        Assert.Equal(Android, both.Android!.Build);
        Assert.Equal("ios=ios,ipad", Shape(Ci.GetCiJobs([new("ios", "ios"), new("ipad", "ios")], Ios, Android)));
    }

    [Fact]
    public void Runs_targets_of_other_platforms_such_as_HTTP_tests_in_the_web_job_beside_phone_jobs_too()
    {
        Assert.Equal("web=all", Shape(Ci.GetCiJobs([new("default", "custom")], Ios, Android)));
        Assert.Equal("web=api;ios=ios", Shape(Ci.GetCiJobs([new("api", "custom"), new("ios", "ios")], Ios, Android)));
        Assert.False(Ci.GetCiJobs([], Ios, Android).Any);
    }
}
