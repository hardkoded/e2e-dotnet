---
title: "TestIdentity"
description: "Who a test is (upstream's TestIdentity, collect/collect.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "TestIdentity"
  hidden: true
---

<a id="E2E_Reports_TestIdentity"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record TestIdentity : IEquatable<TestIdentity>
```


Who a test is (upstream's `TestIdentity`, `collect/collect.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;TestIdentity&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Constructors

<a id="E2E_Reports_TestIdentity__ctor_"></a>
<a id="E2E_Reports_TestIdentity__ctor_System_Collections_Generic_IReadOnlyList_System_String__System_String_System_String_System_Int32_"></a>

### `TestIdentity(IReadOnlyList<string>, string, string, int)`


```csharp
public TestIdentity(IReadOnlyList<string> TitlePath, string Id, string File, int DeclarationIndex)
```


Who a test is (upstream's `TestIdentity`, `collect/collect.ts`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `TitlePath` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `Id` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `File` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `DeclarationIndex` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |

## Properties

<a id="E2E_Reports_TestIdentity_DeclarationIndex_"></a>
<a id="E2E_Reports_TestIdentity_DeclarationIndex"></a>

### DeclarationIndex


```csharp
public int DeclarationIndex { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Reports_TestIdentity_File_"></a>
<a id="E2E_Reports_TestIdentity_File"></a>

### File


```csharp
public string File { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_TestIdentity_Id_"></a>
<a id="E2E_Reports_TestIdentity_Id"></a>

### Id


```csharp
public string Id { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_TestIdentity_TitlePath_"></a>
<a id="E2E_Reports_TestIdentity_TitlePath"></a>

### TitlePath


```csharp
public IReadOnlyList<string> TitlePath { get; init; }
```


**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
