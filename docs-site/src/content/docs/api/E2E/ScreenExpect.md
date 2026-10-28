---
title: "ScreenExpect"
description: "Matchers on the whole screen: String)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ScreenExpect"
  hidden: true
---

<a id="E2E_ScreenExpect"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ScreenExpect
```


Matchers on the whole screen: `String)`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L608)

## Properties

<a id="E2E_ScreenExpect_Not_"></a>
<a id="E2E_ScreenExpect_Not"></a>

### Not


```csharp
public ScreenExpect Not { get; }
```


Inverts the matcher: waits for the screen to differ from its stored screenshot.

**Value:** [ScreenExpect](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L620)

## Methods

<a id="E2E_ScreenExpect_ToHaveScreenshotAsync_"></a>
<a id="E2E_ScreenExpect_ToHaveScreenshotAsync_E2E_ScreenshotOptions_System_String_"></a>

### ToHaveScreenshotAsync(ScreenshotOptions, string?)


```csharp
public Task ToHaveScreenshotAsync(ScreenshotOptions options, string? callerFile = null)
```


Waits for the screen to look like its stored screenshot, `<test file>-snapshots/<name>-<target>-<os>.png`
beside the file this call is in. &lt;code class="paramref"&gt;name&lt;/code&gt; defaults to the test's title and a count. A missing screenshot is
written and fails the call, or, with `E2E_UPDATE_SNAPSHOTS=1`, written and passed. Only the web engine captures pixels.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [ScreenshotOptions](/api/E2E/ScreenshotOptions/) |  |
| `callerFile` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L634)

<a id="E2E_ScreenExpect_ToHaveScreenshotAsync_System_String_E2E_ScreenshotOptions_System_String_"></a>

### ToHaveScreenshotAsync(string?, ScreenshotOptions?, string?)


```csharp
public Task ToHaveScreenshotAsync(string? name = null, ScreenshotOptions? options = null, string? callerFile = null)
```


Waits for the screen to look like its stored screenshot, `<test file>-snapshots/<name>-<target>-<os>.png`
beside the file this call is in. &lt;code class="paramref"&gt;name&lt;/code&gt; defaults to the test's title and a count. A missing screenshot is
written and fails the call, or, with `E2E_UPDATE_SNAPSHOTS=1`, written and passed. Only the web engine captures pixels.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `options` | [ScreenshotOptions](/api/E2E/ScreenshotOptions/) |  |
| `callerFile` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L627)
