---
title: "VcsInfo"
description: "Where the code under test came from (upstream's VcsInfo, internal/vcs.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "VcsInfo"
  hidden: true
---

<a id="E2E_Reports_VcsInfo"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record VcsInfo : IEquatable<VcsInfo>
```


Where the code under test came from (upstream's `VcsInfo`, `internal/vcs.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;VcsInfo&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L107)

## Constructors

<a id="E2E_Reports_VcsInfo__ctor_"></a>
<a id="E2E_Reports_VcsInfo__ctor_System_String_System_String_System_Nullable_System_Boolean__"></a>

### VcsInfo(string, string?, bool?)


```csharp
public VcsInfo(string Commit, string? Branch = null, bool? Dirty = null)
```


Where the code under test came from (upstream's `VcsInfo`, `internal/vcs.ts`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Commit` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The commit the working tree is checked out at. |
| `Branch` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The branch name; null on a detached HEAD or when only the commit is known. |
| `Dirty` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) | True when tracked files differ from the commit; null when git could not say. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L107)

## Properties

<a id="E2E_Reports_VcsInfo_Branch_"></a>
<a id="E2E_Reports_VcsInfo_Branch"></a>

### Branch


```csharp
public string? Branch { get; init; }
```


The branch name; null on a detached HEAD or when only the commit is known.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L107)

<a id="E2E_Reports_VcsInfo_Commit_"></a>
<a id="E2E_Reports_VcsInfo_Commit"></a>

### Commit


```csharp
public string Commit { get; init; }
```


The commit the working tree is checked out at.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L107)

<a id="E2E_Reports_VcsInfo_Dirty_"></a>
<a id="E2E_Reports_VcsInfo_Dirty"></a>

### Dirty


```csharp
public bool? Dirty { get; init; }
```


True when tracked files differ from the commit; null when git could not say.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L107)
