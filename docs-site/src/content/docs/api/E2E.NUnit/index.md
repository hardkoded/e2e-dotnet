---
title: "E2E.NUnit"
description: "The NUnit runner. Subclass E2ETest to get a session per test."
editUrl: false
sidebar:
  label: "E2E.NUnit"
---

[API reference](/api/)

The NUnit runner. Subclass E2ETest to get a session per test.

## Classes

| Type | Summary |
| --- | --- |
| [E2ERunFixture](/api/E2E.NUnit/E2ERunFixture/) | A .NET-only run-level hook (upstream's runner stops the app commands and calls the reporters when the run ends; NUnit has no such owner). Derive a [SetUpFixture] from it, outside any namespace so it covers the whole assembly, and the app commands that the targets declare stop when the last test of the assembly is done, not when the test process exits, and the reporters run (see Reporter): csharp [SetUpFixture] public sealed class AppCommands : E2ERunFixture; |
| [E2ETest](/api/E2E.NUnit/E2ETest/) | Base class for an NUnit test that drives the app. Setup starts one engine session. Teardown commits the replay cache from the NUnit result: a pass or a skip records verified acts, a failure evicts the unverified ones it recorded or replayed, and a cancelled test leaves the cache alone. The first attempt can replay. [Retry] attempts run live and still record. Each [Repeat] iteration is a first attempt. A check in a derived [TearDown] after a failure verifies nothing. Each Expect.Soft failure is recorded on the NUnit result, as inside Assert.EnterMultipleScope, so the test fails when its body ends and lists every soft failure. A soft failure outranks a skip: Assert.Ignore or Assert.Inconclusive after one fails the test, and the skip reason stays in the message. Settings come from E2EFixture. |
| [PollExpectationExtensions](/api/E2E.NUnit/PollExpectationExtensions/) | NUnit constraints for Expect.Poll, in place of the upstream value matchers. |
