// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace E2E;

/// <summary>
/// Files the agent can always attach to a file input, named <c>sample:&lt;name&gt;</c> (upstream <c>agent/sample-files.ts</c>). A project rarely
/// keeps files the model could know to name, so without these an exploration skips every upload flow it meets. They are e2e's own bytes,
/// written once per process to a temporary directory: naming one reads nothing of the project or the machine.
/// </summary>
internal static class SampleFiles
{
    /// <summary>What an upload path starts with to name a sample instead of a project file.</summary>
    public const string SamplePrefix = "sample:";

    // A 32 by 32 PNG of four colored squares.
    private const string Png =
        "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAIAAAD8GO2jAAAAOklEQVR42mO45+NAEnLruUMSYhi1YNSCUQtGLRi1YEhYELAlhST04YQNSWjUglELRi0YtWDUgiFhAQCR4HRb8WkrnwAAAABJRU5ErkJggg==";

    // The same picture as a baseline JPEG, without metadata.
    private const string Jpeg =
        "/9j/4AAQSkZJRgABAgAAAQABAAD/wAARCAAgACADAREAAhEBAxEB/9sAQwAFAwQEBAMFBAQEBQUFBgcMCAcHBwcPCwsJDBEPEhIRDxERExYcFxMUGhURERghGBodHR8fHxMXIiQiHiQcHh8e/9sAQwEFBQUHBgcOCAgOHhQRFB4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4e/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwDPr40/dgoA0K/oU/kYKAM+v56P65CgDQr+hT+RgoA1a/ByQoA9Yr8pP3cKAPJ6/Vj8ICgD1ivyk/dwoA//2Q==";

    /// <summary>A one-page PDF reading "e2e sample document", its cross-reference table counted from the objects.</summary>
    private static byte[] Pdf()
    {
        const string text = "BT /F1 24 Tf 72 720 Td (e2e sample document) Tj ET";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {text.Length} >>\nstream\n{text}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        ];
        var body = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>(objects.Length);
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(body.Length);
            body.Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var table = new StringBuilder();
        foreach (var offset in offsets)
        {
            table.Append($"{offset:D10} 00000 n \n");
        }

        var size = (objects.Length + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var start = body.Length;
        body.Append($"xref\n0 {size}\n0000000000 65535 f \n{table}trailer\n<< /Size {size} /Root 1 0 R >>\nstartxref\n{start}\n%%EOF\n");
        return Encoding.UTF8.GetBytes(body.ToString());
    }

    // Every sample's bytes by name, in the order upstream lists them.
    private static readonly (string Name, Func<byte[]> Bytes)[] Samples =
    [
        ("photo.png", () => Convert.FromBase64String(Png)),
        ("photo.jpg", () => Convert.FromBase64String(Jpeg)),
        ("document.pdf", Pdf),
        ("notes.txt", () => Encoding.UTF8.GetBytes("e2e sample text\nSecond line: café, 東京, 🙂\n")),
        ("table.csv", () => Encoding.UTF8.GetBytes("name,email,amount\nAda Lovelace,ada@example.test,42\n\"Hopper, Grace\",grace@example.test,7.5\nZoë Ångström,zoe@example.test,0\n")),
        ("empty.txt", () => []),
    ];

    /// <summary>The samples' names, as an upload names them after <see cref="SamplePrefix"/>.</summary>
    public static IReadOnlyList<string> SampleNames { get; } = [.. Samples.Select(sample => sample.Name)];

    private static readonly Lock Gate = new();

    /// <summary>The directory this process wrote the samples to, once one was asked for.</summary>
    private static string? _directory;

    /// <summary>
    /// Where the named sample is on disk, written on first use and removed when the process exits; null for a name that is no sample.
    /// </summary>
    public static string? SamplePath(string name)
    {
        var sample = Samples.FirstOrDefault(candidate => candidate.Name == name);
        if (sample.Name is null)
        {
            return null;
        }

        lock (Gate)
        {
            _directory ??= WriteSamples();
        }

        return Path.Combine(_directory, name);
    }

    private static string WriteSamples()
    {
        var created = Directory.CreateTempSubdirectory("e2e-samples-").FullName;
        // Registered before the first write, so a write that throws still leaves nothing behind for the next process to find.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => RemoveQuietly(created);
        try
        {
            foreach (var (file, bytes) in Samples)
            {
                File.WriteAllBytes(Path.Combine(created, file), bytes());
            }
        }
        catch
        {
            RemoveQuietly(created);
            throw;
        }

        return created;
    }

    // The samples are e2e's own copies; a file another process still holds, or a directory already gone, is left for the temp cleaner.
    private static void RemoveQuietly(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
