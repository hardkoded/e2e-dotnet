# E2E.Decision

Decision-model support (the decide call and the OpenAI Decisions API model, not yet the executor or the filter) for the community [`E2E`](https://www.nuget.org/packages/E2E) port of the [e2e](https://github.com/tester-army/e2e) agentic testing framework. It ports `@e2e-dev/decision`. Not an official TesterArmy product.

A decision model answers `choice`, `boolean`, and `score` questions with probabilities instead of generating text. This package holds the decision-model abstraction (`IDecisionModel`, `Decide`), the step-budget accounting around a model call, and the OpenAI Decisions API provider (`OpenAIDecisionModel`). The decision executor and the decision test filter build on it.

```
dotnet add package E2E.Decision
```
