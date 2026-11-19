# E2E.Decision

Decision-model support (the decide call, the decision executor, the decision test filter, and the OpenAI Decisions API model) for the community [`E2E`](https://www.nuget.org/packages/E2E) port of the [e2e](https://github.com/tester-army/e2e) agentic testing framework. It ports `@e2e-dev/decision`. Not an official TesterArmy product.

A decision model answers `choice`, `boolean`, and `score` questions with probabilities instead of generating text. This package holds the decision-model abstraction (`IDecisionModel`, `Decide`), the step-budget accounting around a model call, and the OpenAI Decisions API provider (`OpenAIDecisionModel`). `Executor.DecisionExecutor` is a step executor that acts through a decision model and a small text model; set it as `AgentOptions.Executor`. `Filter.DecisionFilter` is a test filter that asks a decision model which tests a change can affect; give it to the run fixture's `Filters`.

```
dotnet add package E2E.Decision
```
