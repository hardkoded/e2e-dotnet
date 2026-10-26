// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Tests.SessionEnvelope;

/// <summary><c>describe('session file names')</c> of <c>tests/unit/session-envelope.test.ts</c>.</summary>
public sealed class SessionFileNamesTests : SessionEnvelopeKit
{
    /// <summary>A session whose payload names the pair that produced it.</summary>
    private static SavedSession StateFor(string targetId, string name) =>
        new(new EngineState("state", 1, new JsonObject { ["pair"] = $"{targetId}/{name}" }), NoSecrets);

    private static string Pair(SavedSession loaded) => (string)loaded.State.Data!["pair"]!;

    [Fact]
    public async Task Keeps_two_pairs_whose_joined_names_collide_apart()
    {
        var (store, _, _) = OpenStore();
        var first = Identity with { TargetId = "a--b" };
        var second = Identity with { TargetId = "a" };
        try
        {
            await store.SaveAsync("c", first, StateFor("a--b", "c"));
            await store.SaveAsync("b--c", second, StateFor("a", "b--c"));
            Assert.Equal("a--b/c", Pair(await store.LoadAsync("c", first)));
            Assert.Equal("a/b--c", Pair(await store.LoadAsync("b--c", second)));
        }
        finally
        {
            store.Cleanup();
        }
    }

    [Fact]
    public async Task Keeps_a_dot_joined_and_a_dash_joined_pair_apart()
    {
        var (store, _, _) = OpenStore();
        var first = Identity with { TargetId = "a.b" };
        var second = Identity with { TargetId = "a-b" };
        try
        {
            await store.SaveAsync("c", first, StateFor("a.b", "c"));
            await store.SaveAsync("c", second, StateFor("a-b", "c"));
            Assert.Equal("a.b/c", Pair(await store.LoadAsync("c", first)));
            Assert.Equal("a-b/c", Pair(await store.LoadAsync("c", second)));
        }
        finally
        {
            store.Cleanup();
        }
    }

    [Fact]
    public async Task Keeps_the_longest_allowed_names_within_the_file_name_limit()
    {
        var (store, root, runId) = OpenStore();
        var longName = new string('-', 128);
        var longTarget = string.Concat(Enumerable.Repeat("a.b-", 60)) + "end";
        var wide = Identity with { TargetId = longTarget };
        var twin = Identity with { TargetId = longTarget + "x" };
        try
        {
            await store.SaveAsync(longName, wide, StateFor(longTarget, longName));
            await store.SaveAsync(longName, twin, StateFor(longTarget + "x", longName));
            var files = Directory.GetFiles(Path.Combine(root, runId));
            Assert.Equal(2, files.Length);
            foreach (var file in files)
            {
                Assert.True(System.Text.Encoding.UTF8.GetByteCount(Path.GetFileName(file)) < 200);
            }

            Assert.Equal($"{longTarget}/{longName}", Pair(await store.LoadAsync(longName, wide)));
            Assert.Equal($"{longTarget}x/{longName}", Pair(await store.LoadAsync(longName, twin)));
        }
        finally
        {
            store.Cleanup();
        }
    }

    [Fact]
    public async Task Keeps_every_session_file_inside_the_run_directory()
    {
        var (store, root, runId) = OpenStore();
        var hostile = Identity with { TargetId = "../../escape" };
        try
        {
            await store.SaveAsync("..", hostile, StateFor("../../escape", ".."));
            await store.SaveAsync("x/y\\z", hostile, StateFor("../../escape", "x/y\\z"));
            var files = Directory.GetFiles(Path.Combine(root, runId));
            Assert.Equal(2, files.Length);
            foreach (var file in files)
            {
                Assert.Matches(@"^[A-Za-z0-9_%-]+\.json\z", Path.GetFileName(file));
            }

            Assert.Equal([runId], Directory.GetFileSystemEntries(root).Select(Path.GetFileName));
            Assert.Equal("../../escape/..", Pair(await store.LoadAsync("..", hostile)));
        }
        finally
        {
            store.Cleanup();
        }
    }

    /// <summary>Opens a store on a fresh temporary root.</summary>
    private (SessionStore Store, string Root, string RunId) OpenStore()
    {
        var root = TempRoot();
        var runId = Guid.CreateVersion7().ToString();
        return (SessionStore.Create(runId, root), root, runId);
    }
}
