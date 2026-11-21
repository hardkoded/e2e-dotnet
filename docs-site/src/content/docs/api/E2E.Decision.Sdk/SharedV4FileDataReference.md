---
title: "SharedV4FileDataReference"
description: "File data holding a provider reference ({ [provider]: id })."
editUrl: false
prev: false
next: false
sidebar:
  label: "SharedV4FileDataReference"
  hidden: true
---

<a id="E2E_Decision_Sdk_SharedV4FileDataReference"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record SharedV4FileDataReference : SharedV4FileData, IEquatable<SharedV4FileData>, IEquatable<SharedV4FileDataReference>
```


File data holding a provider reference (`{ [provider]: id }`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [SharedV4FileData](/api/E2E.Decision.Sdk/SharedV4FileData/)

**Implements:** [IEquatable&lt;SharedV4FileData&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;SharedV4FileDataReference&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/SharedV4.cs)

## Constructors

<a id="E2E_Decision_Sdk_SharedV4FileDataReference__ctor_"></a>
<a id="E2E_Decision_Sdk_SharedV4FileDataReference__ctor_System_Collections_Generic_IReadOnlyDictionary_System_String_System_String__"></a>

### `SharedV4FileDataReference(IReadOnlyDictionary<string, string>)`


```csharp
public SharedV4FileDataReference(IReadOnlyDictionary<string, string> Reference)
```


File data holding a provider reference (`{ [provider]: id }`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Reference` | [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2) | The file identifier of each provider. |

## Properties

<a id="E2E_Decision_Sdk_SharedV4FileDataReference_Reference_"></a>
<a id="E2E_Decision_Sdk_SharedV4FileDataReference_Reference"></a>

### Reference


```csharp
public IReadOnlyDictionary<string, string> Reference { get; init; }
```


The file identifier of each provider.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)
