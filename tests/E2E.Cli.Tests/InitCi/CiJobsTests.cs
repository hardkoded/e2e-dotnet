// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.InitCi;

/// <summary>Upstream's <c>describe('ciJobs')</c>. The jobs name no targets: the port picks one in the fixture, not with <c>--target</c>.</summary>
public sealed class CiJobsTests
{
    [Fact]
    // Partial: a job is a flag, since the port has no --target to name its targets, and no build kind to pass.
    public void Partial_Gives_each_phone_platform_its_job_and_runs_everything_else_in_the_web_job()
    {
        Assert.Equal(new CiJobs(true, false, false), Ci.GetCiJobs([new("web", "web")]));
        Assert.Equal(
            new CiJobs(true, true, true),
            Ci.GetCiJobs([new("chromium", "web"), new("api", "custom"), new("iphone", "ios"), new("pixel", "android")])
        );
        Assert.Equal(new CiJobs(false, true, false), Ci.GetCiJobs([new("ios", "ios"), new("ipad", "ios")]));
    }

    [Fact]
    public void Partial_Runs_targets_of_other_platforms_such_as_HTTP_tests_in_the_web_job_beside_phone_jobs_too()
    {
        Assert.Equal(new CiJobs(true, false, false), Ci.GetCiJobs([new("default", "custom")]));
        Assert.Equal(new CiJobs(true, true, false), Ci.GetCiJobs([new("api", "custom"), new("ios", "ios")]));
        Assert.False(Ci.GetCiJobs([]).Any);
    }
}
