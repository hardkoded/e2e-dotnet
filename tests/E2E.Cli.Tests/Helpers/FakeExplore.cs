// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.Helpers;

/// <summary>
/// <c>e2e explore</c> reads the process environment for accounts, so the tests that set an account run apart from the tests that
/// explore.
/// </summary>
internal static class ExploreEnvironment
{
    public const string Name = "ExploreEnvironment";
}

/// <summary>
/// An <see cref="Explore.ExploreAsync"/> that plays a scripted exploration through <see cref="ExploreOptions.OnProgress"/> and
/// returns its outcome, as upstream's list-reporter tests feed the reporter a stream of <c>explore</c> events.
/// </summary>
internal sealed class FakeExplore(IReadOnlyList<ExploreProgress> moments, ExploreOutcome outcome)
{
    /// <summary>The options the command passed.</summary>
    public ExploreOptions? Options { get; private set; }

    public Task<ExploreOutcome> RunAsync(ExploreOptions options, CancellationToken cancellationToken)
    {
        Options = options;
        foreach (var moment in moments)
        {
            options.OnProgress?.Invoke(moment);
        }

        return Task.FromResult(outcome);
    }

    /// <summary>Runs <c>e2e explore</c> with the fake and recorded output.</summary>
    public async Task<(int Code, string Stdout, string Stderr)> InvokeAsync(params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var code = await ExploreCommand.RunAsync(args, stdout, stderr, interactive: false, RunAsync, CancellationToken.None);
        return (code, stdout.ToString(), stderr.ToString());
    }
}
