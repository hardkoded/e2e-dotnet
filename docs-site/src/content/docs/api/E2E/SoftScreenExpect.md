---
title: "SoftScreenExpect"
description: "The screen matchers of ScreenExpect, but an ASSERTIONFAILED is kept on the session instead of thrown, as SoftLocatorExpect does."
editUrl: false
prev: false
next: false
sidebar:
  label: "SoftScreenExpect"
  hidden: true
---

<a id="E2E_SoftScreenExpect"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class SoftScreenExpect
```


The screen matchers of [ScreenExpect](/api/E2E/ScreenExpect/), but an
`ASSERTION_FAILED` is kept on the session instead of thrown, as [SoftLocatorExpect](/api/E2E/SoftLocatorExpect/) does.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L77)

## Properties

<a id="E2E_SoftScreenExpect_Not_"></a>
<a id="E2E_SoftScreenExpect_Not"></a>

### Not


```csharp
public SoftScreenExpect Not { get; }
```


Inverts the matcher, as [Not](/api/E2E/ScreenExpect/#E2E_ScreenExpect_Not). A failure is still kept, not thrown.

**Value:** [SoftScreenExpect](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L89)

## Methods

<a id="E2E_SoftScreenExpect_ToHaveScreenshotAsync_"></a>
<a id="E2E_SoftScreenExpect_ToHaveScreenshotAsync_E2E_ScreenshotOptions_System_String_"></a>

### ToHaveScreenshotAsync(ScreenshotOptions, string?)


```csharp
public Task ToHaveScreenshotAsync(ScreenshotOptions options, string? callerFile = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [ScreenshotOptions](/api/E2E/ScreenshotOptions/) |  |
| `callerFile` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L93)

<a id="E2E_SoftScreenExpect_ToHaveScreenshotAsync_System_String_E2E_ScreenshotOptions_System_String_"></a>

### ToHaveScreenshotAsync(string?, ScreenshotOptions?, string?)


```csharp
public Task ToHaveScreenshotAsync(string? name = null, ScreenshotOptions? options = null, string? callerFile = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `options` | [ScreenshotOptions](/api/E2E/ScreenshotOptions/) |  |
| `callerFile` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L91)
