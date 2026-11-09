// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Tests.Explore;
using static E2E.Tests.Explore.ExploreHarness;
using Explorer = E2E.Explore;

namespace E2E.Tests.ReportConfigDigest;

/// <summary>
/// .NET-only: upstream has no <c>it</c> that reads <c>project.configDigest</c> from an exploration's report. Its exploration runs through the
/// runner, whose <c>buildReport</c> takes the resolved config; the port's <see cref="Explorer.ExploreAsync"/> opens its own session, which
/// takes the config in <c>E2ESessionOptions.Config</c>.
/// </summary>
public sealed class ExploreReportConfigDigestTests
{
    [Fact]
    public async Task An_explorations_report_carries_the_digest_of_the_config_it_ran_under()
    {
        var model = new ExploreModel(_ => ExploreModel.Finish("Nothing here."), _ => ExploreModel.Done("passed", "unused"));
        var root = TempRoot();
        var config = ExploreHarness.Config(root, "\"actionTimeout\": 10000");
        var options = Options(root, model.Model, [], config: config);
        await Explorer.ExploreAsync(options);

        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Assert.Single(Files(root), file => Path.GetFileName(file) == "report.json")));
        var digest = report.RootElement.GetProperty("run").GetProperty("project").GetProperty("configDigest").GetString();
        Assert.Equal(HostTestFilters.Resolve(config, [], options.Agents).ConfigDigest, digest);
        Assert.NotEqual(new string('0', 64), digest);

        // The agents built in code enter the digest, as a fixture's do.
        Assert.NotEqual(HostTestFilters.Resolve(config, []).ConfigDigest, digest);
    }
}
