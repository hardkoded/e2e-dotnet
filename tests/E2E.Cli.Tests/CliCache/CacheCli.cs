// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.CliCache;

/// <summary>
/// The helpers of upstream <c>tests/unit/cli-cache.test.ts</c>: a temporary project whose config puts the replay cache in <c>store/</c>, a
/// <c>seed</c> that writes entries through the store, and an <c>invoke</c> that runs <c>e2e cache</c> in process and keeps what it printed.
/// </summary>
internal sealed class CacheCli : IDisposable
{
    public const string CacheDir = "store";

    public CacheCli()
    {
        Root = Directory.CreateTempSubdirectory("e2e-cache-cli-").FullName;
        WriteConfig("""{ "targets": [{ "name": "web", "platform": "web", "app": { "url": "http://127.0.0.1:3000" } }], "cache": { "dir": "store" } }""");
    }

    public string Root { get; }

    public string Store => Path.Combine(Root, CacheDir);

    public string Stdout { get; private set; } = "";

    public string Stderr { get; private set; } = "";

    public int ExitCode { get; private set; }

    public void Dispose() => Directory.Delete(Root, recursive: true);

    public void WriteConfig(string json) => File.WriteAllText(Path.Combine(Root, "e2e.config.json"), json);

    /// <summary>Upstream's <c>trace(recordedFor, actions)</c>: a recording of <paramref name="actions"/> taps.</summary>
    public static CacheEntry Trace(string? test, string? target, string? instruction, int actions) => new()
    {
        Test = test,
        TargetId = target,
        Instruction = instruction,
        Actions = [.. Enumerable.Range(0, actions).Select(index => new RecordedAction { Kind = "tap", Summary = "tap " + index, Role = "button", Name = "Button " + index })],
    };

    public void Seed(params (string KeyHash, CacheEntry Entry)[] entries)
    {
        var store = new FileStepCache(Store);
        foreach (var (keyHash, entry) in entries)
        {
            store.Write(keyHash, entry);
        }
    }

    public void Invoke(params string[] args) => Invoke(args, env: null);

    public void Invoke(string[] args, IReadOnlyDictionary<string, string?>? env)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        ExitCode = CacheCommand.Run([.. args, "--config", Path.Combine(Root, "e2e.config.json")], stdout, stderr, interactive: false, Root, env);
        Stdout = stdout.ToString();
        Stderr = stderr.ToString();
    }
}
