---
title: "E2E.XUnit.V3"
description: "The xUnit v3 runner. Subclass E2ETest to get a session per test."
editUrl: false
sidebar:
  label: "E2E.XUnit.V3"
---

[API reference](/api/)

The xUnit v3 runner. Subclass E2ETest to get a session per test.

## Classes

| Type | Summary |
| --- | --- |
| [E2ERunFixture](/api/E2E.XUnit.V3/E2ERunFixture/) | A .NET-only run-level hook (upstream's runner stops the app commands and calls the reporters when the run ends; xUnit has no such owner). Register it as an assembly fixture, and the app commands that the targets declare stop when the last test of the assembly is done, not when the test process exits, and the reporters run (see Reporter): csharp [assembly: AssemblyFixture(typeof(E2E.XUnit.V3.E2ERunFixture))] |
| [E2ETest](/api/E2E.XUnit.V3/E2ETest/) | Base class for an xUnit v3 test that drives the app. InitializeAsync() starts one engine session. DisposeAsync() commits the replay cache from the xUnit result: a pass or a skip records verified acts, a failure evicts the unverified ones it recorded or replayed, and a cancelled test leaves the cache alone. xUnit has no retry, so every test is a first attempt and can replay. A check in a derived DisposeAsync after a failure verifies nothing. Each Expect.Soft failure is kept on the session. The test body runs on, and the test fails when it is disposed, with every soft failure in one message. Settings come from E2EFixture. |
