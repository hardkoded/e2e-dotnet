---
title: "TestFilterCandidate"
description: "One test a filter may leave out: what names it, never its body (upstream's TestFilterCandidate)."
editUrl: false
prev: false
next: false
sidebar:
  label: "TestFilterCandidate"
  hidden: true
---

<a id="E2E_TestFilterCandidate"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record TestFilterCandidate : IEquatable<TestFilterCandidate>
```


One test a filter may leave out: what names it, never its body (upstream's `TestFilterCandidate`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;TestFilterCandidate&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L27)

## Constructors

<a id="E2E_TestFilterCandidate__ctor_"></a>
<a id="E2E_TestFilterCandidate__ctor_System_String_System_String_System_Collections_Generic_IReadOnlyList_System_String__System_Collections_Generic_IReadOnlyList_System_String__System_String_"></a>

### `TestFilterCandidate(string, string, IReadOnlyList<string>, IReadOnlyList<string>, string?)`


```csharp
public TestFilterCandidate(string Id, string File, IReadOnlyList<string> TitlePath, IReadOnlyList<string> Tags, string? SetupFile = null)
```


One test a filter may leave out: what names it, never its body (upstream's `TestFilterCandidate`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Id` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Stable test id (`<file>::<titles>`), the report's `testId`, and the key `Exclude` takes. |
| `File` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Project-relative POSIX path of the test file. |
| `TitlePath` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) | The describe titles, then the test title. |
| `Tags` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) | The describe chain's tags, outermost first, then the test's own. |
| `SetupFile` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Project-relative POSIX path of the setup test file that saves the test's session; null when the test uses none. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L27)

## Properties

<a id="E2E_TestFilterCandidate_File_"></a>
<a id="E2E_TestFilterCandidate_File"></a>

### File


```csharp
public string File { get; init; }
```


Project-relative POSIX path of the test file.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L27)

<a id="E2E_TestFilterCandidate_Id_"></a>
<a id="E2E_TestFilterCandidate_Id"></a>

### Id


```csharp
public string Id { get; init; }
```


Stable test id (`<file>::<titles>`), the report's `testId`, and the key `Exclude` takes.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L27)

<a id="E2E_TestFilterCandidate_SetupFile_"></a>
<a id="E2E_TestFilterCandidate_SetupFile"></a>

### SetupFile


```csharp
public string? SetupFile { get; init; }
```


Project-relative POSIX path of the setup test file that saves the test's session; null when the test uses none.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L27)

<a id="E2E_TestFilterCandidate_Tags_"></a>
<a id="E2E_TestFilterCandidate_Tags"></a>

### Tags


```csharp
public IReadOnlyList<string> Tags { get; init; }
```


The describe chain's tags, outermost first, then the test's own.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L27)

<a id="E2E_TestFilterCandidate_TitlePath_"></a>
<a id="E2E_TestFilterCandidate_TitlePath"></a>

### TitlePath


```csharp
public IReadOnlyList<string> TitlePath { get; init; }
```


The describe titles, then the test title.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L27)
