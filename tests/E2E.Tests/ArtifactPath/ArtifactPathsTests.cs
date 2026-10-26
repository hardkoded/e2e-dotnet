// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Tests.ArtifactPath;

/// <summary><c>describe('artifact paths')</c> of upstream <c>artifact-path.test.ts</c>.</summary>
[Collection(BrowserCollection.Name)]
public sealed class ArtifactPathsTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("e2e-artifact-path-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task Writes_a_server_chosen_download_name_as_one_counted_file_inside_the_attempt_downloads_folder()
    {
        var session = await new WebEngine(headless: true).StartAsync(new EngineStartOptions { ArtifactsDir = _directory }, CancellationToken.None);
        try
        {
            var surface = WebEngine.SurfaceOf(session)!;
            var folder = Path.Combine(_directory, "downloads");
            for (var index = 0; index < HostileNames.All.Length; index++)
            {
                var name = HostileNames.All[index];
                var (relative, absolute) = surface.ArtifactPath("downloads", name, "");
                var file = Path.GetFileName(absolute);
                Assert.Equal($"downloads/{file}", relative);
                Assert.Equal(folder, Path.GetDirectoryName(absolute));
                Assert.True(Regex.IsMatch(file, $"^{(index + 1).ToString("D3", CultureInfo.InvariantCulture)}-[A-Za-z0-9._-]{{1,64}}\\z"), name);
            }
        }
        finally
        {
            await session.DisposeAsync();
        }
    }
}
