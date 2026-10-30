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

    private static string Create()
    {
        var directory = Directory.CreateTempSubdirectory("e2e-upload-");
        var file = Path.Combine(directory.FullName, "avatar.png");
        System.IO.File.WriteAllText(file, "png");
        return file;
    }
}
