// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;

namespace E2E.Tests.ArtifactStore;

/// <summary>
/// The scaffolding of upstream's <c>artifact-store.test.ts</c> (its <c>root</c>, <c>afterEach</c>, and <c>capturing</c>): a temporary
/// artifacts root per test, removed when the test ends, and a store that keeps what it was handed.
/// </summary>
public abstract class ArtifactStoreHarness : IDisposable
{
    private readonly List<string> _roots = [];

    public void Dispose()
    {
        foreach (var directory in _roots)
        {
            Directory.Delete(directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    protected string Root()
    {
        var directory = Directory.CreateTempSubdirectory("e2e-artifact-store-").FullName;
        _roots.Add(directory);
        return directory;
    }

    protected static CapturingStore Capturing() => new();

    protected sealed class CapturingStore : IArtifactStore
    {
        private readonly ConcurrentQueue<StoredArtifact> _puts = new();

        public IReadOnlyList<StoredArtifact> Puts => [.. _puts];

        public Task<string> PutAsync(StoredArtifact artifact, CancellationToken cancellationToken = default)
        {
            _puts.Enqueue(artifact);
            return Task.FromResult("r2://bucket/" + artifact.Sha256[..8]);
        }
    }

    protected sealed class DelegateStore(Func<StoredArtifact, Task<string>> put) : IArtifactStore
    {
        public Task<string> PutAsync(StoredArtifact artifact, CancellationToken cancellationToken = default) => put(artifact);
    }
}
