# Desktop sample: macOS Calculator

This sample drives the Calculator app that ships with macOS through `DesktopEngine` (`E2E.Desktop`), the port of upstream's `@e2e-dev/desktop`. It reads and drives the app through the macOS accessibility tree, so the tests need a Mac.

## Run it on a Mac

1. Grant the app that starts the tests (your terminal or IDE) **Accessibility** in **System Settings > Privacy & Security**, and **Screen Recording** for screenshots.
2. Check the setup:

```bash
dotnet run --project src/E2E.Cli -- doctor
```

3. Run the tests that need no model:

```bash
dotnet test samples/DesktopCalculator --filter "TestCategory!=RealModel"
```

4. Run the agent test, which needs a GitHub Copilot login:

```bash
dotnet run --project src/E2E.Cli -- login github-copilot
dotnet test samples/DesktopCalculator --filter "TestCategory=RealModel"
```

The tests are skipped on Windows and Linux. A run that includes the `macos` target stops with `UNSUPPORTED_CAPABILITY` there.

## What it shows

- `platform: macos` and `app.bundleId` in `e2e.config.json` name the app. `app.appPath` names a built `.app` bundle instead.
- `DesktopTest` gives the `Desktop` fixture: `WindowsAsync()`, `UseWindowAsync()`, `PressAsync()`, and `CloseAppAsync()`.
- `App.OpenAsync()` launches the app in the background. Return `new DesktopOptions { Headed = true }` from `CreateDesktopOptions()` for apps that need to come forward.

Calculator's button names depend on the macOS version. If a locator does not match, run `dotnet run --project src/E2E.Cli -- explore` or print the tree with an agent step, and adjust the names.
