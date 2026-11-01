---
title: "IBrowserSession"
description: "The browser-shaped surface an engine session can add on top of IEngineSession. The Browser fixture uses it, and fails with UNSUPPORTED_CAPABILITY when the se..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IBrowserSession"
  hidden: true
---

<a id="E2E_Engine_IBrowserSession"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public interface IBrowserSession : IEngineSession, IAsyncDisposable
```


The browser-shaped surface an engine session can add on top of
[IEngineSession](/api/E2E.Engine/IEngineSession/). The [Browser](/api/E2E/Browser/) fixture uses it, and
fails with `UNSUPPORTED_CAPABILITY` when the session does not implement it.
The harness has already validated every argument.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L15)
