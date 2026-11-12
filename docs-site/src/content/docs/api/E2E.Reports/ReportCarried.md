---
title: "ReportCarried"
description: "What earlier runs left unresolved that a --last-failed rerun did not run again (upstream's run.carried): their results as they reported them, and the suite h..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportCarried"
  hidden: true
---

<a id="E2E_Reports_ReportCarried"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ReportCarried : IEquatable<ReportCarried>
```


What earlier runs left unresolved that a `--last-failed` rerun did not run again (upstream's `run.carried`): their results as
they reported them, and the suite hook failures whose scope still holds one of them. The next `--last-failed` selects these tests too.
Not part of the run's status, exit code, or summary. The port has no serial groups, so the block's `serialGroups` is always empty and not modelled.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReportCarried&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs)

## Properties

<a id="E2E_Reports_ReportCarried_Errors_"></a>
<a id="E2E_Reports_ReportCarried_Errors"></a>

### Errors


```csharp
public IReadOnlyList<ReportError> Errors { get; init; }
```


The suite hook failures whose scope still holds a carried result.

**Value:** [IReadOnlyList&lt;ReportError&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Reports_ReportCarried_Results_"></a>
<a id="E2E_Reports_ReportCarried_Results"></a>

### Results


```csharp
public IReadOnlyList<ReportResult> Results { get; init; }
```


The results as the run that last ran them reported them; at least one.

**Value:** [IReadOnlyList&lt;ReportResult&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
