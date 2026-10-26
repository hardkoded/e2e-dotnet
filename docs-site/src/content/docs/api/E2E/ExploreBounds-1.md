---
title: "ExploreBounds<T>"
description: "The least, the most, and the default of a bound option. Upstream's inline { min, max, default }."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExploreBounds<T>"
  hidden: true
---

<a id="E2E_ExploreBounds_1"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ExploreBounds<T> : IEquatable<ExploreBounds<T>>
```


The least, the most, and the default of a bound option. Upstream's inline `{ min, max, default }`.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ExploreBounds&lt;T&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L14)

## Constructors

<a id="E2E_ExploreBounds_1__ctor_"></a>
<a id="E2E_ExploreBounds_1__ctor__0__0__0_"></a>

### ExploreBounds(T, T, T)


```csharp
public ExploreBounds(T Min, T Max, T Default)
```


The least, the most, and the default of a bound option. Upstream's inline `{ min, max, default }`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Min` | `T` |  |
| `Max` | `T` |  |
| `Default` | `T` |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L14)

## Properties

<a id="E2E_ExploreBounds_1_Default_"></a>
<a id="E2E_ExploreBounds_1_Default"></a>

### Default


```csharp
public T Default { get; init; }
```


**Value:** `T`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L14)

<a id="E2E_ExploreBounds_1_Max_"></a>
<a id="E2E_ExploreBounds_1_Max"></a>

### Max


```csharp
public T Max { get; init; }
```


**Value:** `T`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L14)

<a id="E2E_ExploreBounds_1_Min_"></a>
<a id="E2E_ExploreBounds_1_Min"></a>

### Min


```csharp
public T Min { get; init; }
```


**Value:** `T`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L14)
