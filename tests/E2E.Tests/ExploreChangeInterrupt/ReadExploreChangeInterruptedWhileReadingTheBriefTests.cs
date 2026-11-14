// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.ExploreChangeInterrupt;

/// <summary>
/// Upstream's <c>describe('readExploreChange interrupted while reading the brief')</c> (<c>tests/unit/explore-change-interrupt.test.ts</c>):
/// an interrupt that lands once the change is measured, while the brief is read, stops the read as an interrupt of the run does.
/// </summary>
public sealed class ReadExploreChangeInterruptedWhileReadingTheBriefTests
{
    /// <summary>
    /// Partial: the interrupt surfaces as a cancellation, where upstream rejects with <c>INTERRUPTED</c> and the message
    /// <c>interrupted while reading the change to explore</c>. Upstream mocks <c>findChanges</c> to abort once it returns;
    /// <see cref="ReadExploreChangeOptions.FindChanges"/> is the port's seam for the same.
    /// </summary>
    [Fact]
    public async Task Partial_Is_a_cancellation()
    {
        var dir = Directory.CreateTempSubdirectory("e2e-explore-change-interrupt-").FullName;
        try
        {
            GitRepo.InitRepo(dir);
            GitRepo.RunGit(dir, "branch", "base");
            GitRepo.CommitFiles(dir, new Dictionary<string, string> { ["a.txt"] = "a\n" }, "add");
            using var interrupt = new CancellationTokenSource();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ExploreChanges.ReadExploreChangeAsync(new ReadExploreChangeOptions
            {
                Cwd = dir,
                ChangedSince = "base",
                CancellationToken = interrupt.Token,
                Redact = text => text,
                FindChanges = async options =>
                {
                    var changes = await E2E.Changes.FindChangesAsync(options);
                    await interrupt.CancelAsync();
                    return changes;
                },
            }));
        }
        finally
        {
            GitRepo.RemoveRepo(dir);
        }
    }
}
