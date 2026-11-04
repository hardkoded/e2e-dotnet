// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Xml;
using System.Xml.Linq;

namespace E2E.Cli;

/// <summary>One example app <c>e2e init --example</c> can copy (upstream's <c>ExamplePreset</c>, whose project folder, test script, and build flag the samples do not need: each is a test project at its root, run with <c>dotnet test</c>).</summary>
/// <param name="Id">The folder name under <c>samples/</c>, which is also the flag value.</param>
/// <param name="Hint">What the example shows.</param>
internal sealed record ExamplePreset(string Id, string Hint);

/// <summary>What a copy wrote, so a cancelled or failed init can take it back (upstream's <c>ExampleCopy</c>).</summary>
/// <param name="Ref">The git ref the files came from: <c>v&lt;version&gt;</c> or <c>main</c>.</param>
/// <param name="Remove">Removes everything the copy wrote, and the directory when the copy created it.</param>
internal sealed record ExampleCopy(string Ref, Action Remove);

/// <summary>What <see cref="Examples.CopyExampleAsync"/> takes beside the example and the destination (upstream's <c>CopyOptions</c>).</summary>
internal sealed class CopyOptions
{
    /// <summary>Called when the CLI's release tag is missing and the copy falls back to <c>main</c>.</summary>
    public required Action<string> OnFallback { get; init; }

    /// <summary>Stops the download and removes the partial copy.</summary>
    public CancellationToken Signal { get; init; }

    /// <summary>Test seam (.NET-only): the handler that answers the download, in place of stubbing <c>fetch</c>.</summary>
    public HttpMessageHandler? Handler { get; init; }
}

/// <summary>The download or the unpacking failed (upstream's <c>ExampleDownloadError</c>).</summary>
internal sealed class ExampleDownloadError(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Thrown when the caller's signal stopped the copy, such as Ctrl-C; the partial copy is already removed (upstream's <c>ExampleCopyCancelled</c>).</summary>
internal sealed class ExampleCopyCancelled(CancellationToken token) : OperationCanceledException("the example download was cancelled", token);

/// <summary>
/// Upstream's <c>cli/init/examples.ts</c>: the <c>e2e init --example</c> step, which copies one of the repository's
/// <c>samples/</c> apps into the project directory. The samples are not in the NuGet packages; they come from the GitHub
/// tarball of the tag this tool was released under, or from <c>main</c> when that tag does not exist (a preview or a source
/// build). The samples reference the libraries by project, so the copy points them at the packages instead.
/// </summary>
internal static class Examples
{
    private const string Repository = "hardkoded/e2e-dotnet";

    /// <summary>
    /// How long the download may go without a byte before it fails. An idle limit, not a total one: the tarball is the whole
    /// repository, and a slow but moving connection must still finish.
    /// </summary>
    internal static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Entries a directory may hold and still count as empty for an example.</summary>
    private static readonly string[] IgnoredEntries = [".git", ".DS_Store", "Thumbs.db"];

    /// <summary>The properties the repository's <c>Directory.Build.props</c> sets for the samples, which a copy outside the repository does not inherit.</summary>
    private static readonly KeyValuePair<string, string>[] InheritedProperties =
    [
        new("TargetFramework", "net10.0"),
        new("LangVersion", "latest"),
        new("Nullable", "enable"),
        new("ImplicitUsings", "enable"),
    ];

    /// <summary>Every standalone test project under <c>samples/</c>.</summary>
    public static readonly IReadOnlyList<ExamplePreset> All =
    [
        new("E2E.Sample", "NUnit tests of the Dariten demo"),
        new("E2E.XUnit.V3.Sample", "xUnit v3 tests of the Dariten demo"),
        new("TodoMvc", "NUnit agent tests of TodoMVC"),
    ];

    /// <summary>The preset a flag value names: the folder name, in any case.</summary>
    public static ExamplePreset? FindExample(string name) =>
        All.FirstOrDefault(example => string.Equals(example.Id, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether <paramref name="dir"/> is missing or holds nothing an example could collide with.</summary>
    public static bool IsEmptyDirectory(string dir) =>
        !Directory.Exists(dir) || Directory.EnumerateFileSystemEntries(dir).All(entry => IgnoredEntries.Contains(Path.GetFileName(entry)));

    /// <summary>
    /// Downloads the tarball and writes <c>samples/&lt;id&gt;/</c> into <paramref name="destination"/>, which must be empty or
    /// missing. The project file points at the packages of this tool's version instead of the repository's projects (see
    /// <see cref="AdaptManifest"/>). On failure or cancellation the partial copy is removed before the error is thrown.
    /// </summary>
    /// <exception cref="ExampleDownloadError">The download or the archive failed.</exception>
    /// <exception cref="ExampleCopyCancelled"><paramref name="options"/> signal stopped the copy.</exception>
    public static async Task<ExampleCopy> CopyExampleAsync(ExamplePreset example, string destination, CopyOptions options)
    {
        ArgumentNullException.ThrowIfNull(example);
        ArgumentNullException.ThrowIfNull(options);
        var created = !Directory.Exists(destination);
        var written = new HashSet<string>(StringComparer.Ordinal);
        void Remove()
        {
            if (created)
            {
                if (Directory.Exists(destination))
                {
                    Directory.Delete(destination, recursive: true);
                }

                return;
            }

            foreach (var entry in written)
            {
                var path = Path.Combine(destination, entry);
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
                else
                {
                    File.Delete(path);
                }
            }
        }

        var version = Versions.RunnerVersion ?? "0.0.0";
        var tag = "v" + version;
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(options.Signal);
        idle.CancelAfter(IdleTimeout);
        using var http = options.Handler is null ? new HttpClient() : new HttpClient(options.Handler, disposeHandler: false);
        http.Timeout = Timeout.InfiniteTimeSpan;
        http.DefaultRequestHeaders.UserAgent.ParseAdd("E2E.Cli/" + version);
        var succeeded = false;
        try
        {
            var reference = tag;
            var response = await Download("refs/tags/" + tag, http, idle.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                response.Dispose();
                options.OnFallback(tag);
                reference = "main";
                response = await Download("refs/heads/main", http, idle.Token).ConfigureAwait(false);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new ExampleDownloadError($"GitHub answered {(int)response.StatusCode} for {Repository}@{reference}");
                }

                Directory.CreateDirectory(destination);
                await using var body = await response.Content.ReadAsStreamAsync(idle.Token).ConfigureAwait(false);
                await using var watched = new WatchedStream(body, idle, IdleTimeout);
                await using var gunzip = new GZipStream(watched, CompressionMode.Decompress);

                // Extraction stops reading once it passed the folder; the rest of the download is dropped.
                var files = await ExtractFolderAsync(gunzip, $"samples/{example.Id}/", destination, top => written.Add(top), idle.Token).ConfigureAwait(false);
                if (files == 0)
                {
                    throw new ExampleDownloadError($"{Repository}@{reference} has no samples/{example.Id}");
                }
            }

            AdaptManifest(destination);
            succeeded = true;
            return new ExampleCopy(reference, Remove);
        }
        catch (Exception cause) when (cause is not ExampleDownloadError and not OperationCanceledException and (HttpRequestException or IOException or InvalidDataException or XmlException))
        {
            throw new ExampleDownloadError(cause.Message, cause);
        }
        catch (OperationCanceledException) when (options.Signal.IsCancellationRequested)
        {
            throw new ExampleCopyCancelled(options.Signal);
        }
        catch (OperationCanceledException cause)
        {
            throw new ExampleDownloadError($"GitHub sent nothing for {IdleTimeout.TotalSeconds:0} s", cause);
        }
        finally
        {
            if (!succeeded)
            {
                try
                {
                    Remove();
                }
                catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
                {
                    // The copy failed first; a leftover file must not hide that error.
                }
            }
        }
    }

    /// <summary>Requests the repository tarball for one ref.</summary>
    private static Task<HttpResponseMessage> Download(string reference, HttpClient http, CancellationToken cancellationToken)
    {
        var encoded = string.Join('/', reference.Split('/').Select(Uri.EscapeDataString));
        return http.GetAsync(new Uri($"https://codeload.github.com/{Repository}/tar.gz/{encoded}"), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    /// <summary>
    /// Points the copied project at the packages of this tool's version: each <c>ProjectReference</c> into the repository's
    /// <c>src/</c> becomes a <c>PackageReference</c> to the project of that name, and the properties the repository's
    /// <c>Directory.Build.props</c> set for it are written into the project (upstream's <c>adaptManifest</c> moves its
    /// <c>e2e</c> and <c>@e2e-dev/*</c> pins to this CLI's ranges, and a sample's name does not change).
    /// </summary>
    private static void AdaptManifest(string projectDir)
    {
        var projects = Directory.GetFiles(projectDir, "*.csproj");
        if (projects.Length != 1)
        {
            throw new InvalidDataException($"The example has {projects.Length} project files at its root, not one.");
        }

        var original = File.ReadAllText(projects[0]);
        var document = XDocument.Parse(original, LoadOptions.PreserveWhitespace);
        var range = Versions.DependencyRange(Versions.RunnerVersion);
        foreach (var reference in document.Descendants().Where(element => element.Name.LocalName == "ProjectReference").ToList())
        {
            var include = ((string?)reference.Attribute("Include") ?? "").Replace('\\', '/');
            if (include.Contains("/src/", StringComparison.Ordinal) || include.StartsWith("src/", StringComparison.Ordinal))
            {
                reference.ReplaceWith(new XElement(reference.Name.Namespace + "PackageReference", new XAttribute("Include", Path.GetFileNameWithoutExtension(include)), new XAttribute("Version", range)));
            }
        }

        var properties = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "PropertyGroup");
        if (properties is not null)
        {
            foreach (var (name, value) in InheritedProperties.Where(entry => !document.Descendants().Any(element => element.Name.LocalName == entry.Key)))
            {
                var property = new XElement(properties.Name.Namespace + name, value);
                var last = properties.Elements().LastOrDefault();
                var spacing = last?.PreviousNode is XText { Value: var text } && string.IsNullOrWhiteSpace(text) ? text : "\n    ";
                if (last is null)
                {
                    properties.Add(new XText(spacing), property);
                }
                else
                {
                    last.AddAfterSelf(new XText(spacing), property);
                }
            }
        }

        File.WriteAllText(projects[0], document.Declaration is null ? document.Root!.ToString(SaveOptions.DisableFormatting) + "\n" : document.ToString(SaveOptions.DisableFormatting) + "\n");
    }

    /// <summary>
    /// Writes the regular files of a tar stream that sit under <c>&lt;root&gt;/&lt;folder&gt;</c> into
    /// <paramref name="destination"/>, without the leading part, and stops reading once the stream has moved past the folder
    /// (git archives list paths in order). Paths that would leave <paramref name="destination"/> are refused. Returns how many
    /// files were written. Upstream hand-parses ustar, pax, and GNU headers; <see cref="TarReader"/> reads them here.
    /// </summary>
    /// <exception cref="ExampleDownloadError">An entry's path leaves <paramref name="destination"/>.</exception>
    public static async Task<int> ExtractFolderAsync(Stream source, string folder, string destination, Action<string> onTopLevel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onTopLevel);
        var root = Path.GetFullPath(destination);
        var files = 0;
        var inside = false;
        await using var reader = new TarReader(source, leaveOpen: true);
        while (await reader.GetNextEntryAsync(copyData: false, cancellationToken).ConfigureAwait(false) is { } entry)
        {
            // Every entry of a git archive sits under one `<repo>-<ref>/` directory.
            var relative = entry.Name[(entry.Name.IndexOf('/', StringComparison.Ordinal) + 1)..];
            if (!relative.StartsWith(folder, StringComparison.Ordinal))
            {
                if (inside)
                {
                    break;
                }

                continue;
            }

            inside = true;
            var inner = relative[folder.Length..].TrimEnd('/');
            if (inner.Length == 0)
            {
                continue;
            }

            var segments = inner.Split('/');
            if (segments.Any(segment => segment is "" or "." or "..") || Path.IsPathRooted(inner) || inner.Contains('\\', StringComparison.Ordinal))
            {
                throw new ExampleDownloadError("the archive holds an unsafe path: " + entry.Name);
            }

            onTopLevel(segments[0]);
            var absolute = Path.Combine(root, Path.Combine(segments));
            if (entry.EntryType == TarEntryType.Directory)
            {
                Directory.CreateDirectory(absolute);
            }
            else if (entry.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile && entry.DataStream is { } data)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
                await using (var file = new FileStream(absolute, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await data.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
                }

                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(absolute, (entry.Mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0 ? (UnixFileMode)0b110_100_100 : (UnixFileMode)0b111_101_101);
                }

                files++;
            }
        }

        return files;
    }

    /// <summary>Restarts the idle limit on every read: .NET-only, in place of upstream's <c>watched</c> generator over the chunks.</summary>
    private sealed class WatchedStream(Stream inner, CancellationTokenSource idle, TimeSpan limit) : Stream
    {
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
            var read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            idle.CancelAfter(limit);
            return read;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            idle.CancelAfter(limit);
            return read;
        }

        public override ValueTask DisposeAsync() => inner.DisposeAsync();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
