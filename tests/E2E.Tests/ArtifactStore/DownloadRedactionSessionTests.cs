// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace E2E.Tests.ArtifactStore;

/// <summary>
/// .NET-only: the download redaction of an <see cref="AttemptScope"/>. Upstream's sink reads the ledger once, when a download is
/// registered (<c>describe('download redaction')</c> of <c>artifact-store.test.ts</c>, ported in <see cref="DownloadRedactionTests"/>). The
/// port builds its report when the attempt ends, so the scope scans a download again with the values a later fill registered.
/// </summary>
public sealed class DownloadRedactionSessionTests : IDisposable
{
    private const string Secret = "download-secret-Qx7-2718";
    private readonly string _root = Directory.CreateTempSubdirectory("e2e-artifact-store-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string AttemptDir => Path.Combine(_root, "test", "attempt-1");

    private AttemptScope Downloads(params (string Name, string Value)[] filled)
    {
        Directory.CreateDirectory(Path.Combine(AttemptDir, "downloads"));
        var scope = new AttemptScope
        {
            Session = null!,
            Agents = new Dictionary<string, ResolvedAgent>(),
            Cache = null,
            CacheEnabled = false,
            TestTitle = "downloads",
            EnginePlatform = "web",
            ArtifactsDir = AttemptDir,
        };
        foreach (var (name, value) in filled)
        {
            scope.Register(name, value);
        }

        return scope;
    }

    private string Write(string name, byte[] bytes)
    {
        File.WriteAllBytes(Path.Combine(AttemptDir, "downloads", name), bytes);
        return "downloads/" + name;
    }

    private string Write(string name, string text) => Write(name, Encoding.UTF8.GetBytes(text));

    private string Read(string name) => File.ReadAllText(Path.Combine(AttemptDir, "downloads", name), new UTF8Encoding(false));

    private async Task<JsonObject> RecordAsync(AttemptScope scope, int index)
    {
        await scope.Artifacts!.SettleAsync();
        var attached = scope.Attached[index];
        return ReportWire.Artifact(new ReportArtifact(attached.Id, attached.Kind.ToString().ToLowerInvariant(), attached.Path, Redaction: attached.Redaction), _root, null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rescans_downloads_with_values_registered_after_attachment(bool alreadyHasSecret)
    {
        var scope = alreadyHasSecret ? Downloads(("api-key", Secret)) : Downloads();
        const string later = "later-credential-Jk8-5931";
        scope.AttachArtifact("download", Write("export.txt", $"key={later}\n"));
        Assert.Equal(alreadyHasSecret ? "complete" : "incomplete", (string?)(await RecordAsync(scope, 0))["redaction"]);
        Assert.Equal($"key={later}\n", Read("export.txt"));

        if (alreadyHasSecret)
        {
            // FillAsync remembers a Secret through this path; the earlier registered value remains known too.
            scope.Remember(new Dictionary<string, object?> { ["api-key"] = global::E2E.Secret.Create("api-key", later) });
        }
        else
        {
            scope.Register("api-key", later);
        }

        var record = await RecordAsync(scope, 0);
        const string redacted = "key=<secret:api-key>\n";
        Assert.Equal("complete", (string?)record["redaction"]);
        Assert.Equal(redacted, Read("export.txt"));
        Assert.Equal(Encoding.UTF8.GetByteCount(redacted), (long?)record["size"]);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(redacted))), (string?)record["sha256"]);
    }

    // A failed final scan must replace the earlier registration's successful result.
    [Fact]
    public async Task Reports_a_previously_complete_download_incomplete_when_the_final_scan_fails()
    {
        var scope = Downloads(("api-key", Secret));
        scope.AttachArtifact("download", Write("export.txt", "nothing secret here"));
        Assert.Equal("complete", (string?)(await RecordAsync(scope, 0))["redaction"]);

        Write("export.txt", [0xff, 0xfe]);
        scope.Register("api-key", "later-credential-Jk8-5931");
        Assert.Equal("incomplete", (string?)(await RecordAsync(scope, 0))["redaction"]);
    }

    // Regression: upstream's ASCII spelling shortcut also misses these valid JSON encodings.
    [Theory]
    [InlineData("""{"key":"\u0068\u0075\u006e\u0074\u0065\u0072\u0032"}""", """{"key":"<secret:api-key>"}""")]
    [InlineData("""{"key":"\u0068\u0075\u006E\u0074\u0065\u0072\u0032"}""", """{"key":"<secret:api-key>"}""")]
    [InlineData("""{"key":"hunter\u0032"}""", """{"key":"<secret:api-key>"}""")]
    [InlineData("""{"text":"{\"key\":\"\\u0068\\u0075\\u006e\\u0074\\u0065\\u0072\\u0032\"}"}""", """{"text":"{\"key\":\"<secret:api-key>\"}"}""")]
    public async Task Redacts_JSON_Unicode_escaped_ASCII_secrets_before_reporting_complete(string body, string redacted)
    {
        var scope = Downloads(("api-key", "hunter2"));
        scope.AttachArtifact("download", Write("export.json", body));
        var record = await RecordAsync(scope, 0);
        Assert.Equal("complete", (string?)record["redaction"]);
        Assert.Equal(redacted, Read("export.json"));
        Assert.Equal(Encoding.UTF8.GetByteCount(redacted), (long?)record["size"]);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(redacted))), (string?)record["sha256"]);
    }

    // A BOM-less UTF-16 file can pass UTF-8 decoding while its NUL bytes keep a secret from matching.
    [Fact]
    public async Task Leaves_a_UTF_16_download_with_a_value_incomplete_as_served()
    {
        var scope = Downloads(("api-key", Secret));
        var utf16 = Encoding.Unicode.GetBytes($"key={Secret}");
        scope.AttachArtifact("download", Write("export.csv", utf16));
        Assert.Equal("incomplete", (string?)(await RecordAsync(scope, 0))["redaction"]);
        Assert.Equal(utf16, File.ReadAllBytes(Path.Combine(AttemptDir, "downloads", "export.csv")));
    }
}
