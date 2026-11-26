---
title: "ResolvedLaunchArguments"
description: "Launch arguments after the checks: a list of strings, or an object of argument name to value (upstream's ResolvedLaunchArguments, readonly string[] | LaunchA..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ResolvedLaunchArguments"
  hidden: true
---

<a id="E2E_Internal_ResolvedLaunchArguments"></a>

Namespace: [E2E.Internal](/api/E2E.Internal/)


```csharp
public sealed class ResolvedLaunchArguments
```


Launch arguments after the checks: a list of strings, or an object of argument name to value (upstream's `ResolvedLaunchArguments`,
`readonly string[] | LaunchArgumentValues`). Exactly one of [List](#E2E_Internal_ResolvedLaunchArguments_List) and [Values](#E2E_Internal_ResolvedLaunchArguments_Values) is set. .NET-only holder: C# has no union type.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Internal/LaunchArgumentRules.cs)

## Properties

<a id="E2E_Internal_ResolvedLaunchArguments_List_"></a>
<a id="E2E_Internal_ResolvedLaunchArguments_List"></a>

### List


```csharp
public IReadOnlyList<string>? List { get; }
```


The arguments as a list of strings, or null for the object form.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Internal_ResolvedLaunchArguments_Values_"></a>
<a id="E2E_Internal_ResolvedLaunchArguments_Values"></a>

### Values


```csharp
public IReadOnlyDictionary<string, LaunchArgumentValue>? Values { get; }
```


The arguments as an object of argument name to value, in declaration order, or null for the list form.

**Value:** [IReadOnlyDictionary&lt;string, LaunchArgumentValue&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

## Methods

<a id="E2E_Internal_ResolvedLaunchArguments_Of_"></a>
<a id="E2E_Internal_ResolvedLaunchArguments_Of_System_Collections_Generic_IReadOnlyDictionary_System_String_E2E_LaunchArgumentValue__"></a>

### `Of(IReadOnlyDictionary<string, LaunchArgumentValue>)`


```csharp
public static ResolvedLaunchArguments Of(IReadOnlyDictionary<string, LaunchArgumentValue> values)
```


The object form of &lt;code class="paramref"&gt;values&lt;/code&gt;.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `values` | [IReadOnlyDictionary&lt;string, LaunchArgumentValue&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2) |  |

**Returns:** [ResolvedLaunchArguments](./)

<a id="E2E_Internal_ResolvedLaunchArguments_Of_System_Collections_Generic_IReadOnlyList_System_String__"></a>

### `Of(IReadOnlyList<string>)`


```csharp
public static ResolvedLaunchArguments Of(IReadOnlyList<string> list)
```


The list form of &lt;code class="paramref"&gt;list&lt;/code&gt;.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `list` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

**Returns:** [ResolvedLaunchArguments](./)
