// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Reports;
using static E2E.Decision.Tests.FilterHelpers;

namespace E2E.Decision.Tests.FilterRunner;

/// <summary>
/// Upstream's <c>describe('decisionFilter through e2e/runner')</c> in <c>tests/filter-runner.test.ts</c>. Upstream calls the runner's
/// <c>list</c> over test files it links to <c>e2e</c>; here <c>HostTestFilters.ExcludeAsync</c>, the seam a host runs the filters through, takes
/// the tests an adapter found by reflection.
/// </summary>
public sealed class DecisionFilterThroughE2ERunnerTests : IDisposable
{
    /// <summary>The lines between a private key's armor. Each starts with a letter, so git names the one before a hunk in its header.</summary>
    private static readonly string[] KeyBody =
    [
        "xKeHYQKKYmUIHQUvbL7GD750SXH0WBXc6bbXLt4bdV+uXZqRUz1eI+aY1ScVJc4y",
        "S6zifhBTNEGyB9oJAIa5tz5ysBf2KeiKJ1IxTX6NM2iy1MHdiiYASaEWsgczEERc",
        "CWRxVgjsPQIi0i7ipQkTw1bSSj4Do4fN7oK0dM7aV0UZ3IosvqejGgi0wtS4bsLc",
        "QHN92o+8A0UKM07rsb0kHw2Suae36glwK36DxlsIv39zIMH0aLER07vcp+OiAcj2",
        "aQCHBjMz16MMVuDQzZ1+166hj0dCWD2MeZCwLloc47gzHBRUwHkP+oB+lLyXpidZ",
        "blFibO92Ojru4SBe+OEPfbHKIJPryDHzjrvzYQjinZFkucTuly6LC1cneD4VcDzv",
        "BzLNjY5dbqYqnR3kQBIpzHRecSOGnAssaJ1F+vu7jVgw3CJqfCJu+ZY9AgKppO5j",
        "kQX47iWXWqzd7zhnnjRe1IBALJBm6RImI9lr9dMimYrSsXDidKRTBJpNlo7eLwSF",
        "f4dOHnruzxiTOpVjARdbXrRLWhNaUYUj1ZW1dvLqOg+uts0fUl6+A1BKzO0twks+",
        "dMDEh1aTEWM5zuShco2ysKT+ANbjlP+P7G9eNIXrKOyG54T2fdLQcNOey0x7Cg0B",
        "LvkmGVJZiDkdXe13oRS74SiFvpn9XFQw3KVCRYDne548wO+GTggdu1gX9DGbJEIZ",
        "OW8s0NfAcDDnCZAWm4MQhgiEb4wCgxPmKZNepNzsDl8cQYQaaecnLAb9AuFFkuug",
    ];

    /// <summary>A 14-line private key, configured as a secret.</summary>
    private static readonly string PrivateKey = string.Join(
        "\n",
        ["-----BEGIN PRIVATE KEY-----", .. KeyBody, "-----END PRIVATE KEY-----"]
    );

    /// <summary>The line a commit writes in place of the key's seventh body line. It shares no 8 characters with the key, so the model reads it.</summary>
    private const string RotatedLine = "MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQC7VJTUt9Us8cKQ";

    private readonly List<ChangedRepo> _repos = [];

    public void Dispose()
    {
        foreach (var repo in _repos)
        {
            repo.Dispose();
        }
    }

    private ChangedRepo Repo(Dictionary<string, string> baseFiles, Dictionary<string, string> change)
    {
        var repo = ChangedRepoOf(baseFiles, change);
        _repos.Add(repo);
        return repo;
    }

    private static E2EConfig ConfigOf(ChangedRepo repo, string json) =>
        E2EConfig.Parse(json, Directory.ResolveLinkTarget(repo.Root, true)?.FullName ?? repo.Root, _ => null);

    private static DiscoveredTest Found(string ns, string type, string method) =>
        new($"{ns}.{type}.{method}", ns, type, method, []);

    [Fact]
    public async Task Leaves_out_the_tests_it_rates_unlikely_so_the_runner_accepts_its_scores_along_with_its_exclusions()
    {
        using var repo = Repo(
            new Dictionary<string, string>
            {
                ["tests/Cart/CartTests.cs"] = "class CartTests { void Adds_an_item() {} }\n",
                ["tests/Login/LoginTests.cs"] = "class LoginTests { void Signs_in() {} }\n",
                ["src/Cart.cs"] = "class Cart { const int Total = 1; }\n",
            },
            new Dictionary<string, string> { ["src/Cart.cs"] = "class Cart { const int Total = 2; }\n" }
        );
        var model = ScriptedJudge(text => text.Contains("Signs_in", StringComparison.Ordinal) ? 0.05 : 0.9);
        var selection = await HostTestFilters.ExcludeAsync(
            ConfigOf(repo, "{}"),
            [E2E.Decision.Filter.DecisionFilter(new DecisionFilterOptions { Model = model })],
            repo.Base,
            [Found("App.Tests.Cart", "CartTests", "Adds_an_item"), Found("App.Tests.Login", "LoginTests", "Signs_in")],
            _ => { },
            CancellationToken.None
        );

        // A runner that refused the result would leave nothing out.
        var left = Assert.Single(selection.Excluded);
        Assert.Equal(("App.Tests.Login.LoginTests.Signs_in", "5% likely affected"), (left.Key, left.Value));
        var result = Assert.Single(selection.Results);
        Assert.Equal(ResultStatus.Skipped, result.Status);
        Assert.Equal(new ReportSkip("filtered", "5% likely affected", "decision"), result.Skip);
    }

    [Fact]
    public async Task Sends_no_line_of_a_configured_private_key_when_commits_edit_the_line_after_it_or_one_in_its_middle_the_diffs_context_lines_and_hunk_headers_included()
    {
        using var repo = Repo(
            new Dictionary<string, string>
            {
                ["tests/Login/LoginTests.cs"] = "class LoginTests { void Signs_in() {} }\n",
                ["src/after.ts"] = $"export const key = `{PrivateKey}`;\nexport const rotated = 1;\n",
                ["src/middle.ts"] = $"export const key = `{PrivateKey}`;\n",
            },
            new Dictionary<string, string>
            {
                ["src/after.ts"] = $"export const key = `{PrivateKey}`;\nexport const rotated = 2;\n",
                ["src/middle.ts"] =
                    $"export const key = `{PrivateKey.Replace(KeyBody[6], RotatedLine, StringComparison.Ordinal)}`;\n",
            }
        );
        var model = ScriptedJudge(_ => 0.9);
        var config = ConfigOf(repo, JsonSerializer.Serialize(new { secrets = new { deployKey = PrivateKey } }));
        await HostTestFilters.ExcludeAsync(
            config,
            [E2E.Decision.Filter.DecisionFilter(new DecisionFilterOptions { Model = model })],
            repo.Base,
            [Found("App.Tests.Login", "LoginTests", "Signs_in")],
            _ => { },
            CancellationToken.None
        );
        var request = Assert.Single(model.Requests);
        var diff = request.State!.Value.GetProperty("diff").GetString()!;
        Assert.Contains("-export const rotated = 1;\n+export const rotated = 2;\n", diff);
        Assert.Contains($"+{RotatedLine}", diff);
        var sent = JsonSerializer.Serialize(model.Requests.Select(r => r.State!.Value.GetRawText()));
        foreach (var line in PrivateKey.Split('\n'))
        {
            Assert.DoesNotContain(line, sent, StringComparison.Ordinal);
        }
    }
}
