---
title: Web
---

# Get started: web

`WebEngine` drives Chromium through [Microsoft.Playwright](https://playwright.dev/dotnet/). The first launch in a process installs the headless shell, so the first run needs network access. Set `E2E_SKIP_BROWSER_INSTALL=1` when the browser is already installed.

## 1. Create the test project

```bash
dotnet new nunit -n MyApp.E2ETests
cd MyApp.E2ETests
dotnet add package E2E
dotnet add package E2E.NUnit          # or E2E.XUnit.V3 for xUnit v3
```

Add `E2E` to the implicit usings, so tests need no `using E2E;`:

```xml
<ItemGroup>
  <Using Include="E2E" />
  <Using Include="NUnit.Framework" />
</ItemGroup>
```

## 2. Point at your app

```json
{
  "targets": [{ "name": "web", "platform": "web", "app": { "url": "http://localhost:3000" } }],
  "actionTimeout": 15000,
  "assertionTimeout": 10000,
  "agents": {
    "default": {
      "provider": "copilot",
      "model": "claude-sonnet-5.5",
      "context": "MyApp is a project tracker. Never delete data."
    }
  },
  "cache": { "dir": ".e2e/cache" }
}
```

Copy the file to the output folder:

```xml
<ItemGroup>
  <None Update="e2e.config.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

Start your app at that address before the run. `App.OpenAsync("/path")` opens a path on `app.url`.

## 3. Write a test

Derive from `E2ETest`. It starts a `WebEngine` session for each test.

```csharp
using E2E.NUnit;

public sealed class SignupTests : E2ETest
{
    [Test]
    public async Task The_home_page_opens()
    {
        await App.OpenAsync("/");
        await Expect.That(Screen.GetByRole("heading", "Welcome")).ToBeVisibleAsync();
    }

    [Test]
    [Category("RealModel")]
    public async Task A_visitor_signs_up_for_a_trial()
    {
        await App.OpenAsync("/");

        await Agent.ActAsync("sign up for a free trial as {name} with email {email}", new ActOptions
        {
            Params = new Dictionary<string, object?>
            {
                ["name"] = "Ada Lovelace",
                ["email"] = "ada@example.test",
            },
        });

        await Agent.AssertAsync("the welcome screen greets Ada by name");
        await Expect.That(Screen.GetByRole("status")).ToContainTextAsync("trial");
    }
}
```

- Give `ActAsync` one atomic goal per call. `Params` fills the placeholders.
- `AssertAsync` asks the model to judge the screen. `Expect.That(...)` checks an exact value with no model call, so pair each agent step with one.
- Tag tests that call a model, for example `[Category("RealModel")]`, so CI can skip them with `--filter "TestCategory!=RealModel"`.

## 4. Run

```bash
dotnet test
E2E_HEADLESS=0 dotnet test      # watch the browser
```

The first run asks the model to perform each `ActAsync`. A passing run records each verified step in `.e2e/cache`, and the next run replays it with no model call.

## Next

- [Dariten sample](../samples/dariten.md) and [TodoMVC sample](../samples/todomvc.md): web tests you can run today.
- [Hosted browsers, CDP, video, and trace](../../README.md#hosted-browsers) in the guide.
