---
title: "Destination"
description: "One drop destination for drag, keyed like an element (upstream's Destination)."
editUrl: false
prev: false
next: false
sidebar:
  label: "Destination"
  hidden: true
---

<a id="E2E_Decision_Destination"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed record Destination : IEquatable<Destination>
```


One drop destination for `drag`, keyed like an element (upstream's `Destination`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;Destination&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Elements.cs)

## Constructors

<a id="E2E_Decision_Destination__ctor_"></a>
<a id="E2E_Decision_Destination__ctor_System_String_System_String_System_String_"></a>

### Destination(string, string, string)


```csharp
public Destination(string Id, string Label, string Role)
```


One drop destination for `drag`, keyed like an element (upstream's `Destination`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Id` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Label` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Role` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Decision_Destination_Id_"></a>
<a id="E2E_Decision_Destination_Id"></a>

### Id


```csharp
public string Id { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Destination_Label_"></a>
<a id="E2E_Decision_Destination_Label"></a>

### Label


```csharp
public string Label { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Destination_Role_"></a>
<a id="E2E_Decision_Destination_Role"></a>

### Role


```csharp
public string Role { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
