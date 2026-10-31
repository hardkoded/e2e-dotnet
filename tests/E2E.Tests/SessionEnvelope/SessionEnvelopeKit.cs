// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using Microsoft.Extensions.Time.Testing;

namespace E2E.Tests.SessionEnvelope;

/// <summary>The helpers of <c>tests/unit/session-envelope.test.ts</c>: one identity, a temp-dir list the cases clean up, and <c>saveEnvelope</c>.</summary>
public abstract class SessionEnvelopeKit : IDisposable
{
    internal static readonly SessionIdentity Identity = new("web", "playwright", "1.61.1", 1, "web", new string('a', 64));

    protected const string SecretValue = "provider-minted-token-7391";

    /// <summary>Temp dirs the cases in a class create, removed after each so a run leaves nothing behind.</summary>
    private readonly List<string> _tempDirs = [];

    internal static IReadOnlyList<(string Name, string Value)> NoSecrets => [];

    /// <summary>The state every case saves.</summary>
    internal static EngineState Cookies() => new("playwright-state", 1, JsonNode.Parse("""{"cookies":[{"name":"sid","value":"abc"}]}"""));

    public void Dispose()
    {
        foreach (var directory in _tempDirs)
        {
            Directory.Delete(directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>A fresh temporary root the case removes at its end.</summary>
    protected string TempRoot()
    {
        var root = Directory.CreateTempSubdirectory("e2e-sessions-").FullName;
        _tempDirs.Add(root);
        return root;
    }

    /// <summary>Saves one state and returns the parsed envelope, its raw text, its file, and its store.</summary>
    internal async Task<(SessionStore Store, string Raw, string File, JsonObject Envelope)> SaveEnvelope(IReadOnlyList<(string Name, string Value)>? secrets = null, FakeTimeProvider? time = null)
    {
        var root = TempRoot();
        var runId = Guid.CreateVersion7().ToString();
        var store = SessionStore.Create(runId, root, time);
        await store.SaveAsync("member", Identity, new SavedSession(Cookies(), secrets ?? NoSecrets));
        var file = Assert.Single(Directory.GetFiles(Path.Combine(root, runId)));
        Assert.Matches(@"^web--member-[0-9a-f]{16}\.json\z", Path.GetFileName(file));
        var raw = await File.ReadAllTextAsync(file);
        return (store, raw, file, JsonNode.Parse(raw)!.AsObject());
    }
}
