# Contributing

This repo is a community .NET port of [tester-army/e2e](https://github.com/tester-army/e2e). Prefer the upstream public behavior, described in [COMPATIBILITY.md](COMPATIBILITY.md), over a .NET-only invention. Record intentional differences in that file.

## Layout

| Path | Role |
| --- | --- |
| `src/E2E` | SDK: tests, expect, agent, cache, document engine, Playwright `WebEngine` |
| `src/E2E.Eas` | EAS hosted simulators and emulators for `MobileEngine`, packed as `E2E.Eas` |
| `src/E2E.Mobile` | Mobile engine: the snapshot model, locators, errors, the agent-device client, the device pool and providers, and the session over agent-device |
| `src/E2E.Kernel` | Kernel hosted browsers for `WebEngineOptions.Browser`, packed as `E2E.Kernel` |
| `src/E2E.Limrun` | Limrun hosted simulators and emulators for `MobileEngine`, packed as `E2E.Limrun` |
| `src/E2E.TestMu.Mobile` | TestMu AI real and virtual mobile devices for `MobileEngine`, driven through Appium, packed as `E2E.TestMu.Mobile`. No upstream counterpart |
| `src/E2E.Mobile.DevFlow` | .NET MAUI apps for `MobileEngine`, driven through the MAUI DevFlow agent inside the app, packed as `E2E.Mobile.DevFlow`. No upstream counterpart |
| `src/E2E.NUnit` | NUnit fixture `E2ETest` |
| `src/E2E.TestMu` | TestMu AI hosted browsers for `WebEngineOptions.Browser`, packed as `E2E.TestMu`. No upstream counterpart |
| `src/E2E.XUnit.V3` | xUnit v3 fixture `E2ETest` |
| `tests/E2E.Tests` | Unit tests. No API key. The Chromium tests install the browser on the first launch |
| `tests/E2E.Cli.Tests` | Tests of the `e2e` tool: the bundled skill, the MCP server, its sessions, and its tools. The server tests start `e2e mcp` as a child process, open a session on a fixture app in Chromium, and talk to it over stdio. No API key |
| `tests/E2E.Eas.Tests` | Unit tests of `src/E2E.Eas`, with a fake EAS service. No device, no API key. The tests that evaluate a dynamic app config run a stand-in `expo` with `node`, so they need Node on the path |
| `tests/E2E.Kernel.Tests` | Unit tests of `src/E2E.Kernel`, with a fake Kernel REST API. No browser, no API key. Ported from upstream's `packages/kernel/tests`, plus a few tests of the REST requests that are .NET-only |
| `tests/E2E.Limrun.Tests` | Unit tests of `src/E2E.Limrun`, with a fake Limrun API and a scripted device client. No device, no API key |
| `tests/E2E.TestMu.Mobile.Tests` | Unit tests of `src/E2E.TestMu.Mobile`, with a fake Appium hub. No device, no account, no key |
| `tests/E2E.Mobile.DevFlow.Tests` | Unit tests of `src/E2E.Mobile.DevFlow`, with a fake DevFlow agent and broker. No device, no app |
| `tests/E2E.TestMu.Tests` | Unit tests of `src/E2E.TestMu`: the endpoint, the capabilities, and the credentials. No API key. One live test attaches to the real grid and is skipped unless `LT_USERNAME` and `LT_ACCESS_KEY` are set. All are .NET-only |
| `tests/E2E.Mobile.Tests` | Unit tests of `src/E2E.Mobile`, with snapshots captured from a real iOS simulator and Android emulator, and a scripted agent-device client. No device, no API key |
| `tests/E2E.Agent.Tests` | Agent tests against a real model, ported from upstream's `apps/testbed/tests-agent`. They serve upstream's playground pages on a loopback port. All are in the `RealModel` category and need a GitHub Copilot login |
| `skills/e2e-dotnet` | The agent skill bundled in `E2E.Cli` and printed by `e2e guide` |
| `skills/create-verification-skill` | A generator skill for consumers that writes a project-local `verify-<app>` skill. Not bundled. Its example, `references/example/verify-todomvc`, names `samples/TodoMvc` tests and locators: update it when they change |
| `skills/migrate-to-e2e` | A skill for consumers that moves an existing suite to e2e one slice at a time. Not bundled. It reads the `e2e-dotnet` skill's topics: update it when a topic it names is renamed |
| `samples/E2E.Sample` | NUnit tests against the live Dariten demo. The agent test needs a GitHub Copilot login (`dotnet run --project src/E2E.Cli -- login github-copilot`) |
| `samples/E2E.XUnit.V3.Sample` | The `samples/E2E.Sample` tests, written for xUnit v3 |
| `samples/MobileApp` | A small .NET MAUI app (iOS and Android) for the mobile sample. It is not in `E2E.slnx`: building it needs the MAUI workload, and CI has none. Build it with `dotnet build samples/MobileApp -f net10.0-ios -r iossimulator-arm64` |
| `samples/MobileApp.E2ETests` | Tests that drive `samples/MobileApp` on a simulator or emulator with `MobileEngine`. All are `RealModel` and `Device`, and need a device and Node 22.12 or newer. Leave them out of a local real-model run with `--filter "TestCategory=RealModel&TestCategory!=Device"`. The project builds in `E2E.slnx` |

## Development

Requires the .NET 10 SDK (`global.json`).

```bash
dotnet restore
dotnet build
dotnet test
```

`WebEngine` installs the Chromium build that Playwright expects on its first launch in a process. So the Chromium tests in `tests/E2E.Tests` need network access the first time, and fail if the install fails. `E2E_SKIP_BROWSER_INSTALL=1` skips the install.

Style is enforced at build time through `.editorconfig` and `Directory.Build.props` (`EnforceCodeStyleInBuild`, `TreatWarningsAsErrors`). C# files use the Apache file header.

Do not commit API keys.

## Documentation

The site in `docs-site/` is [Astro Starlight](https://starlight.astro.build/). Guides for Get started and Samples are Markdown in `docs-site/src/content/docs/`. The Guide, Compatibility, and Contributing pages are this repository's `README.md`, `COMPATIBILITY.md`, and `CONTRIBUTING.md`, copied when the site starts or builds.

Preview:

```bash
cd docs-site
npm install
npm run dev
```

The dev server is at http://localhost:4321/e2e-dotnet/ . `npm run build` then `npm run preview` serves the production build, including search.

The API reference is generated from the XML docs of `E2E`, `E2E.NUnit`, and `E2E.XUnit.V3`. After a public API change, regenerate it and commit the pages:

```bash
cd docs-site
npm run api
```

That needs the .NET 10 SDK. It runs DocFX metadata, then writes Markdown into `docs-site/src/content/docs/api/`.

`.github/workflows/docs.yml` builds and checks the site on pull requests and on pushes to `main`. It does not deploy. GitHub Pages is retired. Vercel hosts https://www.hardkoded.com/e2e-dotnet/ and publishes it on push to `main`.

## Release

Push a version tag such as `v0.1.0` (previews use `v0.1.0-preview.1`). `.github/workflows/publish.yml` tests, packs `E2E`, `E2E.NUnit`, `E2E.XUnit.V3`, `E2E.Cli`, `E2E.Mobile`, `E2E.Mobile.DevFlow`, `E2E.Limrun`, `E2E.Eas`, `E2E.TestMu`, and `E2E.Kernel`, and pushes them to nuget.org with trusted publishing. The policy is repository owner `hardkoded`, repository `e2e-dotnet`, workflow file `publish.yml`. The workflow has to be on the default branch before the tag is pushed.

Docs are not published by the tag. Vercel publishes them on push to `main`. See [Documentation](#documentation).
