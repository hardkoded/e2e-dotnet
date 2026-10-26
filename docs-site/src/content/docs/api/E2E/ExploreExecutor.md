---
title: "ExploreExecutor"
description: "The explorer: the built-in agent with exploration guidance appended to the project's and one tool added to the project's vocabulary, reportfinding. Nothing e..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExploreExecutor"
  hidden: true
---

<a id="E2E_ExploreExecutor"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public static class ExploreExecutor
```


The explorer: the built-in agent with exploration guidance appended to the project's and one tool added to the project's
vocabulary, `report_finding`. Nothing else changes: budgets, loop guards, wind-down, secrets, origin policy, and the
transcript are the harness's, and each exploration step runs as one ordinary `ActAsync` step.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Executor.cs#L23)

## Fields

<a id="E2E_ExploreExecutor_FindingToolName"></a>

### FindingToolName


```csharp
public const string FindingToolName = "report_finding"
```


The name of the tool the explorer reports a defect with.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Executor.cs#L26)
