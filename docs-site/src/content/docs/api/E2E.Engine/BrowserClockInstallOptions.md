---
title: "BrowserClockInstallOptions"
description: "The options of Browser.Clock.InstallAsync."
editUrl: false
prev: false
next: false
sidebar:
  label: "BrowserClockInstallOptions"
  hidden: true
---

<a id="E2E_Engine_BrowserClockInstallOptions"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record BrowserClockInstallOptions : IEquatable<BrowserClockInstallOptions>
```


The options of `Browser.Clock.InstallAsync`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;BrowserClockInstallOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebClock.cs)

## Properties

<a id="E2E_Engine_BrowserClockInstallOptions_Time_"></a>
<a id="E2E_Engine_BrowserClockInstallOptions_Time"></a>

### Time


```csharp
public ClockTime? Time { get; init; }
```


The time the fake clock starts at; the runner's now when left out.

**Value:** `ClockTime?`
