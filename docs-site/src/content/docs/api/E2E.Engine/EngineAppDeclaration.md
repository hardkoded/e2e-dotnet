---
title: "EngineAppDeclaration"
description: "The app a target declares, as its engine reads it at config load (upstream's EngineAppDeclaration): the target's app, with the URL the target opens. The harn..."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineAppDeclaration"
  hidden: true
---

<a id="E2E_Engine_EngineAppDeclaration"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record EngineAppDeclaration : EngineAppInfo, IEquatable<EngineAppInfo>, IEquatable<EngineAppDeclaration>
```


The app a target declares, as its engine reads it at config load (upstream's `EngineAppDeclaration`): the target's `app`, with the
URL the target opens. The harness has checked the shape of every field before `ValidateApp` sees it.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [EngineAppInfo](/api/E2E.Engine/EngineAppInfo/)

**Implements:** [IEquatable&lt;EngineAppInfo&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;EngineAppDeclaration&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs)

## Properties

<a id="E2E_Engine_EngineAppDeclaration_Url_"></a>
<a id="E2E_Engine_EngineAppDeclaration_Url"></a>

### Url


```csharp
public string? Url { get; init; }
```


Base URL the target opens, as declared; see `app.url`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
