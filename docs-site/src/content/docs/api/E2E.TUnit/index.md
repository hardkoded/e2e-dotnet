---
title: "E2E.TUnit"
description: "The TUnit runner. Subclass E2ETest to get a session per test."
editUrl: false
sidebar:
  label: "E2E.TUnit"
---

[API reference](/api/)

The TUnit runner. Subclass E2ETest to get a session per test.

## Classes

| Type | Summary |
| --- | --- |
| [E2ERunFixture](/api/E2E.TUnit/E2ERunFixture/) | A .NET-only run-level hook (upstream's runner stops the app commands and calls the reporters when the run ends; TUnit has no such owner). Derive a public, non-abstract class with a public parameterless constructor from it in the test project, and the app commands that the targets declare stop when the last test of the run is done, not when the test process exits, and the reporters run (see E2E.Reports.Reporter): csharp public sealed class AppCommands : E2ERunFixture; A project that declares no such class runs none of this: TUnit runs a hook of a library for every project that references it, so the hook looks for the classes derived from this one in the test assembly, and does nothing when there are none. A run with several of them runs each. |
| [E2ESetupAttribute](/api/E2E.TUnit/E2ESetupAttribute/) | Marks a test of an E2ETest class as a setup test that saves sessions. Upstream's test.setup(title, { sessions }, fn). It goes beside [Test] (TUnit does not let a library define a test attribute), and Title becomes the test's display name. Its body signs in and calls SaveSessionAsync once for each name in Saves; a name saved twice, a name not declared, or a declared name left unsaved fails the test with SESSION_CONTRACT. The sessions are saved when the test passes. A test that restores one is marked E2ESessionAttribute. &lt;p&gt; A test that restores a session nobody has saved yet runs the setup test that declares it first, once per run, so a dependent can run alone (upstream's runner schedules the setup too). The dependent runs it on a new instance of the class (parameterless constructor), in a session of its own and without the class's own [Before(Test)] and [After(Test)] hooks; exactly one setup test of the assembly must declare the name, or the restore fails with SESSION_UNAVAILABLE, as it does when the setup fails. TUnit then skips that setup test when its turn comes, and still runs one that no selected test needs, which upstream's runner does not. The setup test gets the highest execution priority, so TUnit schedules it before the other tests. &lt;/p&gt; &lt;p&gt; TUnit runs tests in parallel, so the setup test and a test that restores its session can start at the same moment, and the setup then runs twice (once for the dependent, once as its own test). Put [NotInParallel] on the class when it must not. &lt;/p&gt; |
| [E2ETest](/api/E2E.TUnit/E2ETest/) | Base class for a TUnit test that drives the app. A before-test hook starts one engine session. An after-test hook commits the replay cache from the TUnit result: a pass or a skip records verified acts, a failure evicts the unverified ones it recorded or replayed, and a cancelled test leaves the cache alone. A derived [After(Test)] hook that throws fails the test, and the cache sees that failure: the commit runs when TUnit ends the test, because the result holds the hook's failure only then. The first attempt can replay. [Retry] attempts run live and still record. TUnit builds one instance per test, so the session lives on the instance. Each Expect.Soft failure is kept on the session. The test body runs on, and the test fails when its after-test hook runs, with every soft failure in one message. Settings come from E2EFixture. |
| [LooksBeforeTeardownExecutor](/api/E2E.TUnit/LooksBeforeTeardownExecutor/) | Wraps the test method, so the app is looked at the moment the body fails, as upstream does. A derived [After(Test)] hook runs after the method, and runs before this fixture's own, so it could navigate away first. The wrapper also keeps what the body did, because TUnit sets the test result only after its hooks have run. |
