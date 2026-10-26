---
title: "AppLogEntry"
description: "One line of what the app did on its own, as an engine reports it to AppLog (upstream's AppLogEntry): console output, an exception nothing caught, a failed re..."
editUrl: false
prev: false
next: false
sidebar:
  label: "AppLogEntry"
  hidden: true
---

<a id="E2E_Engine_AppLogEntry"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record AppLogEntry : IEquatable<AppLogEntry>
```


One line of what the app did on its own, as an engine reports it to [AppLog](/api/E2E.Engine/EngineStartOptions/#E2E_Engine_EngineStartOptions_AppLog) (upstream's
`AppLogEntry`): console output, an exception nothing caught, a failed request. The harness redacts and bounds it.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;AppLogEntry&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/AppLogEntry.cs#L14)

## Constructors

<a id="E2E_Engine_AppLogEntry__ctor_"></a>
<a id="E2E_Engine_AppLogEntry__ctor_E2E_AppLogSource_E2E_AppLogLevel_System_String_"></a>

### AppLogEntry(AppLogSource, AppLogLevel, string)


```csharp
public AppLogEntry(AppLogSource Source, AppLogLevel Level, string Text)
```


One line of what the app did on its own, as an engine reports it to [AppLog](/api/E2E.Engine/EngineStartOptions/#E2E_Engine_EngineStartOptions_AppLog) (upstream's
`AppLogEntry`): console output, an exception nothing caught, a failed request. The harness redacts and bounds it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Source` | [AppLogSource](/api/E2E/AppLogSource/) | Where the line came from. |
| `Level` | [AppLogLevel](/api/E2E/AppLogLevel/) | How bad it was. |
| `Text` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The line. A blank one is dropped. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/AppLogEntry.cs#L14)

## Properties

<a id="E2E_Engine_AppLogEntry_Level_"></a>
<a id="E2E_Engine_AppLogEntry_Level"></a>

### Level


```csharp
public AppLogLevel Level { get; init; }
```


How bad it was.

**Value:** [AppLogLevel](/api/E2E/AppLogLevel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/AppLogEntry.cs#L14)

<a id="E2E_Engine_AppLogEntry_Source_"></a>
<a id="E2E_Engine_AppLogEntry_Source"></a>

### Source


```csharp
public AppLogSource Source { get; init; }
```


Where the line came from.

**Value:** [AppLogSource](/api/E2E/AppLogSource/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/AppLogEntry.cs#L14)

<a id="E2E_Engine_AppLogEntry_Text_"></a>
<a id="E2E_Engine_AppLogEntry_Text"></a>

### Text


```csharp
public string Text { get; init; }
```


The line. A blank one is dropped.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/AppLogEntry.cs#L14)
