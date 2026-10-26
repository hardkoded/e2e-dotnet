---
title: "Secret"
description: "A value the model must not see. Prompts, transcripts, reports, and the names and test ids an act records in its replay cache entry receive <secret:name>. The..."
editUrl: false
prev: false
next: false
sidebar:
  label: "Secret"
  hidden: true
---

<a id="E2E_Secret"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class Secret
```


A value the model must not see. Prompts, transcripts, reports, and the names
and test ids an act records in its replay cache entry receive
`<secret:name>`. The agent fills the real value itself.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Values/Secret.cs#L15)

## Fields

<a id="E2E_Secret_MinLength"></a>

### MinLength


```csharp
public const int MinLength = 6
```


The fewest characters, counted as code points, a secret value may have. Redaction rewrites
every occurrence of the value, so a shorter one would take ordinary text with it.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Values/Secret.cs#L32)

## Properties

<a id="E2E_Secret_Name_"></a>
<a id="E2E_Secret_Name"></a>

### Name


```csharp
public string Name { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Values/Secret.cs#L24)

<a id="E2E_Secret_Purpose_"></a>
<a id="E2E_Secret_Purpose"></a>

### Purpose


```csharp
public string? Purpose { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Values/Secret.cs#L26)

## Methods

<a id="E2E_Secret_Create_"></a>
<a id="E2E_Secret_Create_System_String_System_String_System_String_"></a>

### Create(string, string, string?)


```csharp
public static Secret Create(string name, string value, string? purpose = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `value` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `purpose` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [Secret](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Values/Secret.cs#L36)

<a id="E2E_Secret_ToString_"></a>
<a id="E2E_Secret_ToString"></a>

### ToString()


```csharp
public override string ToString()
```


Returns a string that represents the current object.

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string) — A string that represents the current object.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Values/Secret.cs#L62)
