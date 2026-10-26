---
title: "DocumentWorld"
description: "Routes a DocumentEngine can open. Each open builds a new page from the mapped factory."
editUrl: false
prev: false
next: false
sidebar:
  label: "DocumentWorld"
  hidden: true
---

<a id="E2E_Engine_DocumentWorld"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class DocumentWorld
```


Routes a [DocumentEngine](/api/E2E.Engine/DocumentEngine/) can open. Each open builds a new page from the mapped factory.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L357)

## Methods

<a id="E2E_Engine_DocumentWorld_Map_"></a>
<a id="E2E_Engine_DocumentWorld_Map_System_String_System_Action_E2E_Engine_DocumentPage__"></a>

### `Map(string, Action<DocumentPage>)`


```csharp
public DocumentWorld Map(string route, Action<DocumentPage> build)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `route` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `build` | [Action&lt;DocumentPage&gt;](https://learn.microsoft.com/dotnet/api/system.action-1) |  |

**Returns:** [DocumentWorld](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L361)
