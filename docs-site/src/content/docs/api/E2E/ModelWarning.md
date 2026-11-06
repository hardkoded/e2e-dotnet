---
title: "ModelWarning"
description: "One of upstream's call warnings (CallWarning): the provider changed the request instead of failing it. A provider that sends auto in place of the forced tool..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ModelWarning"
  hidden: true
---

<a id="E2E_ModelWarning"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ModelWarning : IEquatable<ModelWarning>
```


One of upstream's call warnings (`CallWarning`): the provider changed the request instead of failing it. A provider that sends
`auto` in place of the forced tool choice it was asked for says so with `Type` `unsupported` and `Feature` `toolChoice`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ModelWarning&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs)

## Constructors

<a id="E2E_ModelWarning__ctor_"></a>
<a id="E2E_ModelWarning__ctor_System_String_System_String_System_String_"></a>

### ModelWarning(string, string?, string?)


```csharp
public ModelWarning(string Type, string? Feature = null, string? Details = null)
```


One of upstream's call warnings (`CallWarning`): the provider changed the request instead of failing it. A provider that sends
`auto` in place of the forced tool choice it was asked for says so with `Type` `unsupported` and `Feature` `toolChoice`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Type` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The kind of warning, such as `unsupported`. |
| `Feature` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The request feature it is about, such as `toolChoice`. |
| `Details` | [string](https://learn.microsoft.com/dotnet/api/system.string) | What the provider did about it. Provider text: redact it before it reaches a message. |

## Properties

<a id="E2E_ModelWarning_Details_"></a>
<a id="E2E_ModelWarning_Details"></a>

### Details


```csharp
public string? Details { get; init; }
```


What the provider did about it. Provider text: redact it before it reaches a message.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_ModelWarning_Feature_"></a>
<a id="E2E_ModelWarning_Feature"></a>

### Feature


```csharp
public string? Feature { get; init; }
```


The request feature it is about, such as `toolChoice`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_ModelWarning_Type_"></a>
<a id="E2E_ModelWarning_Type"></a>

### Type


```csharp
public string Type { get; init; }
```


The kind of warning, such as `unsupported`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
