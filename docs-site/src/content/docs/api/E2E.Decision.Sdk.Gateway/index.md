---
title: "E2E.Decision.Sdk.Gateway"
description: "Public types in the `E2E.Decision.Sdk.Gateway` namespace."
editUrl: false
sidebar:
  label: "E2E.Decision.Sdk.Gateway"
---

[API reference](/api/)

Public types in the `E2E.Decision.Sdk.Gateway` namespace.

## Classes

| Type | Summary |
| --- | --- |
| [GatewayDecisionProvider](/api/E2E.Decision.Sdk.Gateway/GatewayDecisionProvider/) | .NET-only: the AI SDK's createGateway decision-model factory, served by the Vercel.AI package's GatewayProvider instead of a port inside E2E.Decision. It makes decisionModel (and the deprecated evaluationModel) return an IDecisionModel that ExperimentalDecide and the decision executor accept. The rest of the Gateway (language, realtime, batch, spend, credits) is Vercel.AI's: reach it through Gateway. |
