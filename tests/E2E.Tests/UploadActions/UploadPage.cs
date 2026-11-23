// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.UploadActions;

/// <summary>The fixture file and the pages the upload action tests share (upstream's <c>upload-actions.test.ts</c> helpers).</summary>
internal static class UploadPage
{
    // Upstream writes avatar.png in a temp directory before the tests.
    public static readonly string AvatarFile = Create();

    /// <summary>The page's record of the file the input holds and the clicks it saw.</summary>
    public const string State = """
        () => Array.from(document.querySelector('#avatar').files ?? [], (picked) => picked.name).join(', ') + '|' + window.clicks
        """;

    /// <summary>Starts recording the <c>input</c> and <c>change</c> events of the file input in <c>window.events</c>.</summary>
    public const string ListenForInputAndChange = """
        () => {
            window.events = [];
            const input = document.querySelector('#avatar');
            input.addEventListener('input', () => window.events.push('input'));
            input.addEventListener('change', () => window.events.push('change'));
            return '';
        }
        """;

    /// <summary>The in-memory files the metadata test sets: one binary, one empty (upstream's <c>files</c>).</summary>
    public static IReadOnlyList<FilePayload> Payloads { get; } =
    [new("résumé.bin", "application/octet-stream", [0, 128, 255]), new("empty.txt", "text/plain", [])];

    /// <summary>What the page holds after <see cref="Payloads"/> are set: each file's name, type, and bytes.</summary>
    public const string PayloadsHeld = """
        async () => JSON.stringify(await Promise.all(Array.from(document.querySelector('#avatar').files ?? [], async (picked) => ({
            name: picked.name, mimeType: picked.type, bytes: Array.from(new Uint8Array(await picked.arrayBuffer())),
        }))))
        """;

    /// <summary>What <see cref="PayloadsHeld"/> reports for <see cref="Payloads"/>.</summary>
    public const string PayloadsExpected =
        """[{"name":"résumé.bin","mimeType":"application/octet-stream","bytes":[0,128,255]},{"name":"empty.txt","mimeType":"text/plain","bytes":[]}]""";

    /// <summary>The pages of the in-memory metadata test: a file input, a label for a hidden one, and a button that opens its chooser.</summary>
    public static string MetadataPage(string kind) =>
        kind switch
        {
            "input" => """<input id="avatar" type="file" multiple aria-label="Avatar">""",
            "label" =>
                """<label id="pick" for="avatar"><span data-testid="pick">Choose</span></label><input id="avatar" type="file" multiple hidden>""",
            _ =>
                """<button id="pick" onclick="document.getElementById('avatar').click()">Choose</button><input id="avatar" type="file" multiple hidden>""",
        };

    /// <summary>The message every missing-path failure starts with (upstream's <c>setInputFiles path does not exist ...</c>).</summary>
    public const string MissingPathMessage =
        "setInputFiles path does not exist or is under a directory the runner cannot search: ";

    /// <summary>The paths the missing-path tests hand over: one that does not exist, and one under a file.</summary>
    public static string[] MissingPaths() =>
        [
            Path.Combine(Path.GetDirectoryName(AvatarFile)!, "missing.png"),
            Path.Combine(AvatarFile, "inside-a-file.png"),
        ];

    /// <summary>The runtime's own message for a path that cannot be stat'ed, which the engine reports as is (upstream's <c>statFailure</c>).</summary>
    public static string StatFailure(string target)
    {
        try
        {
            System.IO.File.GetAttributes(target);
        }
        catch (Exception cause)
        {
            return cause.Message;
        }

        throw new InvalidOperationException(target + " exists");
    }

    /// <summary>Makes a directory the runner cannot search, holding a file; false where permission bits do not apply (Windows, or root).</summary>
    public static bool TryLockDirectory(string name, out string locked, out string hidden)
    {
        locked = Path.Combine(Path.GetDirectoryName(AvatarFile)!, name);
        hidden = Path.Combine(locked, "avatar.png");
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
        {
            return false;
        }

        Directory.CreateDirectory(locked);
        System.IO.File.WriteAllText(hidden, "png");
        if (!OperatingSystem.IsWindows())
        {
            System.IO.File.SetUnixFileMode(locked, UnixFileMode.None);
        }

        return true;
    }

    /// <summary>Lets the runner search <paramref name="locked"/> again, so the fixture can be cleaned up.</summary>
    public static void UnlockDirectory(string locked)
    {
        if (!OperatingSystem.IsWindows())
        {
            System.IO.File.SetUnixFileMode(
                locked,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );
        }
    }

    private static string Create()
    {
        var directory = Directory.CreateTempSubdirectory("e2e-upload-");
        var file = Path.Combine(directory.FullName, "avatar.png");
        System.IO.File.WriteAllText(file, "png");
        return file;
    }
}
