---
title: "PageResponse"
description: "What WaitForResponseAsync returns and its predicate reads: a WebResponse and the request it answers."
editUrl: false
prev: false
next: false
sidebar:
  label: "PageResponse"
  hidden: true
---

<a id="E2E_PageResponse"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class PageResponse : WebResponse
```


What `WaitForResponseAsync` returns and its predicate reads: a [WebResponse](/api/E2E/WebResponse/) and the request it answers.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [WebResponse](/api/E2E/WebResponse/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs)

## Properties

<a id="E2E_PageResponse_Request_"></a>
<a id="E2E_PageResponse_Request"></a>

### Request


```csharp
public WebRequest Request { get; }
```


The request this response answers.

**Value:** [WebRequest](/api/E2E.Engine/WebRequest/)
