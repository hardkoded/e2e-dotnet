---
title: "ErrorDetails"
description: "Structured facts of one failure, beside its prose message (upstream's ErrorDetails, internal/errors.ts). An assertion carries what it expected and observed a..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ErrorDetails"
  hidden: true
---

<a id="E2E_ErrorDetails"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ErrorDetails : IEquatable<ErrorDetails>
```


Structured facts of one failure, beside its prose message (upstream's `ErrorDetails`, `internal/errors.ts`). An assertion
carries what it expected and observed and how many nodes matched. A locator failure carries the locator as written, what it asked
for, and how long it waited. A report bounds the text fields.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ErrorDetails&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L34)

## Properties

<a id="E2E_ErrorDetails_Expected_"></a>
<a id="E2E_ErrorDetails_Expected"></a>

### Expected


```csharp
public string? Expected { get; init; }
```


What an assertion expected.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L40)

<a id="E2E_ErrorDetails_Locator_"></a>
<a id="E2E_ErrorDetails_Locator"></a>

### Locator


```csharp
public string? Locator { get; init; }
```


The locator as written, such as `getByRole("button", "Save")`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L37)

<a id="E2E_ErrorDetails_Matches_"></a>
<a id="E2E_ErrorDetails_Matches"></a>

### Matches


```csharp
public int? Matches { get; init; }
```


How many nodes the locator matched.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L46)

<a id="E2E_ErrorDetails_Name_"></a>
<a id="E2E_ErrorDetails_Name"></a>

### Name


```csharp
public string? Name { get; init; }
```


The name, or the text, a locator asked for.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L52)

<a id="E2E_ErrorDetails_Observed_"></a>
<a id="E2E_ErrorDetails_Observed"></a>

### Observed


```csharp
public string? Observed { get; init; }
```


What an assertion observed.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L43)

<a id="E2E_ErrorDetails_Role_"></a>
<a id="E2E_ErrorDetails_Role"></a>

### Role


```csharp
public string? Role { get; init; }
```


The role a locator asked for.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L49)

<a id="E2E_ErrorDetails_TestId_"></a>
<a id="E2E_ErrorDetails_TestId"></a>

### TestId


```csharp
public string? TestId { get; init; }
```


The test id a locator asked for.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L55)

<a id="E2E_ErrorDetails_WaitedMs_"></a>
<a id="E2E_ErrorDetails_WaitedMs"></a>

### WaitedMs


```csharp
public long? WaitedMs { get; init; }
```


How long the locator waited, in milliseconds.

**Value:** [long?](https://learn.microsoft.com/dotnet/api/system.int64)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L58)
