---
title: "IConfigurableReporter"
description: "A Reporter that takes the options of its e2e.config.json entry. .NET-only: upstream's config lists reporter objects built in code (github(options)), and a JS..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IConfigurableReporter"
  hidden: true
---

<a id="E2E_Reports_IConfigurableReporter"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public interface IConfigurableReporter
```


A [Reporter](/api/E2E.Reports/Reporter/) that takes the `options` of its `e2e.config.json` entry. .NET-only: upstream's config lists reporter
objects built in code (`github(options)`), and a JSON entry holds no code, so the options arrive as JSON.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Reporter.cs)

## Methods

<a id="E2E_Reports_IConfigurableReporter_Configure_"></a>
<a id="E2E_Reports_IConfigurableReporter_Configure_System_Text_Json_JsonElement_"></a>

### Configure(JsonElement)


```csharp
void Configure(JsonElement options)
```


Called once, right after the reporter is built, and only when the entry has `options`. The reporter rejects content it does not
know with a clear error that names the key, never the value: `e2e.config.json` is committed, so options hold no tokens.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement) |  |
