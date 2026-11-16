---
title: "WebApp"
description: "App on a web target. A WebEngine target always declares app.url, so BaseUrl is always set. Upstream's WebApp (web/fixtures.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "WebApp"
  hidden: true
---

<a id="E2E_Engine_WebApp"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class WebApp : App
```


`App` on a web target. A [WebEngine](/api/E2E.Engine/WebEngine/) target always declares `app.url`, so [BaseUrl](#E2E_Engine_WebApp_BaseUrl) is always set. Upstream's `WebApp` (`web/fixtures.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [App](/api/E2E/App/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebFixtures.cs)

## Properties

<a id="E2E_Engine_WebApp_BaseUrl_"></a>
<a id="E2E_Engine_WebApp_BaseUrl"></a>

### BaseUrl


```csharp
public string BaseUrl { get; }
```


The target's `app.url` with the run's port and no trailing slash: `$"{app.BaseUrl}/api/users"`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
