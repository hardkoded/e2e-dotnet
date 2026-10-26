---
title: "BrowserProtocol"
description: "The protocol a leased browser's endpoint speaks. .NET-only: upstream's lease always carries a CDP endpoint."
editUrl: false
prev: false
next: false
sidebar:
  label: "BrowserProtocol"
  hidden: true
---

<a id="E2E_Engine_BrowserProtocol"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public enum BrowserProtocol
```


The protocol a leased browser's endpoint speaks. .NET-only: upstream's lease always carries a CDP endpoint.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L28)

## Fields

<a id="E2E_Engine_BrowserProtocol_Cdp"></a>

### Cdp


```csharp
Cdp = 0
```


The Chrome DevTools Protocol, dialed with Playwright's `connectOverCDP`. A browser the engine finds with a default context.

**Value:** [BrowserProtocol](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L31)

<a id="E2E_Engine_BrowserProtocol_Playwright"></a>

### Playwright


```csharp
Playwright = 1
```


Playwright's own protocol, dialed with Playwright's `connect`: a Playwright server or a grid that hosts one.
The browser has no context yet, so the engine opens its own, and a dropped connection cannot be reattached.

**Value:** [BrowserProtocol](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L37)
