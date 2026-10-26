---
title: "RecordedTarget"
description: "RecordedTarget in E2E."
editUrl: false
prev: false
next: false
sidebar:
  label: "RecordedTarget"
  hidden: true
---

<a id="E2E_RecordedTarget"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class RecordedTarget
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L140)

## Properties

<a id="E2E_RecordedTarget_InputPurpose_"></a>
<a id="E2E_RecordedTarget_InputPurpose"></a>

### InputPurpose


```csharp
public string? InputPurpose { get; set; }
```


An anchor's input purpose, such as `password`. Null for `none`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L155)

<a id="E2E_RecordedTarget_Name_"></a>
<a id="E2E_RecordedTarget_Name"></a>

### Name


```csharp
public string? Name { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L144)

<a id="E2E_RecordedTarget_Placeholder_"></a>
<a id="E2E_RecordedTarget_Placeholder"></a>

### Placeholder


```csharp
public string? Placeholder { get; set; }
```


An anchor's placeholder.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L152)

<a id="E2E_RecordedTarget_Role_"></a>
<a id="E2E_RecordedTarget_Role"></a>

### Role


```csharp
public string? Role { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L142)

<a id="E2E_RecordedTarget_States_"></a>
<a id="E2E_RecordedTarget_States"></a>

### States


```csharp
public List<string>? States { get; set; }
```


An anchor's `checked`, `expanded`, `pressed`, and `selected` states that are on. Null for none.

**Value:** [List&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L161)

<a id="E2E_RecordedTarget_TestId_"></a>
<a id="E2E_RecordedTarget_TestId"></a>

### TestId


```csharp
public string? TestId { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L146)

<a id="E2E_RecordedTarget_Text_"></a>
<a id="E2E_RecordedTarget_Text"></a>

### Text


```csharp
public string? Text { get; set; }
```


An anchor's text when it differs from its name, with secrets redacted.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L149)

<a id="E2E_RecordedTarget_Value_"></a>
<a id="E2E_RecordedTarget_Value"></a>

### Value


```csharp
public string? Value { get; set; }
```


An anchor's value, with secrets redacted. Null for an empty or secure field.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L158)
