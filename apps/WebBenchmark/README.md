# WebBenchmark

Upstream's `apps/web-benchmark/app` (a Next.js app of self-contained scenarios that are hard to automate) rebuilt as an ASP.NET Core Blazor Web App. The home page lists every scenario; one opens at `/e/<slug>`. `ExamplesCatalog.cs` is upstream's `examples.ts`: every slug, name, description, and planted bug. A scenario's `Component` is null until it is ported: it is listed, and its page answers 404.

```bash
dotnet run --project apps/WebBenchmark        # http://127.0.0.1:4280, as upstream's `next start app --port 4280`
dotnet run --project apps/WebBenchmark --urls http://127.0.0.1:5000
```

Rules kept from upstream: the DOM a scenario exposes (test ids, texts, roles, styles) stays as upstream renders it, and a planted bug is never fixed.

Every scenario of upstream's `examples.ts` has a page: one Razor component per scenario in `Examples/`, `control-inventory` included. To add a scenario, create `Examples/YourCase.razor`, register it in `ExamplesCatalog.cs` (the doc comment there states the rules for task and bug-book entries), and write its tests in `apps/WebBenchmark.E2ETests`. The tests that drive the app are in [`apps/WebBenchmark.E2ETests`](../WebBenchmark.E2ETests).
