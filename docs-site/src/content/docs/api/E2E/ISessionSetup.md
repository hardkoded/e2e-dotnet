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
test is; `E2E.NUnit.E2ESetupAttribute` is the NUnit one and `E2E.XUnit.V3.E2ESetupAttribute` the xUnit v3 one.

**Derived:** [E2ESetupAttribute](/api/E2E.NUnit/E2ESetupAttribute/), [E2ESetupAttribute](/api/E2E.XUnit.V3/E2ESetupAttribute/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/SessionAttributes.cs)

## Properties

<a id="E2E_ISessionSetup_Saves_"></a>
<a id="E2E_ISessionSetup_Saves"></a>

### Saves


```csharp
IReadOnlyList<string> Saves { get; }
```


The names the setup test saves, each exactly once, with `SaveSessionAsync`.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
