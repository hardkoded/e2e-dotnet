// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.InitCi;

/// <summary>Upstream's <c>describe('CI_SERVICES')</c>.</summary>
public sealed class CiServicesTests
{
    [Fact]
    public void Has_one_service_per_ci_choice_in_the_order_the_flag_lists_them()
    {
        Assert.Equal(["github-actions", "gitlab"], Ci.Services.Select(service => service.Id));
        var (options, error) = InitFlags.Parse(["--ci", "gitlab"]);
        Assert.Null(error);
        Assert.Equal("gitlab", options!.Ci);
        Assert.Contains(
            "github-actions, gitlab",
            Assert.IsType<string>(InitFlags.Parse(["--ci", "x"]).Error),
            StringComparison.Ordinal
        );
    }
}
