---
title: "ActionSource"
description: "Who chose an action an ActionEvent reports."
editUrl: false
prev: false
next: false
sidebar:
  label: "ActionSource"
  hidden: true
---

<a id="E2E_ActionSource"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public enum ActionSource
```


Who chose an action an [ActionEvent](/api/E2E/ActionEvent/) reports.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L983)

## Fields

<a id="E2E_ActionSource_Model"></a>

### Model


```csharp
Model = 0
```


The executor chose the action.

**Value:** [ActionSource](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L986)

<a id="E2E_ActionSource_Replay"></a>

### Replay


```csharp
Replay = 1
```


The replay cache replayed the action.

**Value:** [ActionSource](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L989)
