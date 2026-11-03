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
| [E2ERunFixture](/api/E2E.XUnit.V3/E2ERunFixture/) | A .NET-only run-level hook (upstream's runner stops the app commands and calls the reporters when the run ends; xUnit has no such owner). Register it as an assembly fixture, and the app commands that the targets declare stop when the last test of the assembly is done, not when the test process exits, and the reporters run (see E2E.Reports.Reporter): csharp [assembly: AssemblyFixture(typeof(E2E.XUnit.V3.E2ERunFixture))] To give the run test filters, derive a fixture, override Filters, and register that type instead. |
| [E2ESetupAttribute](/api/E2E.XUnit.V3/E2ESetupAttribute/) | Marks a test of an E2ETest class as a setup test that saves sessions. Upstream's test.setup(title, { sessions }, fn). It is a test: it takes the place of [Fact], and Title is the test's display name. Its body signs in and calls SaveSessionAsync once for each name in Saves; a name saved twice, a name not declared, or a declared name left unsaved fails the test with SESSION_CONTRACT. The sessions are saved when the test passes. A test that restores one is marked E2ESessionAttribute. &lt;p&gt; xUnit decides the order, not the attribute's dependents: E2ETest runs the setup tests of a class before its other tests (SetupTestMethodOrderer), and no earlier than that. Put the setup tests and the tests that use their sessions in one class, or order the classes yourself: xUnit runs classes in different collections in parallel, so a test in another class may restore a session before it is saved. A test that restores a session nobody has saved yet fails with SESSION_UNAVAILABLE. Unlike upstream's runner, xUnit runs a setup test even when no selected test needs its session. &lt;/p&gt; |
| [E2ETest](/api/E2E.XUnit.V3/E2ETest/) | Base class for an xUnit v3 test that drives the app. InitializeAsync() starts one engine session. DisposeAsync() commits the replay cache from the xUnit result: a pass or a skip records verified acts, a failure evicts the unverified ones it recorded or replayed, and a cancelled test leaves the cache alone. xUnit has no retry, so every test is a first attempt and can replay. A check in a derived DisposeAsync after a failure verifies nothing. Each Expect.Soft failure is kept on the session. The test body runs on, and the test fails when it is disposed, with every soft failure in one message. Settings come from E2EFixture. |
| [SetupTestMethodOrderer](/api/E2E.XUnit.V3/SetupTestMethodOrderer/) | Puts the setup tests (E2ESetupAttribute) of a class before its other tests, which keep xUnit's default order. .NET-only: upstream's runner orders a setup test before its dependents itself, and xUnit orders the test methods. E2ETest applies it; a class with its own [TestMethodOrderer] replaces it. |
