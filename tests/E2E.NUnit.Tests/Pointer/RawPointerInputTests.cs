// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;

namespace E2E.NUnit.Tests.Pointer;

/// <summary>
/// The raw mouse against the playground's <c>/pointer</c> pad, ported from upstream's
/// <c>apps/testbed/tests/pointer.e2e.ts</c> ("raw pointer input"). Not ported: the other five tests need
/// <c>screen.tapAt</c>, tap <c>position</c> or <c>screen.swipe</c>, which the port does not have.
/// </summary>
public sealed class RawPointerInputTests : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [Test]
    public async Task Raw_mouse_input_drags_along_the_pad()
    {
        await App.OpenAsync("/pointer");
        var box = await Screen.GetByRole("image", "Pointer pad").BoundingBoxAsync();
        Assert.That(box, Is.Not.Null);

        await Browser.Mouse.MoveAsync((float)(box!.X + 10), (float)(box.Y + 20));
        await Browser.Mouse.DownAsync();
        await Browser.Mouse.MoveAsync((float)(box.X + 150), (float)(box.Y + 20));
        await Browser.Mouse.UpAsync();
        await Expect.That(Screen.GetByLabel("Pad state")).ToHaveTextAsync("swiped from 10,20 to 150,20");
    }
}
