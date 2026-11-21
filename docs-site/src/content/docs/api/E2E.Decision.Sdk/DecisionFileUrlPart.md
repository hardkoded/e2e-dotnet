---
title: "DecisionFileUrlPart"
description: "A file at a URL (the AI SDK's FilePart with URL data). ExperimentalDecide downloads it through ExperimentalDownload and hands the model a DecisionFilePart wi..."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionFileUrlPart"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecisionFileUrlPart"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record DecisionFileUrlPart : DecisionStatePart, IEquatable<DecisionStatePart>, IEquatable<DecisionFileUrlPart>
```


A file at a URL (the AI SDK's `FilePart` with URL data). [ExperimentalDecide](/api/E2E.Decision.Sdk/ExperimentalDecide/) downloads it through
[ExperimentalDownload](/api/E2E.Decision.Sdk/DecideOptions/#E2E_Decision_Sdk_DecideOptions_ExperimentalDownload) and hands the model a [DecisionFilePart](/api/E2E.Decision.Sdk/DecisionFilePart/) with the bytes. .NET-only name: C#
has no union of bytes and a URL, so the URL form is its own part.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [DecisionStatePart](/api/E2E.Decision.Sdk/DecisionStatePart/)

**Implements:** [IEquatable&lt;DecisionStatePart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;DecisionFileUrlPart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecisionTypes.cs)

## Constructors

<a id="E2E_Decision_Sdk_DecisionFileUrlPart__ctor_"></a>
<a id="E2E_Decision_Sdk_DecisionFileUrlPart__ctor_System_String_System_Uri_"></a>

### DecisionFileUrlPart(string, Uri)


```csharp
public DecisionFileUrlPart(string MediaType, Uri Url)
```


A file at a URL (the AI SDK's `FilePart` with URL data). [ExperimentalDecide](/api/E2E.Decision.Sdk/ExperimentalDecide/) downloads it through
[ExperimentalDownload](/api/E2E.Decision.Sdk/DecideOptions/#E2E_Decision_Sdk_DecideOptions_ExperimentalDownload) and hands the model a [DecisionFilePart](/api/E2E.Decision.Sdk/DecisionFilePart/) with the bytes. .NET-only name: C#
has no union of bytes and a URL, so the URL form is its own part.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `MediaType` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Url` | [Uri](https://learn.microsoft.com/dotnet/api/system.uri) |  |

## Properties

<a id="E2E_Decision_Sdk_DecisionFileUrlPart_MediaType_"></a>
<a id="E2E_Decision_Sdk_DecisionFileUrlPart_MediaType"></a>

### MediaType


```csharp
public string MediaType { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecisionFileUrlPart_Url_"></a>
<a id="E2E_Decision_Sdk_DecisionFileUrlPart_Url"></a>

### Url


```csharp
public Uri Url { get; init; }
```


**Value:** [Uri](https://learn.microsoft.com/dotnet/api/system.uri)
