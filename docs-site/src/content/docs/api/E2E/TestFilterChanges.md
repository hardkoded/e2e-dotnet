---
title: "TestFilterChanges"
description: "What a run's test filters judge: every file that differs from the base commit (upstream's TestFilterChanges)."
editUrl: false
prev: false
next: false
sidebar:
  label: "TestFilterChanges"
  hidden: true
---

<a id="E2E_TestFilterChanges"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record TestFilterChanges : IEquatable<TestFilterChanges>
```


What a run's test filters judge: every file that differs from the base commit (upstream's `TestFilterChanges`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;TestFilterChanges&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L37)

## Constructors

<a id="E2E_TestFilterChanges__ctor_"></a>
<a id="E2E_TestFilterChanges__ctor_System_String_System_String_System_Collections_Generic_IReadOnlyList_System_String__"></a>

### `TestFilterChanges(string, string, IReadOnlyList<string>)`


```csharp
public TestFilterChanges(string Base, string Root, IReadOnlyList<string> Files)
```


What a run's test filters judge: every file that differs from the base commit (upstream's `TestFilterChanges`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Base` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The commit the change is measured from: the merge base of the base ref and HEAD. |
| `Root` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Absolute path of the git repository root, spelled as on disk like `ProjectRoot`; &lt;code class="paramref"&gt;Files&lt;/code&gt; are relative to it. |
| `Files` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) | Repository-relative POSIX paths of every file added, modified, or deleted since &lt;code class="paramref"&gt;Base&lt;/code&gt;, both paths of a rename, uncommitted and untracked files included; sorted by code point. Spelled as git prints them, which on macOS is NFC while a test's `File` keeps the bytes on disk, so compare a non-ASCII path in NFC on both sides. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L37)

## Properties

<a id="E2E_TestFilterChanges_Base_"></a>
<a id="E2E_TestFilterChanges_Base"></a>

### Base


```csharp
public string Base { get; init; }
```


The commit the change is measured from: the merge base of the base ref and HEAD.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L37)

<a id="E2E_TestFilterChanges_Files_"></a>
<a id="E2E_TestFilterChanges_Files"></a>

### Files


```csharp
public IReadOnlyList<string> Files { get; init; }
```


Repository-relative POSIX paths of every file added, modified, or deleted since &lt;code class="paramref"&gt;Base&lt;/code&gt;, both paths of a rename,
uncommitted and untracked files included; sorted by code point. Spelled as git prints them, which on macOS is NFC while a test's
`File` keeps the bytes on disk, so compare a non-ASCII path in NFC on both sides.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L37)

<a id="E2E_TestFilterChanges_Root_"></a>
<a id="E2E_TestFilterChanges_Root"></a>

### Root


```csharp
public string Root { get; init; }
```


Absolute path of the git repository root, spelled as on disk like `ProjectRoot`; &lt;code class="paramref"&gt;Files&lt;/code&gt; are relative to it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L37)
