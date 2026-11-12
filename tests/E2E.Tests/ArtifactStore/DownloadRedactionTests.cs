// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
using E2E.Internal;
using E2E.Reports;

namespace E2E.Tests.ArtifactStore;

/// <summary><c>describe('download redaction')</c> of upstream <c>artifact-store.test.ts</c>.</summary>
public sealed class DownloadRedactionTests : ArtifactStoreHarness
{
    private const string Secret = "download-secret-Qx7-2718";

    private static Redactor LedgerOf(string name, string value) => Redactor.For(new (string Name, string Value)[] { (name, value) });

    private AttemptArtifacts Downloads(bool withLedger, IArtifactStore? store = null)
    {
        var artifacts = ArtifactPaths.CreateAttemptArtifacts(new AttemptArtifactsOptions
        {
            ArtifactsRoot = Root(),
            Segments = ["web", "test-1", "attempt-0"],
            AttemptId = "att-1",
            Store = store,
            Ledger = withLedger ? () => LedgerOf("api-key", Secret) : null,
        });
        Directory.CreateDirectory(Path.Combine(artifacts.Dir, "downloads"));
        return artifacts;
    }

    private static string PathOf(AttemptArtifacts artifacts, string name) => Path.Combine(artifacts.Dir, "downloads", name);

    private static string ReadText(AttemptArtifacts artifacts, string name) => File.ReadAllText(PathOf(artifacts, name), new UTF8Encoding(false));

    [Fact]
    public async Task Labels_a_download_incomplete_by_default_bytes_the_app_served_not_rewritten()
    {
        var store = Capturing();
        var artifacts = Downloads(false, store);
        var body = $"id,key\n1,{Secret}\n";
        File.WriteAllText(PathOf(artifacts, "export.csv"), body);
        artifacts.Sink.Register(ArtifactKind.Download, "downloads/export.csv");
        await artifacts.SettleAsync();

        var record = artifacts.Records[0];
        Assert.Equal(ArtifactKind.Download, record.Kind);
        Assert.Equal("text/csv", record.MediaType);
        Assert.Equal(ArtifactRedaction.Incomplete, record.Redaction);
        var put = store.Puts[0];
        Assert.Equal(ArtifactKind.Download, put.Kind);
        Assert.Equal(ArtifactRedaction.Incomplete, put.Redaction);
        Assert.Equal(body, Encoding.UTF8.GetString(put.Bytes.Span));
    }

    [Fact]
    public async Task Rewrites_a_text_download_through_the_ledger_a_value_can_reach_the_app_without_a_fill()
    {
        var artifacts = Downloads(true);
        File.WriteAllText(PathOf(artifacts, "headers.txt"), $"authorization: {Secret}\n");
        artifacts.Sink.Register(ArtifactKind.Download, "downloads/headers.txt");
        await artifacts.SettleAsync();

        Assert.Equal(ArtifactRedaction.Complete, artifacts.Records[0].Redaction);
        Assert.Equal("authorization: <secret:api-key>\n", ReadText(artifacts, "headers.txt"));
    }

    [Fact]
    public async Task Labels_a_rewritten_download_complete_and_stores_the_rewritten_bytes()
    {
        var store = Capturing();
        var artifacts = Downloads(true, store);
        File.WriteAllText(PathOf(artifacts, "export.json"), $"{{\"key\":\"{Secret}\"}}");
        artifacts.Sink.Register(ArtifactKind.Download, "downloads/export.json");
        await artifacts.SettleAsync();

        const string redacted = "{\"key\":\"<secret:api-key>\"}";
        var record = artifacts.Records[0];
        Assert.Equal("application/json", record.MediaType);
        Assert.Equal(ArtifactRedaction.Complete, record.Redaction);
        Assert.Equal(redacted, ReadText(artifacts, "export.json"));
        var put = store.Puts[0];
        Assert.Equal(ArtifactRedaction.Complete, put.Redaction);
        Assert.Equal(Encoding.UTF8.GetByteCount(redacted), put.Size);
        Assert.Equal(redacted, Encoding.UTF8.GetString(put.Bytes.Span));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(redacted))), put.Sha256);
    }

    [Fact]
    public async Task Rewrites_fragments_and_encoded_forms_in_a_download_the_way_trace_text_entries_are()
    {
        var artifacts = Downloads(true);
        var cut = Secret.Substring(6, 14);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(Secret));
        File.WriteAllText(PathOf(artifacts, "export.txt"), $"cut={cut}\nencoded={encoded}\n");
        artifacts.Sink.Register(ArtifactKind.Download, "downloads/export.txt");
        await artifacts.SettleAsync();

        Assert.Equal(ArtifactRedaction.Complete, artifacts.Records[0].Redaction);
        Assert.Equal("cut=<secret:api-key>\nencoded=<secret:api-key>\n", ReadText(artifacts, "export.txt"));
    }

    [Fact]
    public async Task Rewrites_a_value_CSV_quoted_its_double_quotes_doubled()
    {
        var artifacts = ArtifactPaths.CreateAttemptArtifacts(new AttemptArtifactsOptions
        {
            ArtifactsRoot = Root(),
            Segments = ["web", "test-1", "attempt-0"],
            AttemptId = "att-1",
            Ledger = () => LedgerOf("api-key", "pa\"ss,word"),
        });
        Directory.CreateDirectory(Path.Combine(artifacts.Dir, "downloads"));
        File.WriteAllText(PathOf(artifacts, "export.csv"), "id,key\n1,\"pa\"\"ss,word\"\n");
        artifacts.Sink.Register(ArtifactKind.Download, "downloads/export.csv");
        await artifacts.SettleAsync();

        Assert.Equal(ArtifactRedaction.Complete, artifacts.Records[0].Redaction);
        Assert.Equal("id,key\n1,\"<secret:api-key>\"\n", ReadText(artifacts, "export.csv"));
    }

    [Fact]
    public async Task Keeps_a_byte_order_mark_on_a_rewritten_download()
    {
        var store = Capturing();
        var artifacts = Downloads(true, store);
        byte[] bom = [0xef, 0xbb, 0xbf];
        File.WriteAllBytes(PathOf(artifacts, "export.csv"), [.. bom, .. Encoding.UTF8.GetBytes($"key\n{Secret}\n")]);
        artifacts.Sink.Register(ArtifactKind.Download, "downloads/export.csv");
        await artifacts.SettleAsync();

        var rewritten = File.ReadAllBytes(PathOf(artifacts, "export.csv"));
        Assert.True(rewritten.AsSpan(0, 3).SequenceEqual(bom));
        Assert.Equal("key\n<secret:api-key>\n", Encoding.UTF8.GetString(rewritten, 3, rewritten.Length - 3));
        Assert.Equal(ArtifactRedaction.Complete, artifacts.Records[0].Redaction);
        Assert.Equal(rewritten.Length, artifacts.Records[0].Size);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(rewritten)), store.Puts[0].Sha256);
        Assert.True(store.Puts[0].Bytes.Span.SequenceEqual(rewritten));
    }

    [Fact]
    public async Task Labels_a_scanned_text_download_complete_when_it_held_nothing_to_redact()
    {
        var artifacts = Downloads(true);
        File.WriteAllText(PathOf(artifacts, "notes.txt"), "nothing secret here");
        artifacts.Sink.Register(ArtifactKind.Download, "downloads/notes.txt");
        await artifacts.SettleAsync();

        Assert.Equal(ArtifactRedaction.Complete, artifacts.Records[0].Redaction);
        Assert.Equal(19, artifacts.Records[0].Size);
    }

    [Fact]
    public async Task Leaves_a_download_as_served_with_no_secret_value_and_a_binary_one_with_a_value_both_incomplete()
    {
        var empty = ArtifactPaths.CreateAttemptArtifacts(new AttemptArtifactsOptions
        {
            ArtifactsRoot = Root(),
            Segments = ["web", "test-1", "attempt-0"],
            AttemptId = "att-1",
            Ledger = () => Redactor.None,
        });
        Directory.CreateDirectory(Path.Combine(empty.Dir, "downloads"));
        var body = $"key={Secret}";
        File.WriteAllText(PathOf(empty, "env.txt"), body);
        empty.Sink.Register(ArtifactKind.Download, "downloads/env.txt");
        await empty.SettleAsync();

        Assert.Equal(ArtifactRedaction.Incomplete, empty.Records[0].Redaction);
        Assert.Equal(body, ReadText(empty, "env.txt"));

        var known = Downloads(true);
        byte[] binary = [0xff, 0xfe, 0x00, .. Encoding.UTF8.GetBytes(Secret)];
        File.WriteAllBytes(PathOf(known, "blob.bin"), binary);
        File.WriteAllBytes(PathOf(known, "broken.txt"), binary);
        known.Sink.Register(ArtifactKind.Download, "downloads/blob.bin");
        known.Sink.Register(ArtifactKind.Download, "downloads/broken.txt");
        await known.SettleAsync();

        Assert.Equal([ArtifactRedaction.Incomplete, ArtifactRedaction.Incomplete], known.Records.Select(record => record.Redaction));
        Assert.Equal(binary, File.ReadAllBytes(PathOf(known, "blob.bin")));
        Assert.Equal(binary, File.ReadAllBytes(PathOf(known, "broken.txt")));
    }
}
