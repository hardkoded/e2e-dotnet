---
title: "TestContext"
description: "The fixtures for one test. Call String) before the first step when a precondition is missing."
editUrl: false
prev: false
next: false
sidebar:
  label: "TestContext"
  hidden: true
---

<a id="E2E_TestContext"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class TestContext
```


The fixtures for one test. Call `String)` before the first step when a precondition is missing.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L216)

## Properties

<a id="E2E_TestContext_Agent_"></a>
<a id="E2E_TestContext_Agent"></a>

### Agent


```csharp
public required Agent Agent { get; init; }
```


**Value:** [Agent](/api/E2E/Agent/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L226)

<a id="E2E_TestContext_App_"></a>
<a id="E2E_TestContext_App"></a>

### App


```csharp
public required App App { get; init; }
```


**Value:** [App](/api/E2E/App/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L218)

<a id="E2E_TestContext_Browser_"></a>
<a id="E2E_TestContext_Browser"></a>

### Browser


```csharp
public required Browser Browser { get; init; }
```


The browser fixture. Its members fail with `UNSUPPORTED_CAPABILITY` on an engine without a browser.

**Value:** [Browser](/api/E2E/Browser/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L221)

<a id="E2E_TestContext_CancellationToken_"></a>
<a id="E2E_TestContext_CancellationToken"></a>

### CancellationToken


```csharp
public CancellationToken CancellationToken { get; init; }
```


**Value:** [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L230)

<a id="E2E_TestContext_Platform_"></a>
<a id="E2E_TestContext_Platform"></a>

### Platform


```csharp
public required string Platform { get; init; }
```


The engine platform, such as `web` or `document`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L224)

<a id="E2E_TestContext_Screen_"></a>
<a id="E2E_TestContext_Screen"></a>

### Screen


```csharp
public required Screen Screen { get; init; }
```


**Value:** [Screen](/api/E2E/Screen/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L228)

## Methods

<a id="E2E_TestContext_Skip_"></a>
<a id="E2E_TestContext_Skip_System_Boolean_System_String_"></a>

### Skip(bool, string)


```csharp
public void Skip(bool condition, string reason)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `condition` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `reason` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L232)
