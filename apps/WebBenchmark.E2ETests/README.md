# WebBenchmark tests

The tests that drive [`apps/WebBenchmark`](../WebBenchmark) with the web engine. They port upstream's `apps/web-benchmark/tests`, laid out as upstream lays them out: each upstream test file is a directory, each `describe` a class, each `it` a test.

| Directory | Upstream | What it runs |
| --- | --- | --- |
| `Smoke` | `smoke.e2e.ts` | The home page lists the scenarios and opens one. No model. |

The tests start the app in process on a free loopback port (`Fixtures/BenchmarkTest.cs`). Upstream's `e2e.config.ts` starts `pnpm run start` through `app.command`; the NUnit adapter does not start `app.command`. They need a Playwright browser, like every web test of the repository, and no model login.

```bash
dotnet test apps/WebBenchmark.E2ETests
```

## Not ported yet

Tracked by #1149; each follows in a later pull request.

- The other `tests/*.e2e.ts` suites, `support.ts`, and `canvas.ts`, with the scenarios they drive.
- `tests-agent/`: `login-form`, `planted-bugs`, `scenarios`, and `unique-cache` (real-model tests; `planted-bugs` and `scenarios` replay committed cache recordings).
