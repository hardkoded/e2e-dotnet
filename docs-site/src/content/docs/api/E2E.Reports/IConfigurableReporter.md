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
