# e2e-dotnet

This repo is a .NET port of [tester-army/e2e](https://github.com/tester-army/e2e). See CONTRIBUTING.md and COMPATIBILITY.md.

## Porting an upstream change

- Port every test that the upstream commit adds or changes. Keep its name, steps, and assertions.
- A test that needs a real model or a testbed page is still a test to port. Port the page too. Run the test as a real-model test (`[Category("RealModel")]`, like `samples/TodoMvc`).
- A unit test that checks prompt text or wiring does not replace an upstream behavior test. Add it only next to the ported test.
- Skip an upstream test only when the port has no such feature at all (for example, tracing). Name each skipped test and the reason in the PR report.

## Before you push

- If the change can affect the agent, run every real-model test: `dotnet test E2E.slnx --filter "TestCategory=RealModel"`. This covers the agent, prompts, cache, replay, the web engine, and the page script. Doc-only and config-only changes skip this step.
- The tests use the GitHub Copilot login (`dotnet run --project src/E2E.Cli -- login github-copilot`). If you cannot log in, say so in your report. Do not skip the tests silently.
- Real-model tests can be flaky. Rerun a failed test once. If it fails twice, treat it as a real failure and report it.
