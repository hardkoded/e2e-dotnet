---
title: "IAttemptTab"
description: "One tab of the attempt as an engine session hands it out (upstream's AttemptTab). The Browser fixture holds it behind a BrowserTab; a session reads and acts..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IAttemptTab"
  hidden: true
---

<a id="E2E_Engine_IAttemptTab"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public interface IAttemptTab
```


One tab of the attempt as an engine session hands it out (upstream's `AttemptTab`). The [Browser](/api/E2E/Browser/) fixture holds it behind a
[BrowserTab](/api/E2E/BrowserTab/); a session reads and acts on it through [IBrowserSession](/api/E2E.Engine/IBrowserSession/).

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs)

## Properties

<a id="E2E_Engine_IAttemptTab_IsClosed_"></a>
<a id="E2E_Engine_IAttemptTab_IsClosed"></a>

### IsClosed


```csharp
bool IsClosed { get; }
```


Whether the tab has closed.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)
