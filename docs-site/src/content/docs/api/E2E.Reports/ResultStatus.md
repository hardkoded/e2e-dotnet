---
title: "ResultStatus"
description: "How a test ended, over all its attempts. A pass after an earlier attempt is Flaky, and a retry that was interrupted keeps the Failed or TimedOut verdict of t..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ResultStatus"
  hidden: true
---

<a id="E2E_Reports_ResultStatus"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public enum ResultStatus
```


How a test ended, over all its attempts. A pass after an earlier attempt is [Flaky](#E2E_Reports_ResultStatus_Flaky), and a retry that was interrupted keeps the [Failed](#E2E_Reports_ResultStatus_Failed) or [TimedOut](#E2E_Reports_ResultStatus_TimedOut) verdict of the attempt before it.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L249)

## Fields

<a id="E2E_Reports_ResultStatus_Failed"></a>

### Failed


```csharp
Failed = 2
```


**Value:** [ResultStatus](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L253)

<a id="E2E_Reports_ResultStatus_Flaky"></a>

### Flaky


```csharp
Flaky = 1
```


**Value:** [ResultStatus](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L252)

<a id="E2E_Reports_ResultStatus_Interrupted"></a>

### Interrupted


```csharp
Interrupted = 4
```


**Value:** [ResultStatus](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L255)

<a id="E2E_Reports_ResultStatus_Passed"></a>

### Passed


```csharp
Passed = 0
```


**Value:** [ResultStatus](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L251)

<a id="E2E_Reports_ResultStatus_Skipped"></a>

### Skipped


```csharp
Skipped = 5
```


**Value:** [ResultStatus](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L256)

<a id="E2E_Reports_ResultStatus_TimedOut"></a>

### TimedOut


```csharp
TimedOut = 3
```


**Value:** [ResultStatus](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L254)
