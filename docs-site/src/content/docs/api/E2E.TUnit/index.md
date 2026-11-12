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
| [E2ETest](/api/E2E.TUnit/E2ETest/) | Base class for a TUnit test that drives the app. A before-test hook starts one engine session. An after-test hook commits the replay cache from the TUnit result: a pass or a skip records verified acts, a failure evicts the unverified ones it recorded or replayed, and a cancelled test leaves the cache alone. The first attempt can replay. [Retry] attempts run live and still record. TUnit builds one instance per test, so the session lives on the instance. Each Expect.Soft failure is kept on the session. The test body runs on, and the test fails when its after-test hook runs, with every soft failure in one message. Settings come from E2EFixture. |
| [LooksBeforeTeardownExecutor](/api/E2E.TUnit/LooksBeforeTeardownExecutor/) | Wraps the test method, so the app is looked at the moment the body fails, as upstream does. A derived [After(Test)] hook runs after the method, and runs before this fixture's own, so it could navigate away first. The wrapper also keeps what the body did, because TUnit sets the test result only after its hooks have run. |
