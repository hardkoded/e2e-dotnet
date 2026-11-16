---
title: "WebFixtures"
description: "What a session on a WebEngine holds beside Screen: the Browser fixture, and App with its BaseUrl always set. Read it from Fixtures. Upstream's WebFixtures (w..."
editUrl: false
prev: false
next: false
sidebar:
  label: "WebFixtures"
  hidden: true
---

<a id="E2E_Engine_WebFixtures"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class WebFixtures
```


What a session on a [WebEngine](/api/E2E.Engine/WebEngine/) holds beside `Screen`: the [Browser](#E2E_Engine_WebFixtures_Browser) fixture, and [App](#E2E_Engine_WebFixtures_App) with its
[BaseUrl](/api/E2E.Engine/WebApp/#E2E_Engine_WebApp_BaseUrl) always set. Read it from `Fixtures`. Upstream's `WebFixtures` (`web/fixtures.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebFixtures.cs)

## Properties

<a id="E2E_Engine_WebFixtures_App_"></a>
<a id="E2E_Engine_WebFixtures_App"></a>

### App


```csharp
public WebApp App { get; }
```


The app, with its base URL always set.

**Value:** [WebApp](/api/E2E.Engine/WebApp/)

<a id="E2E_Engine_WebFixtures_Browser_"></a>
<a id="E2E_Engine_WebFixtures_Browser"></a>

### Browser


```csharp
public Browser Browser { get; }
```


The browser fixture.

**Value:** [Browser](/api/E2E/Browser/)
