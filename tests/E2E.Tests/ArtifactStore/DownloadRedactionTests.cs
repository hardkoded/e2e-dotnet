// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace E2E.Tests.ArtifactStore;

/// <summary>
/// <c>describe('download redaction')</c> of upstream <c>artifact-store.test.ts</c>. The port has no <c>createAttemptArtifacts</c> or
/// <c>ArtifactStore</c>, so each test registers the download through <see cref="AttemptScope.AttachArtifact"/> and reads the record
/// <see cref="ReportWire.Artifact"/> writes; the assertions on what the store was handed check the file on disk and the record instead.
/// </summary>
public sealed class DownloadRedactionTests : IDisposable
{
    private const string Secret = "download-secret-Qx7-2718";
    private readonly string _root = Directory.CreateTempSubdirectory("e2e-artifact-store-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private AttemptScope Downloads(params (string Name, string Value)[] filled)
    {
        Directory.CreateDirectory(Path.Combine(_root, "downloads"));
        var scope = new AttemptScope
        {
            Session = null!,
            Agents = new Dictionary<string, ResolvedAgent>(),
            Cache = null,
            CacheEnabled = false,
            TestTitle = "downloads",
            EnginePlatform = "web",
            ArtifactsDir = _root,
        };
        foreach (var (name, value) in filled)
        {
            scope.Register(name, value);
        }

        return scope;
    }

    private string Write(string name, byte[] bytes)
    {
        File.WriteAllBytes(Path.Combine(_root, "downloads", name), bytes);
        return "downloads/" + name;
    }

    private string Write(string name, string text) => Write(name, Encoding.UTF8.GetBytes(text));

    private string Read(string name) => File.ReadAllText(Path.Combine(_root, "downloads", name), new UTF8Encoding(false));

    private JsonObject Record(AttemptScope scope, int index)
    {
        var attached = scope.Attached[index];
        return ReportWire.Artifact(new ReportArtifact(attached.Id, attached.Kind, attached.Path, Redaction: attached.Redaction), _root, null);
    }

    [Fact]
    public void Labels_a_download_incomplete_by_default_bytes_the_app_served_not_rewritten_on_the_record_alone()
    {
        var scope = Downloads();
        var body = $"id,key\n1,{Secret}\n";
        scope.AttachArtifact("download", Write("export.csv", body));
        var record = Record(scope, 0);
        Assert.Equal("download", (string?)record["kind"]);
        Assert.Equal("text/csv", (string?)record["mediaType"]);
        Assert.Equal("incomplete", (string?)record["redaction"]);
        Assert.Equal(body, Read("export.csv"));
    }

    [Fact]
    public void Rewrites_a_text_download_with_nothing_filled_a_value_can_reach_the_app_without_a_fill()
    {
        var scope = Downloads(("api-key", Secret));
        scope.AttachArtifact("download", Write("headers.txt", $"authorization: {Secret}\n"));
        Assert.Equal("complete", (string?)Record(scope, 0)["redaction"]);
        Assert.Equal("authorization: <secret:api-key>\n", Read("headers.txt"));
    }

    [Fact]
    public void Rewrites_a_text_download_through_the_ledger_once_a_secret_was_filled_and_labels_it_complete_on_the_record_alone()
    {
        var scope = Downloads(("api-key", Secret));
        scope.AttachArtifact("download", Write("export.json", $"{{\"key\":\"{Secret}\"}}"));
        var redacted = "{\"key\":\"<secret:api-key>\"}";
        var record = Record(scope, 0);
        Assert.Equal("application/json", (string?)record["mediaType"]);
        Assert.Equal("complete", (string?)record["redaction"]);
        Assert.Equal(redacted, Read("export.json"));
        Assert.Equal(Encoding.UTF8.GetByteCount(redacted), (long?)record["size"]);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(redacted))), (string?)record["sha256"]);
    }

    [Fact]
    public void Rewrites_fragments_and_encoded_forms_in_a_download_the_way_trace_text_entries_are()
    {
        var scope = Downloads(("api-key", Secret));
        var cut = Secret.Substring(6, 14);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(Secret));
        scope.AttachArtifact("download", Write("export.txt", $"cut={cut}\nencoded={encoded}\n"));
        Assert.Equal("complete", (string?)Record(scope, 0)["redaction"]);
        Assert.Equal("cut=<secret:api-key>\nencoded=<secret:api-key>\n", Read("export.txt"));
    }

    [Fact]
    public void Rewrites_a_value_CSV_quoted_its_double_quotes_doubled()
    {
        var scope = Downloads(("api-key", "pa\"ss,word"));
        scope.AttachArtifact("download", Write("export.csv", "id,key\n1,\"pa\"\"ss,word\"\n"));
        Assert.Equal("complete", (string?)Record(scope, 0)["redaction"]);
        Assert.Equal("id,key\n1,\"<secret:api-key>\"\n", Read("export.csv"));
    }

    [Fact]
    public void Keeps_a_byte_order_mark_on_a_rewritten_download_on_the_record_alone()
    {
        var scope = Downloads(("api-key", Secret));
        byte[] bom = [0xef, 0xbb, 0xbf];
        scope.AttachArtifact("download", Write("export.csv", [.. bom, .. Encoding.UTF8.GetBytes($"key\n{Secret}\n")]));
        var rewritten = File.ReadAllBytes(Path.Combine(_root, "downloads", "export.csv"));
        var record = Record(scope, 0);
        Assert.True(rewritten.AsSpan(0, 3).SequenceEqual(bom));
        Assert.Equal("key\n<secret:api-key>\n", Encoding.UTF8.GetString(rewritten, 3, rewritten.Length - 3));
        Assert.Equal("complete", (string?)record["redaction"]);
        Assert.Equal(rewritten.Length, (long?)record["size"]);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(rewritten)), (string?)record["sha256"]);
    }

    [Fact]
    public void Labels_a_scanned_text_download_complete_when_it_held_nothing_to_redact()
    {
        var scope = Downloads(("api-key", Secret));
        scope.AttachArtifact("download", Write("notes.txt", "nothing secret here"));
        var record = Record(scope, 0);
        Assert.Equal("complete", (string?)record["redaction"]);
        Assert.Equal(19L, (long?)record["size"]);
    }

    // .NET-only: the port snapshots the redactor at attachment and builds artifact records when the attempt ends.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rescans_downloads_with_values_registered_after_attachment(bool alreadyHasSecret)
    {
        var scope = alreadyHasSecret ? Downloads(("api-key", Secret)) : Downloads();
        const string later = "later-credential-Jk8-5931";
        scope.AttachArtifact("download", Write("export.txt", $"key={later}\n"));
        Assert.Equal(alreadyHasSecret ? "complete" : "incomplete", (string?)Record(scope, 0)["redaction"]);
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
        var record = Record(scope, 0);
        const string redacted = "key=<secret:api-key>\n";
        Assert.Equal("complete", (string?)record["redaction"]);
        Assert.Equal(redacted, Read("export.txt"));
        Assert.Equal(Encoding.UTF8.GetByteCount(redacted), (long?)record["size"]);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(redacted))), (string?)record["sha256"]);
    }

    // .NET-only: a failed final scan must replace the earlier attachment's successful result.
    [Fact]
    public void Reports_a_previously_complete_download_incomplete_when_the_final_scan_fails()
    {
        var scope = Downloads(("api-key", Secret));
        scope.AttachArtifact("download", Write("export.txt", "nothing secret here"));
        Assert.Equal("complete", (string?)Record(scope, 0)["redaction"]);

        Write("export.txt", [0xff, 0xfe]);
        scope.Register("api-key", "later-credential-Jk8-5931");
        Assert.Equal("incomplete", (string?)Record(scope, 0)["redaction"]);
    }

    // .NET-only regression: upstream's ASCII spelling shortcut also misses these valid JSON encodings.
    [Theory]
    [InlineData("""{"key":"\u0068\u0075\u006e\u0074\u0065\u0072\u0032"}""", """{"key":"<secret:api-key>"}""")]
    [InlineData("""{"key":"\u0068\u0075\u006E\u0074\u0065\u0072\u0032"}""", """{"key":"<secret:api-key>"}""")]
    [InlineData("""{"key":"hunter\u0032"}""", """{"key":"<secret:api-key>"}""")]
    [InlineData("""{"text":"{\"key\":\"\\u0068\\u0075\\u006e\\u0074\\u0065\\u0072\\u0032\"}"}""", """{"text":"{\"key\":\"<secret:api-key>\"}"}""")]
    public void Redacts_JSON_Unicode_escaped_ASCII_secrets_before_reporting_complete(string body, string redacted)
    {
        var scope = Downloads(("api-key", "hunter2"));
        scope.AttachArtifact("download", Write("export.json", body));
        var record = Record(scope, 0);
        Assert.Equal("complete", (string?)record["redaction"]);
        Assert.Equal(redacted, Read("export.json"));
        Assert.Equal(Encoding.UTF8.GetByteCount(redacted), (long?)record["size"]);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(redacted))), (string?)record["sha256"]);
    }

    [Fact]
    public void Leaves_a_download_as_served_with_no_secret_value_and_a_binary_one_with_a_fill_both_incomplete()
    {
        var untainted = Downloads();
        var body = $"key={Secret}";
        untainted.AttachArtifact("download", Write("env.txt", body));
        Assert.Equal("incomplete", (string?)Record(untainted, 0)["redaction"]);
        Assert.Equal(body, Read("env.txt"));

        var tainted = Downloads(("api-key", Secret));
        byte[] binary = [0xff, 0xfe, 0x00, .. Encoding.UTF8.GetBytes(Secret)];
        tainted.AttachArtifact("download", Write("blob.bin", binary));
        tainted.AttachArtifact("download", Write("broken.txt", binary));
        var utf16 = Encoding.Unicode.GetBytes($"key={Secret}");
        tainted.AttachArtifact("download", Write("export.csv", utf16));
        Assert.Equal("incomplete", (string?)Record(tainted, 0)["redaction"]);
        Assert.Equal("incomplete", (string?)Record(tainted, 1)["redaction"]);
        Assert.Equal("incomplete", (string?)Record(tainted, 2)["redaction"]);
        Assert.Equal(binary, File.ReadAllBytes(Path.Combine(_root, "downloads", "blob.bin")));
        Assert.Equal(binary, File.ReadAllBytes(Path.Combine(_root, "downloads", "broken.txt")));
        Assert.Equal(utf16, File.ReadAllBytes(Path.Combine(_root, "downloads", "export.csv")));
    }
}
