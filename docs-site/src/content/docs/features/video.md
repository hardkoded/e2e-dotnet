---
title: Video and screenshots
description: Record an attempt, and compare the screen to a stored PNG.
sidebar:
  label: "Video & screenshots"
---

## Video

With `video` on, an attempt on the web engine writes `video/video.webm` under `.e2e/results/<result>/attempt-<n>/`. A screencast belongs to one page, so each later page the attempt opens (after `App.RestartAsync`, `App.ClearStateAsync`, or a reconnect) adds a segment, `video/video-part<n>.webm`, and each segment has the instant it began, `StartedAt`. `retain-on-failure` records every attempt and deletes the video of one that passed. A browser provider that records (Kernel's `replay.mp4`) records in place of the screencast, and writes its own file, or gives a link and no local file. The `video.webm` files are the screencast's. `MobileEngine` on a local simulator or emulator records the whole device screen to `video/video.mp4` with agent-device, with the taps shown unless `MobileOptions.VideoTouches` is `false`; a hosted device provider's own recording is not used yet. `new WebEngineOptions { Screencast = new WebScreencastOptions { Size = new WebViewport(960, 540), Quality = 70 } }` sets the frame size and the JPEG quality of the screencast; the size defaults to the page's viewport. A mode set on the target or on a test is required: an engine that cannot record it (`DocumentEngine`, the MAUI DevFlow engine) fails the session start with `UNSUPPORTED_ARTIFACT`. A mode from the config root or `E2E_VIDEO` skips such an engine. The retry modes (`on-first-retry`, `on-all-retries`) need NUnit `[Retry]`; xUnit v3 has no retry, so they record nothing there. A video masks nothing and keeps what the screen showed, secrets included.

## Visual comparison

`ToHaveScreenshotAsync` compares what the app shows against a PNG stored next to the test file. It works on the web engine only; `DocumentEngine` fails with `UNSUPPORTED_CAPABILITY`.

```csharp
await Expect.That(Screen).ToHaveScreenshotAsync("checkout.png");
await Expect.That(Screen.GetByRole("button", "Pay")).ToHaveScreenshotAsync("pay-button.png");
await Expect.That(Screen).ToHaveScreenshotAsync("feed.png", new ScreenshotOptions
{
    Mask = [Screen.GetByTestId("timestamp")],
    MaxDiffPixels = 50,
});
```

`Expect.That(Screen)` compares the whole viewport. `Expect.That(locator)` compares the box of its one match, scrolled into view first. Without a name the screenshot is named after the test, `my-test-title-1`.

- **Stored at** `<test file>-snapshots/<name>-<target>-<os>.png`, for example `CheckoutTests.cs-snapshots/checkout-web-linux.png`. The test file is the file the call is written in. The OS is `darwin`, `linux`, or `win32`. Commit the files.
- **Missing.** The first run writes the screenshot and fails. Look at it, commit it, and run again. In CI (`CI` set) nothing is written into the project: the screenshot is attached under `.e2e/results/` at `snapshots/<path in the project>`, so it can be downloaded and committed.
- **Update.** `E2E_UPDATE_SNAPSHOTS=1 dotnet test` writes each missing or different screenshot and passes. A fixture can also override `UpdateSnapshots`. Screenshots are per OS, so write CI's from a CI job.
- **Different.** The matcher captures again until one matches or the assertion timeout passes, then fails with the number of differing pixels and three images under `.e2e/results/<result>/attempt-<n>/screenshots/`: `-diff`, `-actual`, and `-expected`.
- **Options.** `Threshold` (0 to 1, default 0.2), `MaxDiffPixels`, `MaxDiffPixelRatio` (the smaller wins when both are set), `Mask`, `MaskColor` (`#rrggbb`, default `#ff00ff`), and `Timeout`. `.Not` passes once the screen differs from a stored screenshot.
- **Secrets.** Secure fields are covered in every screenshot. After a test fills a `Secret`, the matcher still compares. It masks secure fields only, so a secret the app shows anywhere else is in the image.
