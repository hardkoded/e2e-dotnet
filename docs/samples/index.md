---
title: Samples
---

# Samples

Every sample is a real project in the repository, built on the same packages you install. Copy the one closest to your app.

| Sample | Platform | Needs a model | What it shows |
| --- | --- | --- | --- |
| [Dariten](dariten.md) | Web | One of two tests | The smallest test: a locator test and an agent test against a live public app, for NUnit and xUnit v3. |
| [TodoMVC](todomvc.md) | Web | Yes | Playwright's TodoMVC suite rewritten as agent steps, with a side-by-side comparison. |
| [Mobile app](mobile-app.md) | iOS, Android, .NET MAUI | Some tests | A MAUI app, tests on a simulator or emulator, hosted devices, and DevFlow. |
| [WinApp benchmark](winapp-benchmark.md) | Windows | Some tests | A WinUI 3 app of hard scenarios, and the suites that drive it. |

## Run a sample

From a clone of the repository:

```bash
git clone https://github.com/hardkoded/e2e-dotnet
cd e2e-dotnet
dotnet tool install --global E2E.Cli
e2e login github-copilot            # only for the tests that call a model
dotnet test --project samples/E2E.Sample
```

Tests that call a model are in the `RealModel` category. Skip them with `--filter "TestCategory!=RealModel"`. The first run of an agent step asks the model. A passing run records the step in `.e2e/cache`, and the next run replays it with no model call.
