---
title: "Filter"
description: "Upstream's filter.ts."
editUrl: false
prev: false
next: false
sidebar:
  label: "Filter"
  hidden: true
---

<a id="E2E_Decision_Filter"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public static class Filter
```


Upstream's `filter.ts`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/obj/Release/net10.0/System.Text.RegularExpressions.Generator/System.Text.RegularExpressions.Generator.RegexGenerator/RegexGenerator.g.cs)

## Methods

<a id="E2E_Decision_Filter_DecisionFilter_"></a>
<a id="E2E_Decision_Filter_DecisionFilter_E2E_Decision_DecisionFilterOptions_"></a>

### DecisionFilter(DecisionFilterOptions)


```csharp
public static ITestFilter DecisionFilter(DecisionFilterOptions options)
```


Upstream's `decisionFilter`. A test filter named `decision` that asks the decision model, test by test, whether the change
can affect what it checks, and whether it can break the setup that saves the test's session, and leaves out the tests unlikely on
both counts. A change to the config the run loaded, any file named like an e2e config, or a lockfile leaves nothing out, and a test
whose test file, setup file, or stored screenshots changed always runs. A test the model does not judge runs, and the note says
why. It also scores every test it is handed, so the run starts the likeliest to fail first: 1 for a test the change touches, the
model's probability for one it judged, and 0.5 for one it did not. When every call fails, as without a key, or git or a file read
fails, the filter throws a [TestFilterException](/api/E2E/TestFilterException/), so the run warns and runs every test; the exception carries the
scores the change alone gives, so the tests it touches still start first.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [DecisionFilterOptions](/api/E2E.Decision/DecisionFilterOptions/) |  |

**Returns:** [ITestFilter](/api/E2E/ITestFilter/)
