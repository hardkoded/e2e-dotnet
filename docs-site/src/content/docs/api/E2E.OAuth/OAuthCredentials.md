---
title: "OAuthCredentials"
description: "What a login returns and a refresh renews. Stored as upstream stores it: access, refresh, expires (epoch milliseconds, 0 when the token does not expire), plu..."
editUrl: false
prev: false
next: false
sidebar:
  label: "OAuthCredentials"
  hidden: true
---

<a id="E2E_OAuth_OAuthCredentials"></a>

Namespace: [E2E.OAuth](/api/E2E.OAuth/)


```csharp
public sealed class OAuthCredentials
```


What a login returns and a refresh renews. Stored as upstream stores it: `access`, `refresh`,
`expires` (epoch milliseconds, 0 when the token does not expire), plus each provider's own fields.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthCredentials.cs#L46)

## Properties

<a id="E2E_OAuth_OAuthCredentials_Access_"></a>
<a id="E2E_OAuth_OAuthCredentials_Access"></a>

### Access


```csharp
public required string Access { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthCredentials.cs#L48)

<a id="E2E_OAuth_OAuthCredentials_Expires_"></a>
<a id="E2E_OAuth_OAuthCredentials_Expires"></a>

### Expires


```csharp
public long Expires { get; init; }
```


Epoch milliseconds; 0 when the token does not expire.

**Value:** [long](https://learn.microsoft.com/dotnet/api/system.int64)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthCredentials.cs#L54)

<a id="E2E_OAuth_OAuthCredentials_Extra_"></a>
<a id="E2E_OAuth_OAuthCredentials_Extra"></a>

### Extra


```csharp
public IReadOnlyDictionary<string, string> Extra { get; init; }
```


Provider fields: `accountId`, `residency`, `enterpriseUrl`, `orgId`.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthCredentials.cs#L57)

<a id="E2E_OAuth_OAuthCredentials_Refresh_"></a>
<a id="E2E_OAuth_OAuthCredentials_Refresh"></a>

### Refresh


```csharp
public string Refresh { get; init; }
```


Empty when the vendor issues non-expiring tokens.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthCredentials.cs#L51)

## Methods

<a id="E2E_OAuth_OAuthCredentials_Get_"></a>
<a id="E2E_OAuth_OAuthCredentials_Get_System_String_"></a>

### Get(string)


```csharp
public string? Get(string name)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthCredentials.cs#L59)

<a id="E2E_OAuth_OAuthCredentials_With_"></a>
<a id="E2E_OAuth_OAuthCredentials_With_System_String_System_String_"></a>

### With(string, string?)


```csharp
public OAuthCredentials With(string name, string? value)
```


A copy with &lt;code class="paramref"&gt;name&lt;/code&gt; set, or removed when &lt;code class="paramref"&gt;value&lt;/code&gt; is null.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `value` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [OAuthCredentials](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthCredentials.cs#L62)
