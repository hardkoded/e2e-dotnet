---
title: "LaunchArgumentRules"
description: "The checks and spellings of launch arguments (upstream's core/internal/launch-arguments.ts: checkLaunchArguments, engineLaunchArguments, appleUserDefaults)...."
editUrl: false
prev: false
next: false
sidebar:
  label: "LaunchArgumentRules"
  hidden: true
---

<a id="E2E_Internal_LaunchArgumentRules"></a>

Namespace: [E2E.Internal](/api/E2E.Internal/)


```csharp
public static class LaunchArgumentRules
```


The checks and spellings of launch arguments (upstream's `core/internal/launch-arguments.ts`: `checkLaunchArguments`,
`engineLaunchArguments`, `appleUserDefaults`). .NET-only holder name: C# has no free functions.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/obj/Release/net10.0/System.Text.RegularExpressions.Generator/System.Text.RegularExpressions.Generator.RegexGenerator/RegexGenerator.g.cs)

## Methods

<a id="E2E_Internal_LaunchArgumentRules_AppleUserDefaults_"></a>
<a id="E2E_Internal_LaunchArgumentRules_AppleUserDefaults_E2E_Internal_ResolvedLaunchArguments_"></a>

### AppleUserDefaults(ResolvedLaunchArguments)


```csharp
public static IReadOnlyList<string> AppleUserDefaults(ResolvedLaunchArguments launchArguments)
```


Launch arguments as Apple process arguments. A list stays as it is. An object becomes `-name value` for each entry, as
`NSUserDefaults` reads it: `YES` or `NO` for a boolean, an old-style plist array for a list, and a quoted plist string for a
value the parser would otherwise misread, such as `(draft)` or `-1`. Upstream's `appleUserDefaults`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `launchArguments` | [ResolvedLaunchArguments](/api/E2E.Internal/ResolvedLaunchArguments/) |  |

**Returns:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Internal_LaunchArgumentRules_CheckLaunchArguments_"></a>
<a id="E2E_Internal_LaunchArgumentRules_CheckLaunchArguments_System_String_System_Collections_Generic_IReadOnlyList_System_String__System_Collections_Generic_IReadOnlyDictionary_System_String_System_Nullable_E2E_LaunchArgumentValue___System_String_"></a>

### `CheckLaunchArguments(string, IReadOnlyList<string>?, IReadOnlyDictionary<string, LaunchArgumentValue?>?, string)`


```csharp
public static ResolvedLaunchArguments? CheckLaunchArguments(string label, IReadOnlyList<string>? list, IReadOnlyDictionary<string, LaunchArgumentValue?>? values, string code = "INVALID_CONFIG")
```


Checks launch arguments as declared in code: a list of strings, or an object of argument name (non-empty, no leading dash, no whitespace
or control characters) to string, finite number below 2^53 in magnitude, boolean, or list of strings, none holding a NUL character. A
null value is the entry left out. &lt;code class="paramref"&gt;code&lt;/code&gt; picks the error: a target's config fails with `INVALID_CONFIG`, a test's own
launch with `INVALID_ARGUMENT`. Declaring both &lt;code class="paramref"&gt;list&lt;/code&gt; and &lt;code class="paramref"&gt;values&lt;/code&gt; is refused: .NET-only, since upstream's
one property holds one form.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `label` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `list` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `values` | [IReadOnlyDictionary&lt;string, LaunchArgumentValue?&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2) |  |
| `code` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [ResolvedLaunchArguments](/api/E2E.Internal/ResolvedLaunchArguments/)

<a id="E2E_Internal_LaunchArgumentRules_CheckLaunchArguments_System_String_System_Nullable_System_Text_Json_JsonElement__System_String_"></a>

### CheckLaunchArguments(string, JsonElement?, string)


```csharp
public static ResolvedLaunchArguments? CheckLaunchArguments(string label, JsonElement? declared, string code = "INVALID_CONFIG")
```


Checks `app.launchArguments` as an `e2e.config.json` declared it: an array of strings or an object of argument name to value,
with the checks of `CheckLaunchArguments`.
Upstream's `checkLaunchArguments` reads its `unknown`; the JSON reader is where the port's input is untyped.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `label` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `declared` | [JsonElement?](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement) |  |
| `code` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [ResolvedLaunchArguments](/api/E2E.Internal/ResolvedLaunchArguments/)

<a id="E2E_Internal_LaunchArgumentRules_EngineLaunchArguments_"></a>
<a id="E2E_Internal_LaunchArgumentRules_EngineLaunchArguments_E2E_Internal_ResolvedLaunchArguments_"></a>

### EngineLaunchArguments(ResolvedLaunchArguments?)


```csharp
public static (IReadOnlyList<string>? LaunchArguments, IReadOnlyDictionary<string, LaunchArgumentValue>? LaunchArgumentValues) EngineLaunchArguments(ResolvedLaunchArguments? launchArguments)
```


The two fields an engine reads launch arguments from (upstream's `engineLaunchArguments`): the list, or the object, never both.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `launchArguments` | [ResolvedLaunchArguments](/api/E2E.Internal/ResolvedLaunchArguments/) |  |

**Returns:** [(IReadOnlyList&lt;string&gt; LaunchArguments, IReadOnlyDictionary&lt;string, LaunchArgumentValue&gt; LaunchArgumentValues)](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Internal_LaunchArgumentRules_WithoutNulls_"></a>
<a id="E2E_Internal_LaunchArgumentRules_WithoutNulls_System_Collections_Generic_IReadOnlyDictionary_System_String_System_Nullable_E2E_LaunchArgumentValue___"></a>

### `WithoutNulls(IReadOnlyDictionary<string, LaunchArgumentValue?>?)`


```csharp
public static IReadOnlyDictionary<string, LaunchArgumentValue>? WithoutNulls(IReadOnlyDictionary<string, LaunchArgumentValue?>? values)
```


The values with the entries a null value left out dropped, or null for none declared. The form an engine receives.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `values` | [IReadOnlyDictionary&lt;string, LaunchArgumentValue?&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2) |  |

**Returns:** [IReadOnlyDictionary&lt;string, LaunchArgumentValue&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)
