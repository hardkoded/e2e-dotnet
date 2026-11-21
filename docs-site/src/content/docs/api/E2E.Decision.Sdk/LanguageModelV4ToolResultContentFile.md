---
title: "LanguageModelV4ToolResultContentFile"
description: "File content."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4ToolResultContentFile"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultContentFile"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4ToolResultContentFile : LanguageModelV4ToolResultContentPart, IEquatable<LanguageModelV4ToolResultContentPart>, IEquatable<LanguageModelV4ToolResultContentFile>
```


File content.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LanguageModelV4ToolResultContentPart](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultContentPart/)

**Implements:** [IEquatable&lt;LanguageModelV4ToolResultContentPart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LanguageModelV4ToolResultContentFile&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4Prompt.cs)

## Constructors

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultContentFile__ctor_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultContentFile__ctor_E2E_Decision_Sdk_SharedV4FileData_System_String_"></a>

### LanguageModelV4ToolResultContentFile(SharedV4FileData, string)


```csharp
public LanguageModelV4ToolResultContentFile(SharedV4FileData Data, string MediaType)
```


File content.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Data` | [SharedV4FileData](/api/E2E.Decision.Sdk/SharedV4FileData/) | The file data. |
| `MediaType` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Either a full IANA media type or just the top-level segment. |

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultContentFile_Data_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultContentFile_Data"></a>

### Data


```csharp
public SharedV4FileData Data { get; init; }
```


The file data.

**Value:** [SharedV4FileData](/api/E2E.Decision.Sdk/SharedV4FileData/)

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultContentFile_Filename_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultContentFile_Filename"></a>

### Filename


```csharp
public string? Filename { get; init; }
```


Optional filename of the file.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultContentFile_MediaType_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultContentFile_MediaType"></a>

### MediaType


```csharp
public string MediaType { get; init; }
```


Either a full IANA media type or just the top-level segment.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
