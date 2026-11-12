// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Internal;

namespace E2E;

/// <summary>Atomic report persistence (upstream's <c>report/write.ts</c>). Upstream's <c>fileReporter</c> has no counterpart: the port has no built-in file reporters; a <see cref="E2E.Reports.Reporter"/> is the extension point.</summary>
internal static class WriteReport
{
    // JSON.stringify(document, null, 2): two-space indent, LF line ends whatever the host, and no \u escapes for HTML characters and
    // non-ASCII text in the basic plane (an emoji is still escaped). The names and the null omission are the port's JSON defaults.
    private static readonly JsonSerializerOptions WireOptions = new(JsonDefaults.Options)
    {
        NewLine = "\n",
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Atomically writes one text document, creating the parent directory when it is missing (upstream's <c>writeTextReport</c>).</summary>
    public static async Task WriteTextReportAsync(string filePath, string content, CancellationToken cancellationToken = default)
    {
        if (Path.GetDirectoryName(filePath) is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
        }

        await AtomicWrite.WriteFileAtomicAsync(filePath, content, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Atomically writes a wire JSON document with two-space indent and a trailing newline (upstream's <c>writeJsonReport</c>). It writes the
    /// document as it is: the caller redacts the text in it.
    /// </summary>
    public static Task WriteJsonReportAsync(string filePath, object? document, CancellationToken cancellationToken = default) =>
        WriteTextReportAsync(filePath, JsonSerializer.Serialize(document, WireOptions) + "\n", cancellationToken);

    /// <summary>A path as the report keeps them: POSIX separators whatever the host (upstream's <c>toPosixPath</c>).</summary>
    public static string ToPosixPath(string relative) => relative.Replace(Path.DirectorySeparatorChar, '/');
}
