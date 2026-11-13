// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;

namespace E2E.Tests.SkipAfterFailure;

/// <summary>The <c>it</c> of upstream's <c>skip-after-failure.test.ts</c> that sits outside any <c>describe</c>.</summary>
public sealed class SkipAfterFailureTests : IDisposable
{
    private const string Token = "synthetic-skip-token-271828";

    private readonly string _results = Path.Combine(Directory.CreateTempSubdirectory("e2e-skip-reason-").FullName, "results");

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_results)!, recursive: true);

    /// <summary>
    /// Partial: upstream runs a project through the runner with the json, markdown, and junit reporters and reads their files and the
    /// output; the port has no runner, so the static reasons come from the collection (<c>HostTestFilters.Collect</c>) and the runtime ones
    /// from a session's report. The "failed skip" test (a failure outranks the skip, and its reason stays beside it) has no port: an
    /// attempt that failed keeps no skip reason.
    /// </summary>
    [Fact]
    public async Task Partial_Redacts_and_bounds_static_and_runtime_skip_reasons_before_reporters_see_them()
    {
        var longReason = new string('é', 9000);
        var collection = HostTestFilters.Collect(
            [
                new DiscoveredTest("static secret", "E2E.Tests.SkipAfterFailure", "Skipping", "static secret", [], $"blocked {Token}"),
                new DiscoveredTest("long static", "E2E.Tests.SkipAfterFailure", "Skipping", "long static", [], longReason),
            ],
            [],
            [("key", Token)],
            out _);
        var staticReasons = collection.Tests.Select(test => test.Options.SkipReason!).ToList();

        var runtimeReasons = new List<string>();
        foreach (var reason in new[] { $"blocked {Token}", longReason })
        {
            var session = await E2ESession.StartAsync(new E2ESessionOptions
            {
                Engine = new E2E.Engine.DocumentEngine(new E2E.Engine.DocumentWorld()),
                TestTitle = "runtime skip",
                ResultsDirectory = _results,
                ProjectRoot = _results,
            });
            session.Register("key", Token);
            session.Complete(new SkipException(reason));
            await session.DisposeAsync();
            runtimeReasons.Add(Assert.Single(Assert.Single(session.Report()!.Run.Results).Attempts).Skip!.Reason);
        }

        Assert.Equal("blocked <secret:key>", staticReasons[0]);
        Assert.Equal("blocked <secret:key>", runtimeReasons[0]);
        Assert.NotEmpty(staticReasons[1]);
        Assert.NotEmpty(runtimeReasons[1]);
        foreach (var reason in staticReasons.Concat(runtimeReasons))
        {
            Assert.True(Encoding.UTF8.GetByteCount(reason) <= 8192);
            Assert.DoesNotContain(Token, reason, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(Token, JsonSerializer.Serialize(runtimeReasons), StringComparison.Ordinal);
    }
}
