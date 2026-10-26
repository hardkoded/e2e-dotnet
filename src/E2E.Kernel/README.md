# E2E.Kernel

[Kernel](https://kernel.sh) hosted browsers for [`E2E`](https://www.nuget.org/packages/E2E), part of the community .NET port of the [e2e](https://github.com/tester-army/e2e) agentic testing framework. It ports `@e2e-dev/kernel`. Not an official TesterArmy product.

```
dotnet add package E2E.Kernel
```

`KernelProvider` runs a web target in Kernel's hosted Chromium. By default each worker slot gets its own browser, created when the run starts and deleted when it ends. `Scope = BrowserProviderScope.Attempt` leases one per test attempt instead.

```csharp
using E2E.Engine;
using E2E.Kernel;

var options = new WebEngineOptions
{
    Browser = new KernelProvider(new KernelOptions { Stealth = true }),
    Viewport = null,
};
```

Set `KERNEL_API_KEY` in the environment that starts the tests, such as a CI secret, and `KERNEL_BASE_URL` to reach another Kernel API. Each worker gets its own browser, so the worker count sets how many run at once.

`KernelOptions` takes Kernel's create-browser body: `Stealth`, `Headless`, `Gpu`, `TimeoutSeconds`, `Profile`, `ProxyId`, `Viewport`, and `Tags`. Any other field of the body (`proxy`, `extensions`, `region`, ...) goes in `Extra` by its wire name. On top of that:

| Option | Default | Meaning |
| --- | --- | --- |
| `Scope` | `Worker` | `Worker`: one browser per worker slot for the run. `Attempt`: a fresh browser per test attempt, retries included. An attempt browser rules out `Headers`, `BasicAuth`, and `UserAgent`. |
| `Replay` | `true` | Whether an attempt that records video gets a Kernel replay of the browser's screen, saved as `video/replay.mp4`. `false`: the web engine's screencast of the page. |
| `ReplayParams` | none | Kernel's start-replay body (`Framerate`, `MaxDurationInSeconds`, `RecordAudio`). |

Size the screen with Kernel's `Viewport` and set the web engine's `Viewport = null`, so the page takes the window's size.

What the provider does:

- Tags every browser `e2e_run`, `e2e_target`, `e2e_slot`, and, per attempt, `e2e_attempt`, merged over your `Tags`.
- Deletes, when the run ends, every browser still active with the run's `e2e_run` and `e2e_target` tags.
- Defaults `TimeoutSeconds` to 600, the backstop for a run that dies before it can delete its browsers.
- Logs `browser <id>, watch at <live view URL>` as each browser is created.
- Deletes a browser that arrives after its request was cancelled, and treats one Kernel no longer knows as already deleted.
- Saves downloads under `/tmp/e2e-downloads` on the browser's own disk and reads a finished file back through Kernel's browser filesystem API. `Browser.WaitForDownloadAsync` reads a download through it.
- Kernel replays headed browsers only. With `Headless = true`, or `Replay = false`, the provider does not record. An attempt that records video starts the replay and saves it as `video/replay.mp4`; a browser that is not replayed gets the engine's screencast of the page.

There is no Kernel .NET SDK, so the provider calls Kernel's REST API with `HttpClient`. The wire format was read from the `@onkernel/sdk` source and has not been checked against a live Kernel account.

See the [repository](https://github.com/hardkoded/e2e-dotnet) and its `COMPATIBILITY.md` for what differs from upstream.
