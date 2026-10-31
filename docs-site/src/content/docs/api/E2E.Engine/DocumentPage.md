---
title: "DocumentPage"
description: "One in-memory screen. Builder methods append controls in reading order."
editUrl: false
prev: false
next: false
sidebar:
  label: "DocumentPage"
  hidden: true
---

<a id="E2E_Engine_DocumentPage"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class DocumentPage
```


One in-memory screen. Builder methods append controls in reading order.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L390)

## Properties

<a id="E2E_Engine_DocumentPage_OnScroll_"></a>
<a id="E2E_Engine_DocumentPage_OnScroll"></a>

### OnScroll


```csharp
public Action<ScrollDirection>? OnScroll { get; set; }
```


Runs when the viewport scrolls, for a page that loads more as it is scrolled. The page has no viewport of its own.

**Value:** [Action&lt;ScrollDirection&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L400)

<a id="E2E_Engine_DocumentPage_Path_"></a>
<a id="E2E_Engine_DocumentPage_Path"></a>

### Path


```csharp
public string Path { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L397)

## Methods

<a id="E2E_Engine_DocumentPage_Button_"></a>
<a id="E2E_Engine_DocumentPage_Button_System_String_System_Action_"></a>

### Button(string, Action?)


```csharp
public DocumentElement Button(string name, Action? onTap = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `onTap` | [Action](https://learn.microsoft.com/dotnet/api/system.action) |  |

**Returns:** [DocumentElement](/api/E2E.Engine/DocumentElement/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L409)

<a id="E2E_Engine_DocumentPage_Checkbox_"></a>
<a id="E2E_Engine_DocumentPage_Checkbox_System_String_System_Boolean_"></a>

### Checkbox(string, bool)


```csharp
public DocumentElement Checkbox(string name, bool isChecked = false)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `isChecked` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

**Returns:** [DocumentElement](/api/E2E.Engine/DocumentElement/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L441)

<a id="E2E_Engine_DocumentPage_Heading_"></a>
<a id="E2E_Engine_DocumentPage_Heading_System_String_System_Int32_"></a>

### Heading(string, int)


```csharp
public DocumentElement Heading(string name, int level = 1)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `level` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |

**Returns:** [DocumentElement](/api/E2E.Engine/DocumentElement/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L404)

<a id="E2E_Engine_DocumentPage_Link_"></a>
<a id="E2E_Engine_DocumentPage_Link_System_String_System_String_"></a>

### Link(string, string)


```csharp
public DocumentElement Link(string name, string route)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `route` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [DocumentElement](/api/E2E.Engine/DocumentElement/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L436)

<a id="E2E_Engine_DocumentPage_Paragraph_"></a>
<a id="E2E_Engine_DocumentPage_Paragraph_System_String_"></a>

### Paragraph(string)


```csharp
public DocumentElement Paragraph(string text)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [DocumentElement](/api/E2E.Engine/DocumentElement/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L431)

<a id="E2E_Engine_DocumentPage_Status_"></a>
<a id="E2E_Engine_DocumentPage_Status_System_String_System_Boolean_"></a>

### Status(string, bool)


```csharp
public DocumentElement Status(string name, bool hidden = false)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `hidden` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

**Returns:** [DocumentElement](/api/E2E.Engine/DocumentElement/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L414)

<a id="E2E_Engine_DocumentPage_TestId_"></a>
<a id="E2E_Engine_DocumentPage_TestId_System_String_System_String_System_String_"></a>

### TestId(string, string?, string?)


```csharp
public DocumentElement TestId(string testId, string? role = null, string? name = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `testId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `role` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [DocumentElement](/api/E2E.Engine/DocumentElement/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L446)

<a id="E2E_Engine_DocumentPage_Textbox_"></a>
<a id="E2E_Engine_DocumentPage_Textbox_System_String_System_String_System_Boolean_"></a>

### Textbox(string, string?, bool)


```csharp
public DocumentElement Textbox(string name, string? value = null, bool secure = false)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `value` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `secure` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

**Returns:** [DocumentElement](/api/E2E.Engine/DocumentElement/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L419)
