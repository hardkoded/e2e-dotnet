// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.SetInputFiles;

/// <summary>
/// <c>test.describe('page set input files')</c> of upstream's <c>apps/testbed/tests/set-input-files.e2e.ts</c> (Playwright's
/// <c>page-set-input-files.spec.ts</c>). Not ported, as upstream: should set from memory (<c>SetInputFilesAsync</c> takes file paths, not
/// <c>{ name, mimeType, buffer }</c> payloads); input event.composed should be true and cross shadow dom boundary (sets such a payload).
/// </summary>
public sealed class PageSetInputFilesTests : PlaywrightTest
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly List<string> _outputDirs = [];

    private static string EmptyPage => $"{PREFIX}/empty.html";

    /// <summary>Reads the first file of the input <paramref name="selector"/> matches as text, in the page.</summary>
    private static string ReadFirstFile(string selector) => $$"""
        () => {
          const reader = new FileReader();
          const promise = new Promise((fulfill) => { reader.onload = fulfill; });
          reader.readAsText(document.querySelector({{JsonSerializer.Serialize(selector)}}).files[0]);
          return promise.then(() => reader.result);
        }
        """;

    /// <summary>Records the input and change events the input <paramref name="selector"/> matches dispatches, into <c>window.events</c>.</summary>
    private static string RecordEvents(string selector) => $$"""
        () => {
          window.events = [];
          const e = document.querySelector({{JsonSerializer.Serialize(selector)}});
          e.addEventListener('input', () => window.events.push('input'));
          e.addEventListener('change', () => window.events.push('change'));
        }
        """;

    [TearDown]
    public void RemoveOutputDirs()
    {
        foreach (var dir in _outputDirs)
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        _outputDirs.Clear();
    }

    /// <summary>A fresh directory for files a test writes, removed after the test.</summary>
    private string OutputDir()
    {
        var dir = Directory.CreateTempSubdirectory("e2e-upload-").FullName;
        _outputDirs.Add(dir);
        return dir;
    }

    /// <summary>Writes a 200MB file of <c>A</c>s, Playwright's large upload.</summary>
    private static async Task WriteLargeFileAsync(string file)
    {
        var chunk = new byte[1024 * 1024];
        Array.Fill(chunk, (byte)'A');
        await using var stream = File.Create(file);
        for (var i = 0; i < 200; i++)
        {
            await stream.WriteAsync(chunk);
        }
    }

    /// <summary>The files the server parsed from the form the page posted to <c>/upload</c>.</summary>
    private async Task<List<UploadedFile>> UploadedAsync() =>
        JsonSerializer.Deserialize<List<UploadedFile>>((await Browser.EvaluateAsync<string>("document.body.textContent"))!, Json)!;

    [Test]
    public async Task Should_upload_the_file()
    {
        await Browser.GotoAsync("/input/fileupload.html");
        await Browser.Locator("input[type=file]").SetInputFilesAsync(Asset("file-to-upload.txt"));
        Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('input').files[0].name"), Is.EqualTo("file-to-upload.txt"));
        Assert.That(await Browser.EvaluateAsync<string>(ReadFirstFile("input")), Is.EqualTo("contents of the file"));
    }

    [Test]
    public async Task Should_upload_a_folder()
    {
        await Browser.GotoAsync("/input/folderupload.html");
        var dir = Path.Combine(OutputDir(), "file-upload-test");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "file1.txt"), "file1 content");
        await File.WriteAllTextAsync(Path.Combine(dir, "file2"), "file2 content");
        Directory.CreateDirectory(Path.Combine(dir, "sub-dir"));
        await File.WriteAllTextAsync(Path.Combine(dir, "sub-dir", "really.txt"), "sub-dir file content");
        await Browser.Locator("input[type=file]").SetInputFilesAsync(dir);
        var webkitRelativePaths = (await Browser.EvaluateAsync<string[]>("() => [...document.querySelector('input').files].map((f) => f.webkitRelativePath)"))!;
        Assert.That(webkitRelativePaths, Is.EquivalentTo(new[]
        {
            "file-upload-test/sub-dir/really.txt",
            "file-upload-test/file1.txt",
            "file-upload-test/file2",
        }));
        for (var i = 0; i < webkitRelativePaths.Length; i++)
        {
            var fileContent = await Browser.EvaluateAsync<string>("""
                (i) => {
                  const reader = new FileReader();
                  const promise = new Promise((fulfill) => { reader.onload = fulfill; });
                  reader.readAsText(document.querySelector('input').files[i]);
                  return promise.then(() => reader.result);
                }
                """, i);
            Assert.That(fileContent, Is.EqualTo(await File.ReadAllTextAsync(Path.Combine(dir, "..", webkitRelativePaths[i]))));
        }
    }

    [Test]
    public async Task Should_upload_a_folder_and_throw_for_multiple_directories()
    {
        await Browser.GotoAsync("/input/folderupload.html");
        var dir = Path.Combine(OutputDir(), "file-upload-test");
        Directory.CreateDirectory(Path.Combine(dir, "folder1"));
        await File.WriteAllTextAsync(Path.Combine(dir, "folder1", "file1.txt"), "file1 content");
        Directory.CreateDirectory(Path.Combine(dir, "folder2"));
        await File.WriteAllTextAsync(Path.Combine(dir, "folder2", "file2.txt"), "file2 content");
        var error = await Assert.CatchAsync(() => Browser.Locator("input[type=file]").SetInputFilesAsync([Path.Combine(dir, "folder1"), Path.Combine(dir, "folder2")]));
        Assert.That(error!.Message, Does.Contain("Multiple directories are not supported"));
    }

    [Test]
    public async Task Should_throw_if_a_directory_and_files_are_passed()
    {
        await Browser.GotoAsync("/input/folderupload.html");
        var dir = Path.Combine(OutputDir(), "file-upload-test");
        Directory.CreateDirectory(Path.Combine(dir, "folder1"));
        await File.WriteAllTextAsync(Path.Combine(dir, "folder1", "file1.txt"), "file1 content");
        var error = await Assert.CatchAsync(() => Browser.Locator("input[type=file]").SetInputFilesAsync([Path.Combine(dir, "folder1"), Path.Combine(dir, "folder1", "file1.txt")]));
        Assert.That(error!.Message, Does.Contain("File paths must be all files or a single directory"));
    }

    [Test]
    public async Task Should_throw_when_uploading_a_folder_in_a_normal_file_upload_input()
    {
        await Browser.GotoAsync("/input/fileupload.html");
        var dir = Path.Combine(OutputDir(), "file-upload-test");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "file1.txt"), "file1 content");
        var error = await Assert.CatchAsync(() => Browser.Locator("input[type=file]").SetInputFilesAsync(dir));
        Assert.That(error!.Message, Does.Contain("File input does not support directories, pass individual files instead"));
    }

    [Test]
    public async Task Should_throw_when_uploading_a_file_in_a_directory_upload_input()
    {
        await Browser.GotoAsync("/input/folderupload.html");
        var error = await Assert.CatchAsync(() => Browser.Locator("input[type=file]").SetInputFilesAsync(Asset("file to upload.txt")));
        Assert.That(error!.Message, Does.Contain("[webkitdirectory] input requires passing a path to a directory"));
    }

    [Test]
    public async Task Should_upload_a_file_after_popup()
    {
        await Browser.GotoAsync("/input/fileupload.html");
        await Browser.EvaluateAsync<object>("() => { window['__popup'] = window.open('about:blank'); }");
        await Browser.EvaluateAsync<object>("() => { window['__popup'].close(); }");
        await Browser.Locator("input[type=file]").SetInputFilesAsync(Asset("file-to-upload.txt"));
        Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('input').files[0].name"), Is.EqualTo("file-to-upload.txt"));
    }

    [Test]
    public async Task Should_upload_large_file()
    {
        await Browser.GotoAsync("/input/fileupload.html");
        var uploadFile = Path.Combine(OutputDir(), "200MB.zip");
        await WriteLargeFileAsync(uploadFile);
        await Browser.EvaluateAsync<object>(RecordEvents("input[type=\"file\"]"));
        await Browser.Locator("input[type=\"file\"]").SetInputFilesAsync(uploadFile);
        Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('input[type=\"file\"]').files[0].name"), Is.EqualTo("200MB.zip"));
        Assert.That(await Browser.EvaluateAsync<string[]>("() => window.events"), Is.EqualTo(new[] { "input", "change" }));
        await Task.WhenAll(
            Browser.WaitForURLAsync("/upload"),
            Browser.Locator("input[type=submit]").TapAsync());
        var file1 = (await UploadedAsync())[0];
        Assert.That(file1.Filename, Is.EqualTo("200MB.zip"));
        Assert.That(file1.Size, Is.EqualTo(200 * 1024 * 1024));
        File.Delete(uploadFile);
    }

    [Test]
    public async Task Should_throw_an_error_if_the_file_does_not_exist()
    {
        await Browser.GotoAsync("/input/fileupload.html");
        var error = await Assert.CatchAsync(() => Browser.Locator("input[type=file]").SetInputFilesAsync("i actually do not exist.txt"));
        // Node's "ENOENT: no such file or directory" is .NET's FileNotFoundException message here.
        Assert.That(error!.Message, Does.Contain("Could not find file"));
        Assert.That(error.Message, Does.Contain("i actually do not exist.txt"));
    }

    [Test]
    public async Task Should_upload_large_file_with_relative_path()
    {
        await Browser.GotoAsync("/input/fileupload.html");
        var uploadFile = Path.Combine(OutputDir(), "200MB.zip");
        await WriteLargeFileAsync(uploadFile);
        await Browser.EvaluateAsync<object>(RecordEvents("input[type=\"file\"]"));
        var relativeUploadPath = Path.GetRelativePath(ProjectRoot, uploadFile);
        Assert.That(Path.IsPathRooted(relativeUploadPath), Is.False);
        await Browser.Locator("input[type=\"file\"]").SetInputFilesAsync(relativeUploadPath);
        Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('input[type=\"file\"]').files[0].name"), Is.EqualTo("200MB.zip"));
        Assert.That(await Browser.EvaluateAsync<string[]>("() => window.events"), Is.EqualTo(new[] { "input", "change" }));
        await Task.WhenAll(
            Browser.WaitForURLAsync("/upload"),
            Browser.Locator("input[type=submit]").TapAsync());
        var file1 = (await UploadedAsync())[0];
        Assert.That(file1.Filename, Is.EqualTo("200MB.zip"));
        Assert.That(file1.Size, Is.EqualTo(200 * 1024 * 1024));
        File.Delete(uploadFile);
    }

    [Test]
    public async Task Should_upload_the_file_with_spaces_in_name()
    {
        await Browser.GotoAsync("/input/fileupload.html");
        await Browser.Locator("input[type=file]").SetInputFilesAsync(Asset("file to upload.txt"));
        Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('input').files[0].name"), Is.EqualTo("file to upload.txt"));
        Assert.That(await Browser.EvaluateAsync<string>(ReadFirstFile("input")), Is.EqualTo("contents of the file"));
    }

    [Test]
    public async Task Should_work_smoke()
    {
        await Browser.GotoAsync(Content("<input type=file>"));
        await Browser.Locator("input[type=file]").SetInputFilesAsync(Asset("file-to-upload.txt"));
        Assert.That(await Browser.EvaluateAsync<int>("() => document.querySelector('input').files.length"), Is.EqualTo(1));
        Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('input').files[0].name"), Is.EqualTo("file-to-upload.txt"));
    }

    [Test]
    public async Task Should_work_with_CSP()
    {
        var headers = Uri.EscapeDataString("""{"content-type":"text/html","content-security-policy":"default-src \"none\""}""");
        await Browser.GotoAsync($"/respond?headers={headers}&body={Uri.EscapeDataString("<input type=file>")}");
        await Browser.Locator("input[type=file]").SetInputFilesAsync(Asset("file-to-upload.txt"));
        Assert.That(await Browser.EvaluateAsync<int>("() => document.querySelector('input').files.length"), Is.EqualTo(1));
        Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('input').files[0].name"), Is.EqualTo("file-to-upload.txt"));
    }

    [Test]
    public async Task Should_detect_mime_type()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.GotoAsync(Content("""
            <form action="/upload" method="post" enctype="multipart/form-data" >
              <input type="file" name="file1">
              <input type="file" name="file2">
              <input type="submit" value="Submit">
            </form>
            """));
        await Browser.Locator("input[name=file1]").SetInputFilesAsync(Asset("file-to-upload.txt"));
        await Browser.Locator("input[name=file2]").SetInputFilesAsync(Asset("pptr.png"));
        await Task.WhenAll(
            Browser.WaitForURLAsync("/upload"),
            Browser.Locator("input[type=submit]").TapAsync());
        var files = await UploadedAsync();
        var (file1, file2) = (files[0], files[1]);
        Assert.That(file1.Filename, Is.EqualTo("file-to-upload.txt"));
        Assert.That(file1.Mimetype, Is.EqualTo("text/plain"));
        Assert.That(Convert.FromBase64String(file1.Base64!), Is.EqualTo(await File.ReadAllBytesAsync(Asset("file-to-upload.txt"))));
        Assert.That(file2.Filename, Is.EqualTo("pptr.png"));
        Assert.That(file2.Mimetype, Is.EqualTo("image/png"));
        Assert.That(Convert.FromBase64String(file2.Base64!), Is.EqualTo(await File.ReadAllBytesAsync(Asset("pptr.png"))));
    }

    [Test]
    public async Task Should_not_trim_big_uploaded_files()
    {
        await Browser.GotoAsync(EmptyPage);
        const int dataSize = 1 << 20;
        var files = await Browser.EvaluateAsync<List<UploadedFile>>("""
            async (size) => {
              const body = new FormData();
              body.set('file', new Blob([new Uint8Array(size)]));
              return (await fetch('/upload', { method: 'POST', body })).json();
            }
            """, dataSize);
        Assert.That(files![0].Size, Is.EqualTo(dataSize));
    }

    [Test]
    public async Task Should_emit_input_and_change_events()
    {
        await Browser.GotoAsync(Content("""
            <input id=input type=file></input>
            <script>
              window.events = [];
              input.addEventListener('input', e => window.events.push({ type: e.type }));
              input.addEventListener('change', e => window.events.push({ type: e.type }));
            </script>
            """));
        await Browser.Locator("input[type=file]").SetInputFilesAsync(Asset("file-to-upload.txt"));
        var events = (await Browser.EvaluateAsync<List<EventRecord>>("() => window.events"))!;
        Assert.That(events, Has.Count.EqualTo(2));
        Assert.That(events[0].Type, Is.EqualTo("input"));
        Assert.That(events[1].Type, Is.EqualTo("change"));
    }

    [Test]
    public async Task Input_should_trigger_events_when_files_changed_second_time()
    {
        await Browser.GotoAsync(Content("<input type=file multiple=true/>"));
        await Browser.EvaluateAsync<object>(RecordEvents("input"));

        await Browser.Locator("input[type=file]").SetInputFilesAsync(Asset("file-to-upload.txt"));
        Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('input').files[0].name"), Is.EqualTo("file-to-upload.txt"));
        Assert.That(await Browser.EvaluateAsync<string[]>("() => window.events"), Is.EqualTo(new[] { "input", "change" }));

        await Browser.EvaluateAsync<object>("() => { window.events.length = 0; }");

        await Browser.Locator("input[type=file]").SetInputFilesAsync(Asset("pptr.png"));
        Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('input').files[0].name"), Is.EqualTo("pptr.png"));
        Assert.That(await Browser.EvaluateAsync<string[]>("() => window.events"), Is.EqualTo(new[] { "input", "change" }));
    }

    [Test]
    public async Task Should_preserve_lastModified_timestamp()
    {
        await Browser.GotoAsync(Content("<input type=file multiple=true/>"));
        string[] files = ["file-to-upload.txt", "file-to-upload-2.txt"];
        await Browser.Locator("input[type=file]").SetInputFilesAsync(files.Select(Asset));
        Assert.That(await Browser.EvaluateAsync<string[]>("() => [...document.querySelector('input').files].map((f) => f.name)"), Is.EqualTo(files));
        var timestamps = (await Browser.EvaluateAsync<double[]>("() => [...document.querySelector('input').files].map((f) => f.lastModified)"))!;
        var expectedTimestamps = files.Select(file => new DateTimeOffset(File.GetLastWriteTimeUtc(Asset(file))).ToUnixTimeMilliseconds()).ToArray();
        for (var i = 0; i < timestamps.Length; i++)
        {
            Assert.That(Math.Abs(timestamps[i] - expectedTimestamps[i]), Is.LessThanOrEqualTo(1000));
        }
    }

    private sealed record UploadedFile(string Name, string? Filename, string? Mimetype, long Size, string? Base64);

    private sealed record EventRecord(string Type);
}
