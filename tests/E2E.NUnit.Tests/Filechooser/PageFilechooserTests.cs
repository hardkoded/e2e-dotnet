// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.Filechooser;

/// <summary>
/// <c>test.describe('page filechooser')</c> of upstream's <c>apps/testbed/tests/filechooser.e2e.ts</c> (Playwright's
/// <c>page-filechooser.spec.ts</c>). A file chooser is answered by <c>SetInputFilesAsync</c> on the control whose click opens it, so the
/// page's own <c>click()</c> on the input is a button that does it. Not ported, as upstream: should emit event once, should emit event via
/// prepend, should emit event for iframe, should emit event on/off, should emit event addListener/removeListener, should work when file input
/// is attached to DOM, should not throw when filechooser belongs to iframe, should not throw when frame is detached immediately, should
/// respect timeout, should respect default timeout when there is no custom timeout, should prioritize exact timeout over default timeout,
/// should work with no timeout, should abort with signal, should return the same file chooser when there are many watchdogs simultaneously,
/// should work for single file pick, should work for "multiple", should work for "webkitdirectory", should trigger listener added before
/// navigation (no <c>filechooser</c> events and no <c>FileChooser</c> objects); should not trim big uploaded files (no file chooser; ported in
/// <c>SetInputFiles/PageSetInputFilesTests</c>).
/// </summary>
public sealed class PageFilechooserTests : PlaywrightTest
{
    /// <summary>A button whose click clicks the page's file input, as <c>picker.click()</c> does in Playwright's tests.</summary>
    private const string Picker = "<input type=file><button onclick=\"document.querySelector('input').click()\">pick</button>";

    /// <summary>Resolves, in the page, with the first picked file's text once the input fires <c>input</c>.</summary>
    private const string ReadPicked = """
        () => {
          const picker = document.querySelector('input');
          window.picked = new Promise((x) => { picker.oninput = x; }).then(() => {
            const reader = new FileReader();
            const promise = new Promise((fulfill) => { reader.onload = fulfill; });
            reader.readAsText(picker.files[0]);
            return promise.then(() => reader.result);
          });
        }
        """;

    [Test]
    public async Task Should_upload_multiple_large_files()
    {
        const int filesCount = 10;
        await Browser.GotoAsync("/input/fileupload-multi.html");
        var dir = Directory.CreateTempSubdirectory("e2e-chooser-").FullName;
        try
        {
            var uploadFile = Path.Combine(dir, "50MB_1.zip");
            var chunk = new byte[1024 * 1024];
            Array.Fill(chunk, (byte)'A');
            await using (var stream = File.Create(uploadFile))
            {
                for (var i = 0; i < 49; i++)
                {
                    await stream.WriteAsync(chunk);
                }
            }

            var uploadFiles = new List<string> { uploadFile };
            for (var i = 2; i <= filesCount; i++)
            {
                var dstFile = Path.Combine(dir, $"50MB_{i}.zip");
                File.Copy(uploadFile, dstFile);
                uploadFiles.Add(dstFile);
            }

            await Browser.EvaluateAsync<object>("() => { const button = document.createElement('button'); button.textContent = 'pick'; button.type = 'button'; button.onclick = () => document.querySelector('input[type=\"file\"]').click(); document.body.append(button); }");
            await Browser.Locator("button").SetInputFilesAsync(uploadFiles);
            var filesLen = await Browser.EvaluateAsync<int>("document.getElementsByTagName(\"input\")[0].files.length");
            Assert.That(filesLen, Is.EqualTo(filesCount));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Should_work_when_file_input_is_not_attached_to_DOM()
    {
        await Browser.GotoAsync(Content("""
            <button onclick="window.picked = (async () => {
              const el = document.createElement('input');
              el.type = 'file';
              el.click();
              await new Promise(x => el.oninput = x);
              const reader = new FileReader();
              const promise = new Promise(fulfill => reader.onload = fulfill);
              reader.readAsText(el.files[0]);
              return promise.then(() => reader.result);
            })()">pick</button>
            """));
        await Browser.Locator("button").SetInputFilesAsync(Asset("file-to-upload.txt"));
        Assert.That(await Browser.EvaluateAsync<string>("() => window.picked"), Is.EqualTo("contents of the file"));
    }

    [Test]
    public async Task Should_accept_single_file()
    {
        await Browser.GotoAsync(Content("<input type=file oninput='javascript:console.timeStamp()'><button onclick=\"document.querySelector('input').click()\">pick</button>"));
        await Browser.Locator("button").SetInputFilesAsync(Asset("file-to-upload.txt"));
        Assert.That(await Browser.EvaluateAsync<int>("() => document.querySelector('input').files.length"), Is.EqualTo(1));
        Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('input').files[0].name"), Is.EqualTo("file-to-upload.txt"));
    }

    [Test]
    public async Task Should_be_able_to_read_selected_file()
    {
        await Browser.GotoAsync(Content(Picker));
        await Browser.EvaluateAsync<object>(ReadPicked);
        await Browser.Locator("button").SetInputFilesAsync(Asset("file-to-upload.txt"));
        Assert.That(await Browser.EvaluateAsync<string>("() => window.picked"), Is.EqualTo("contents of the file"));
    }

    // Upstream: { skip: '`setInputFiles([])` is refused, so there is no way to clear a file input (refused as INVALID_ARGUMENT, which `fail` does not cover)' }.
    [Test]
    [Ignore("`SetInputFilesAsync([])` is refused, so there is no way to clear a file input (refused as INVALID_ARGUMENT, which a known difference does not cover)")]
    public async Task Should_be_able_to_reset_selected_files_with_empty_file_list()
    {
        await Browser.GotoAsync(Content(Picker));
        const string watchLength = "() => { const picker = document.querySelector('input'); window.picked = new Promise((x) => { picker.oninput = x; }).then(() => picker.files.length); }";
        await Browser.EvaluateAsync<object>(watchLength);
        await Browser.Locator("button").SetInputFilesAsync(Asset("file-to-upload.txt"));
        Assert.That(await Browser.EvaluateAsync<int>("() => window.picked"), Is.EqualTo(1));
        await Browser.EvaluateAsync<object>(watchLength);
        await Browser.Locator("button").SetInputFilesAsync([]);
        Assert.That(await Browser.EvaluateAsync<int>("() => window.picked"), Is.EqualTo(0));
    }

    [Test]
    public async Task Should_emit_event_after_navigation()
    {
        await Browser.GotoAsync($"{PREFIX}/empty.html");
        await Browser.GotoAsync(Content(Picker));
        await Browser.Locator("button").SetInputFilesAsync(Asset("file-to-upload.txt"));
        Assert.That(await Browser.EvaluateAsync<int>("() => document.querySelector('input').files.length"), Is.EqualTo(1));
        await Browser.GotoAsync($"{CROSS}/empty.html");
        await Browser.GotoAsync($"{CROSS}{Content(Picker)}");
        await Browser.Locator("button").SetInputFilesAsync(Asset("file-to-upload.txt"));
        Assert.That(await Browser.EvaluateAsync<int>("() => document.querySelector('input').files.length"), Is.EqualTo(1));
    }
}
