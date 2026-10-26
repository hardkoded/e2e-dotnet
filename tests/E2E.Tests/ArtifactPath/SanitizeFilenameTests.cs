// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Tests.ArtifactPath;

/// <summary>
/// <c>describe('sanitizeFilename')</c> of upstream <c>artifact-path.test.ts</c>: artifact paths built from names the engine does not
/// choose, a caller's label or the filename a server suggests for a download.
/// </summary>
public sealed class SanitizeFilenameTests
{
    [Fact]
    public void Keeps_no_separator_drive_colon_or_control_character_bounds_the_length_and_names_an_empty_label()
    {
        foreach (var name in HostileNames.All)
        {
            var safe = WebSupport.SanitizeFilename(name);
            Assert.True(Regex.IsMatch(safe, @"^[A-Za-z0-9._-]{1,64}\z"), name);
        }

        Assert.Equal("artifact", WebSupport.SanitizeFilename(""));
    }
}

/// <summary>The names the artifact path tests feed: each must land as one plain file inside the attempt's folder.</summary>
internal static class HostileNames
{
    public static readonly string[] All =
    [
        "../../etc/passwd",
        "..",
        "/etc/cron.d/job",
        "..\\..\\Windows\\win.ini",
        "C:\\Windows\\System32\\drivers\\etc\\hosts",
        "report.pdf:hidden-stream",
        "a\u0000b.txt",
        "CON",
        "nul.txt",
        "LPT1",
        new string('x', 300) + ".zip",
    ];
}
