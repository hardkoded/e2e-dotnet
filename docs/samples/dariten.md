---
title: Dariten
---

# Dariten sample

A web test of [Dariten](https://dariten.vercel.app), a public personal finance demo. The data is shared and anyone can edit it, so the tests only read the app and never check specific balances.

- Source: [`samples/E2E.Sample`](https://github.com/hardkoded/e2e-dotnet/tree/main/samples/E2E.Sample) (NUnit) and [`samples/E2E.XUnit.V3.Sample`](https://github.com/hardkoded/e2e-dotnet/tree/main/samples/E2E.XUnit.V3.Sample) (xUnit v3).
- Platform: web, `WebEngine`.

## The tests

`The_dashboard_links_to_the_transaction_register` uses locators only and needs no model:

```csharp
await App.OpenAsync("/");
await Expect.That(Screen.GetByRole("heading", "Dashboard")).ToBeVisibleAsync();
await Expect.That(Screen.GetByTestId("demo-banner")).ToContainTextAsync("Shared demo");

await Screen.GetByRole("link", "Transactions").ClickAsync();
await Expect.That(Screen.GetByRole("heading", "Transactions")).ToBeVisibleAsync();
```

`An_agent_filters_the_register_by_category` hands the goal to the agent, then judges the result. It is in the `RealModel` category:

```csharp
await App.OpenAsync("/transactions");
await Agent.ActAsync("filter the register to show only the Groceries category");
await Agent.AssertAsync("every transaction listed is in the Groceries category, or the register says no transactions match");
await Expect.That(Screen.GetByRole("heading", "Transactions")).ToBeVisibleAsync();
```

## Run it

```bash
dotnet test --project samples/E2E.Sample                       # NUnit
dotnet test --project samples/E2E.XUnit.V3.Sample              # xUnit v3
dotnet test --project samples/E2E.Sample --filter "TestCategory!=RealModel"   # no model
E2E_HEADLESS=0 dotnet test --project samples/E2E.Sample        # watch the browser
```

The config's `agents.default.context` tells the agent the demo is shared and it must never add, edit, or delete data. Use that pattern for any app with shared state.

To use an API key, change `agents.default` in `e2e.config.json`, for example to `"provider": "openai", "model": "gpt-4.1-mini"` with `OPENAI_API_KEY` set.

Start with [Get started: web](../get-started/web.md).
