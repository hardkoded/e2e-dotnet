---
title: "ISessionSetup"
description: "What a framework adapter's setup attribute says about a test: it is a setup test, and it saves these sessions (upstream's test.setup(title, { sessions }, fn)..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ISessionSetup"
  hidden: true
---

<a id="E2E_ISessionSetup"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public interface ISessionSetup
```


What a framework adapter's setup attribute says about a test: it is a setup test, and it saves these sessions (upstream's
`test.setup(title, { sessions }, fn)`). .NET-only: the adapter declares the attribute, because the test framework owns what a
test is; `E2E.NUnit.E2ESetupAttribute` is the NUnit one.

**Derived:** [E2ESetupAttribute](/api/E2E.NUnit/E2ESetupAttribute/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/SessionAttributes.cs#L28)
