// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Formats.Tar;
using System.IO.Compression;
using System.Net;

namespace E2E.Cli.Tests.InitExamples;

/// <summary>One entry of a test archive (upstream's <c>Entry</c>).</summary>
internal sealed record ArchiveEntry(string Name, string Content = "", UnixFileMode Mode = (UnixFileMode)0b110_100_100, TarEntryType Type = TarEntryType.RegularFile, bool Gnu = false);

/// <summary>
/// .NET-only helpers standing for the top of upstream's <c>init-examples.test.ts</c>: <c>tar</c>, <c>paxRecord</c>,
/// <c>chunks</c>, and <c>viteTarball</c>. <see cref="TarWriter"/> writes the headers and the long names that upstream builds by hand.
/// </summary>
internal static class ExampleArchives
{
    /// <summary>A tar archive of the entries.</summary>
    public static byte[] Tar(IEnumerable<ArchiveEntry> entries)
    {
        using var memory = new MemoryStream();
        using (var writer = new TarWriter(memory, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                TarEntry written = entry.Type switch
                {
                    TarEntryType.GlobalExtendedAttributes => new PaxGlobalExtendedAttributesTarEntry([new KeyValuePair<string, string>("comment", entry.Content)]),
                    TarEntryType.SymbolicLink => new PaxTarEntry(TarEntryType.SymbolicLink, entry.Name) { LinkName = "elsewhere" },
                    TarEntryType.Directory => new PaxTarEntry(TarEntryType.Directory, entry.Name),
                    _ when entry.Gnu => new GnuTarEntry(entry.Type, entry.Name) { DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(entry.Content)) },
                    _ => new PaxTarEntry(entry.Type, entry.Name) { DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(entry.Content)) },
                };
                if (entry.Type != TarEntryType.GlobalExtendedAttributes)
                {
                    written.Mode = entry.Mode;
                }

                writer.WriteEntry(written);
            }
        }

        return memory.ToArray();
    }

    /// <summary>The archive gzipped, as a GitHub tarball is served.</summary>
    public static byte[] Gzip(byte[] archive)
    {
        using var memory = new MemoryStream();
        using (var gzip = new GZipStream(memory, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(archive);
        }

        return memory.ToArray();
    }

    /// <summary>A GitHub tarball of the <c>E2E.Sample</c> example as the codeload service serves it.</summary>
    public static byte[] SampleTarball(string root = "e2e-dotnet-1.0.0") => Gzip(Tar(
    [
        new ArchiveEntry("pax_global_header", "0123456789abcdef0123456789abcdef01234567", Type: TarEntryType.GlobalExtendedAttributes),
        new ArchiveEntry(root + "/", Type: TarEntryType.Directory),
        new ArchiveEntry(root + "/src/readme.md", "not an example"),
        new ArchiveEntry(root + "/samples/", Type: TarEntryType.Directory),
        new ArchiveEntry(root + "/samples/E2E.Sample/", Type: TarEntryType.Directory),
        new ArchiveEntry(
            root + "/samples/E2E.Sample/E2E.Sample.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <RootNamespace>E2E.Sample</RootNamespace>\n    <IsTestProject>true</IsTestProject>\n  </PropertyGroup>\n  <ItemGroup>\n    <PackageReference Include=\"NUnit\" Version=\"5.0.0\" />\n  </ItemGroup>\n  <ItemGroup>\n    <ProjectReference Include=\"..\\..\\src\\E2E\\E2E.csproj\" />\n    <ProjectReference Include=\"..\\..\\src\\E2E.NUnit\\E2E.NUnit.csproj\" />\n  </ItemGroup>\n</Project>\n"),
        new ArchiveEntry(root + "/samples/E2E.Sample/e2e.config.json", "{}\n"),
        new ArchiveEntry(root + "/samples/E2E.Sample/.gitignore", "bin/\n"),
        new ArchiveEntry(root + "/samples/E2E.Sample/DaritenTests.cs", "// tests\n"),
        new ArchiveEntry(root + "/samples/E2E.XUnit.V3.Sample/never.txt", "past the folder"),
    ]));

    /// <summary>A handler that answers every request from a function and records the URLs asked (upstream's <c>vi.stubGlobal('fetch', ...)</c>).</summary>
    public sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];

        public static StubHandler Serving(byte[] body) => new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) }));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri!.ToString());
            return respond(request, cancellationToken);
        }
    }

    /// <summary>A stream that hands out at most <paramref name="size"/> bytes a read, as a network body arrives (upstream's <c>chunks</c>); it cannot seek.</summary>
    public sealed class ChunkedStream(byte[] data, int size = 700) : Stream
    {
        private int _position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = Math.Min(Math.Min(count, size), data.Length - _position);
            Array.Copy(data, _position, buffer, offset, read);
            _position += read;
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>A response body that sends half the archive, then stalls until the request is cancelled, as a real connection body does.</summary>
    public sealed class StallingStream(byte[] data) : Stream
    {
        private bool _sent;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_sent)
            {
                _sent = true;
                var read = Math.Min(buffer.Length, data.Length / 2);
                data.AsMemory(0, read).CopyTo(buffer);
                return read;
            }

            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
