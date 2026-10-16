# e2e-dotnet

This repo is a .NET port of [tester-army/e2e](https://github.com/tester-army/e2e). See CONTRIBUTING.md and COMPATIBILITY.md.

## Porting an upstream change

- Port every test that the upstream commit adds or changes. Keep its name, steps, and assertions.
- A test that needs a real model or a testbed page is still a test to port. Port the page too. Run the test as a real-model test (`[Category("RealModel")]`, like `samples/TodoMvc`).
- When the port adds or changes public API or user-visible behavior, update `skills/e2e-dotnet/SKILL.md` and `docs/` in the same PR. Mirror what upstream's `skills/e2e/references/*.md` and `docs/**/*.mdx` say about that member, in the port's C# names.
- Skip an upstream test only when the port has no such feature at all (for example, tracing). Name each skipped test and the reason in the PR report.
- Lay out ported tests like upstream:
  - Each upstream test file is a directory, named in PascalCase. `protected-app-options.test.ts` becomes `ProtectedAppOptions/`.
  - Each `describe` in that file is one test class file inside the directory. `describe('web({ locale, timezoneId })')` becomes `ProtectedAppOptions/WebLocaleTimezoneIdTests.cs`.
  - Each `it` is one test method, named after the `it` text.
  - A class holds every `it` of its upstream `describe`, not only the ones the commit adds.
  - Tests outside any `describe` go in `<Directory>/<Directory>Tests.cs`.
  - The directory sits in the test project that runs the test, and the namespace follows it: `E2E.Tests.ProtectedAppOptions`.
- In the test projects under `tests/`, every test ports an upstream `it`, except as the next rule allows. If an upstream `it` covers the behavior, even in part, port that `it` instead of writing your own test.
- .NET-only code may keep tests that have no upstream `it`, when no upstream `it` covers that behavior. Examples: model providers, the SigV4 signer, launch timeouts, adapters. Name each such test in the PR report.
- The sample tests under `samples/` (the TodoMvc and Dariten samples) have no upstream `it`. They are required. Do not remove them.
- A ported test checks everything its upstream `it` checks. If a part needs a feature the port does not have, name it in the PR report. Do not keep an upstream name on a test that checks only part of it.
- A port follows upstream's structure and names. Every new type, method, field, constant, or file in `src/` must map to an upstream function, class, or constant (in C# casing). It lives in the equivalent file, with the same owner, and keeps upstream's split of responsibilities.
  - Before you add a symbol, find its upstream counterpart. Read tester-army/e2e with `gh api`. Reuse its name and place.
  - A name upstream does not have is allowed only for a .NET-only concern (dependency injection, nullable handling, test fixtures, and the .NET-only code listed above, such as model providers and the SigV4 signer). An `Async` suffix is fine. The PR report names each such symbol and the reason.
  - Example: PR #249 first added `PerformTargetedAsync`. Upstream has `commitTargeted` calling a private `targeted` in `agent/action-dispatcher.ts`, and `feed.relocate` on the observation feed. The port uses those.
- Extract each upstream function that upstream's tests call directly (for example `replayTrace` and `ReplayHost`). Put it in the equivalent file, so those tests port as written.
  - Before you port a test, open the upstream `it`. Check what it calls and what it asserts, not only its name.
  - If the port lacks a function the upstream test calls, the port test checks only part of it. Rename it, list it as partial in the PR report, and file an issue to extract the function.
  - Example: PR #380 first had a test named like upstream's action-uncertain `it`. It ran through the whole agent, because the port had no `replayTrace`.

## Before you push

- If the change can affect the agent, run every real-model test: `dotnet test E2E.slnx --filter "TestCategory=RealModel"`. This covers the agent, prompts, cache, replay, the web engine, and the page script. Doc-only and config-only changes skip this step.
- The tests use the GitHub Copilot login (`dotnet run --project src/E2E.Cli -- login github-copilot`). If you cannot log in, say so in your report. Do not skip the tests silently.
- Real-model tests can be flaky. Rerun a failed test once. If it fails twice, treat it as a real failure and report it.
- Run `/code-review` on every PR, including a PR that changes only docs or test names. This rule has no exception for small or docs-only changes.

## Reviewing .NET and C# code

Run the `dotnet-claude-kit:code-review` skill on every C# change. Also check these points. They are the defects that an earlier review of the whole codebase found most often. Upstream parity, the last but one point, wins over the others.

- **Cleanup on failure.** Every browser, Playwright driver, child process, session, and `CancellationTokenSource` must be disposed when a later step throws. Look for a resource created before a call that can throw, with no `try`/`finally` or `catch` that disposes it. Put all disposals in a `finally` so one throw does not skip the next.
- **Child processes.** Read stdout and stderr together. Bound every wait with a timeout. Kill the process tree on timeout and on cancel. Pass arguments with `ArgumentList`. Never build an argument string from outside text.
- **Cancellation.** Pass the caller's `CancellationToken` down. Link it with the session's test-timeout token. A caller cancel must surface as cancellation, not as a timeout error and not as "no result". Do not catch `OperationCanceledException` to return `null`.
- **Waits and caches.** Every network or process wait needs a bound. Do not cache a failed `Lazy<Task>`. Write files through a unique temp name, then move.
- **Secrets.** Text from a provider, a response body, or a user step must go through the redactor before it reaches a message, a log, or a cache file. Do not launch or print a URL from outside text without checking the scheme and cleaning it. Set file modes only on files and directories the code created.
- **Shared state.** A test adapter holds no per-test state in a field of a shared fixture. Keep it in the test's own context or session object. Counters touched by concurrent calls use `Interlocked`.
- **Regex.** Use `\z`, not `$`, when a whole-string match must reject a trailing newline.
- **Upstream parity.** Check each fix against upstream first. If upstream behaves the same on purpose, report it. Do not "fix" it in the port.
- **Modern C#.** Match the file you edit. Use collection expressions, primary constructors, and `field` in new code. Do not rewrite untouched code to modernize it.
