---
title: "Credentials"
description: "Reads E2EUSER{NAME}USERNAME and E2EUSER{NAME}PASSWORD. The name is uppercased and dashes become underscores."
editUrl: false
prev: false
next: false
sidebar:
  label: "Credentials"
  hidden: true
---

<a id="E2E_Credentials"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public static class Credentials
```


Reads `E2E_USER_{NAME}_USERNAME` and `E2E_USER_{NAME}_PASSWORD`.
The name is uppercased and dashes become underscores.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Values/Secret.cs#L119)

## Methods

<a id="E2E_Credentials_User_"></a>
<a id="E2E_Credentials_User_System_String_"></a>

### User(string)


```csharp
public static UserCredential User(string name)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [UserCredential](/api/E2E/UserCredential/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Values/Secret.cs#L121)
