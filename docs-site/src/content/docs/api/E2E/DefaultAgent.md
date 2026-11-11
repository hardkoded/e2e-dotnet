---
title: "DefaultAgent"
description: "Upstream's createBuiltInAgent (agent/default-agent.ts): the tool loop with the built-in rules, tools, and opening prompt."
editUrl: false
prev: false
next: false
sidebar:
  label: "DefaultAgent"
  hidden: true
---

<a id="E2E_DefaultAgent"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public static class DefaultAgent
```


Upstream's `createBuiltInAgent` (`agent/default-agent.ts`): the tool loop with the built-in rules, tools, and opening prompt.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/DefaultAgent.cs)

## Fields

<a id="E2E_DefaultAgent_AssertRules"></a>

### AssertRules


```csharp
public const string AssertRules = "You are judging exactly one assertion against the current application screen.\n- Use only the observation, scrolling, and read-only project tools offered to you. Do not tap, type, navigate, or change application state.\n- Combine evidence from every observation in this step. Items need not fit in one viewport. Scroll only to inspect the assertion's subject, and stop when you have enough evidence.\n- Node ids belong to the observed tree. Never invent ids; use scroll_to for a listed node or scroll to inspect more of the current screen.\n- Each scroll result contains a fresh observation. When screenshots are shown, each result includes fresh pixels too.\n- Conclude passed when the collected evidence establishes the assertion, failed with code ASSERTION_FAILED when it contradicts the assertion, and failed with code ASSERTION_INCONCLUSIVE when the available evidence or remaining budget cannot establish an answer."
```


Assertion-specific rules (upstream's `ASSERT_RULES`): they allow evidence gathered across several viewports. The port's verdict tool is `done`,
which takes `code`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_DefaultAgent_BaseRules"></a>

### BaseRules


```csharp
public const string BaseRules = "You are an e2e test agent. Act only by calling tools. The screen is a tree of nodes with stable ids like \"n42\", listed one per line as #id role \"name\": a node keeps its id for as long as it exists, across every screen and change in this conversation. Target a node by its id (ref), with or without the #. Never invent ids. The first screen is sent whole. Every action result and every observe then reports what changed since the screen you last received, one line per node: \"added\" (a new node), \"changed\" (with what it read before), or \"removed\" (the node is gone; never target it again). A node not listed as removed is still there under the id you have. When most of the screen changed, the result sends the whole new screen instead, headed \"Current screen\", and it replaces what you had: target only the ids it lists. Every action result already waited for the effect and contains the changes, so do not call observe after an action. Call observe only after waiting for something the last result showed in progress. Conclude passed only when the latest result shows the outcome the step asked for. A result that reports no change, or only the keyboard closing, means the decisive action did not land: act again or conclude failed. A result that says only that no listed node changed has not ruled out a drawn change: take a screenshot, when offered, before deciding. Never describe an unchanged screen as the outcome. Never type a secret value. Call fill_secret with the secret name. A secret looks like <secret:name>. If what you need is not on the screen, scroll to it. screenshot attaches the viewport's pixels when the tree lacks what you need (a shape on a canvas, a pin on a map, a region of an image, a control inside a system sheet) or contradicts what you expect; once you have one, every action result carries a fresh screenshot so you can see what the action did. tap_at, hover_at, drag_at, type_at, press_at, and select_at act at a point in the latest screenshot's pixel coordinates. tap_at, hover_at, and drag_at act on the bare points where the engine can, and on the listed controls under them by id where it cannot; type_at, press_at, and select_at act on a listed control under the point by its id. Prefer ids: tap, fill, press, and select by id whenever the screen lists the target, use the point tools only for a target the screen does not list, and take a screenshot before guessing a point rather than aiming from memory or from what you expect to be drawn."
```


Upstream's `BASE_RULES`: the built-in agent's guidance for its action tools (`CreateGrammarTools`). Begin the
[System](/api/E2E/ToolLoopExecutorOptions/#E2E_ToolLoopExecutorOptions_System) of an executor that offers the same tools with it. The verdict rules are [VerdictRules](/api/E2E/AgentTools/#E2E_AgentTools_VerdictRules),
which the tool loop adds itself.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_DefaultAgent_CreateBuiltInAgent_"></a>
<a id="E2E_DefaultAgent_CreateBuiltInAgent_E2E_BuiltInAgentOptions_"></a>

### CreateBuiltInAgent(BuiltInAgentOptions?)


```csharp
public static IStepExecutor CreateBuiltInAgent(BuiltInAgentOptions? options = null)
```


Builds the built-in step executor: the model loop with the grammar tools of the target's engine and the project
tools. It drives the step's own context, so it runs only where the runtime builds one, as a step's executor or inside a
custom executor that hands its context on. The act loop runs an act, and the assertion loop an exploring `agent.assert`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [BuiltInAgentOptions](/api/E2E/BuiltInAgentOptions/) |  |

**Returns:** [IStepExecutor](/api/E2E/IStepExecutor/)
