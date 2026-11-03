---
title: "AppSetupStep"
description: "Upstream's { kind: 'app' } step. Url is the one its readiness probe polls, with a free port already assigned and without userinfo or query; null for a comman..."
editUrl: false
prev: false
next: false
sidebar:
  label: "AppSetupStep"
  hidden: true
---

<a id="E2E_Reports_AppSetupStep"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record AppSetupStep : SetupStep, IEquatable<SetupStep>, IEquatable<AppSetupStep>
```


Upstream's `{ kind: 'app' }` step. &lt;code class="paramref"&gt;Url&lt;/code&gt; is the one its readiness probe polls, with a free port already assigned and
without userinfo or query; null for a command that is ready when it exits.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [SetupStep](/api/E2E.Reports/SetupStep/)

**Implements:** [IEquatable&lt;SetupStep&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;AppSetupStep&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Constructors

<a id="E2E_Reports_AppSetupStep__ctor_"></a>
<a id="E2E_Reports_AppSetupStep__ctor_System_String_System_String_"></a>

### AppSetupStep(string, string?)


```csharp
public AppSetupStep(string Label, string? Url = null)
```


Upstream's `{ kind: 'app' }` step. &lt;code class="paramref"&gt;Url&lt;/code&gt; is the one its readiness probe polls, with a free port already assigned and
without userinfo or query; null for a command that is ready when it exits.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Label` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Url` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Reports_AppSetupStep_Label_"></a>
<a id="E2E_Reports_AppSetupStep_Label"></a>

### Label


```csharp
public string Label { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_AppSetupStep_Url_"></a>
<a id="E2E_Reports_AppSetupStep_Url"></a>

### Url


```csharp
public string? Url { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
