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
| [E2ERunFixture](/api/E2E.NUnit/E2ERunFixture/) | A .NET-only run-level hook (upstream's runner stops the app commands and calls the reporters when the run ends; NUnit has no such owner). Derive a [SetUpFixture] from it, outside any namespace so it covers the whole assembly, and the app commands that the targets declare stop when the last test of the assembly is done, not when the test process exits, and the reporters run (see E2E.Reports.Reporter): csharp [SetUpFixture] public sealed class AppCommands : E2ERunFixture; |
| [E2ESetupAttribute](/api/E2E.NUnit/E2ESetupAttribute/) | Marks a test of an E2ETest class as a setup test that saves sessions. Upstream's test.setup(title, { sessions }, fn). It is a test: it takes the place of [Test], and Title is the test's name. Its body signs in and calls SaveSessionAsync once for each name in Saves; a name saved twice, a name not declared, or a declared name left unsaved fails the test with SESSION_CONTRACT. The sessions are saved when the test passes. A test that restores one is marked E2ESessionAttribute. &lt;p&gt; NUnit decides the order, not the attribute's dependents: a setup test runs before the tests of its class that are not setup tests, and before the other fixtures of its namespace unless they set an [Order] of their own. Put setup tests in the first fixture, or order the fixtures. A test that restores a session nobody has saved yet fails with SESSION_UNAVAILABLE. Unlike upstream's runner, NUnit runs a setup test even when no selected test needs its session. &lt;/p&gt; |
| [E2ETest](/api/E2E.NUnit/E2ETest/) | Base class for an NUnit test that drives the app. Setup starts one engine session. Teardown commits the replay cache from the NUnit result: a pass or a skip records verified acts, a failure evicts the unverified ones it recorded or replayed, and a cancelled test leaves the cache alone. The first attempt can replay. [Retry] attempts run live and still record. Each [Repeat] iteration is a first attempt. A check in a derived [TearDown] after a failure verifies nothing. Each Expect.Soft failure is recorded on the NUnit result, as inside Assert.EnterMultipleScope, so the test fails when its body ends and lists every soft failure. A soft failure outranks a skip: Assert.Ignore or Assert.Inconclusive after one fails the test, and the skip reason stays in the message. Settings come from E2EFixture. |
| [PollExpectationExtensions](/api/E2E.NUnit/PollExpectationExtensions/) | NUnit constraints for Expect.Poll, in place of the upstream value matchers. |
