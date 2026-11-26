---
title: "ClientInfo"
description: "Who the client is, for InitializeParams."
editUrl: false
prev: false
next: false
sidebar:
  label: "ClientInfo"
  hidden: true
---

<a id="E2E_Engine_Protocol_ClientInfo"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed record ClientInfo : IEquatable<ClientInfo>
```


Who the client is, for [InitializeParams](/api/E2E.Engine.Protocol/InitializeParams/).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ClientInfo&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Protocol.cs)

## Constructors

<a id="E2E_Engine_Protocol_ClientInfo__ctor_"></a>
<a id="E2E_Engine_Protocol_ClientInfo__ctor_System_String_System_String_"></a>

### ClientInfo(string, string)


```csharp
public ClientInfo(string Name, string Version)
```


Who the client is, for [InitializeParams](/api/E2E.Engine.Protocol/InitializeParams/).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Version` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Engine_Protocol_ClientInfo_Name_"></a>
<a id="E2E_Engine_Protocol_ClientInfo_Name"></a>

### Name


```csharp
public string Name { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_Protocol_ClientInfo_Version_"></a>
<a id="E2E_Engine_Protocol_ClientInfo_Version"></a>

### Version


```csharp
public string Version { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
