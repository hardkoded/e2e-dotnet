---
title: "E2E"
description: "The core. Agent, Screen, Expect, the engines, model clients, config, and the replay cache."
editUrl: false
sidebar:
  label: "E2E"
---

[API reference](/api/)

The core. Agent, Screen, Expect, the engines, model clients, config, and the replay cache.

## Classes

| Type | Summary |
| --- | --- |
| [ActionEvent](/api/E2E/ActionEvent/) | One completed action of an agent.act step, as OnAction receives it. |
| [ActionOptions](/api/E2E/ActionOptions/) | Options for one locator action. |
| [ActOptions](/api/E2E/ActOptions/) | Values passed into one ActAsync goal. Put fresh data in Unique(string). |
| [ActResult](/api/E2E/ActResult/) |  |
| [Agent](/api/E2E/Agent/) | One goal (ActAsync) or a check against the current screen. |
| [AgentAttribute](/api/E2E/AgentAttribute/) | Pins the agent that the calls of a test, or of every test in a class, use when they name none. Upstream's agent option of test and describe. The closest pin wins: a call's Agent, then the method, then the class, then an enclosing or base class, then the E2E_AGENT environment variable. Base and enclosing classes count although the attribute is not inherited: TestAgentOptions reads each layer itself. An unknown name fails the test with INVALID_ARGUMENT. |
| [AgentConfig](/api/E2E/AgentConfig/) | One entry of agents. Upstream model and judge are AI SDK instances; here they are model ids for Provider, which share BaseUrl and ApiKeyEnv. Each entry starts from the defaults; none inherits another's values. |
| [AgentContextAttribute](/api/E2E/AgentContextAttribute/) | Appends facts only this test knows to the context of every agent, after the agent's own context. Upstream's agentContext option of test and describe. Layers join outer to inner, one per line. Keep instructions out of it: it reaches the judges too. |
| [AgentException](/api/E2E/AgentException/) | An agent step failed, was blocked, or could not talk to its model. |
| [AgentOptions](/api/E2E/AgentOptions/) | One named agent, as upstream agents.<name>. Each entry starts from the built-in defaults; no agent inherits another's values. A call picks one with its Agent option. |
| [AgentTool](/api/E2E/AgentTool/) | A project tool the agent offers to the model beside its own: upstream's defineTool. It declares whether it changes the application and which platforms it is offered on; the agent owns dispatch and accounting. The tool's name is its key in Tools. |
| [AgentToolContext](/api/E2E/AgentToolContext/) | What a project tool reaches of the step it runs in (upstream's ToolContext): a look at the screen, and a screenshot kept as evidence of the step. A tool that changes the app may not look: it observes in a separate call. |
| [AnthropicModel](/api/E2E/AnthropicModel/) | Anthropic Messages API client. The system prompt and the newest message carry cache breakpoints, as upstream marks them, so each turn of a tool loop reads the previous turn's prefix from the cache. |
| [AnthropicModelOptions](/api/E2E/AnthropicModelOptions/) |  |
| [App](/api/E2E/App/) | Opens and resets the app under test. A relative URL is resolved against BaseUrl; no URL opens the base URL. |
| [AppConfig](/api/E2E/AppConfig/) |  |
| [AssertOptions](/api/E2E/AssertOptions/) | agent.assert: one judgment and one repair round, within Timeout. |
| [AwsCredentials](/api/E2E/AwsCredentials/) | AWS access key credentials for Signature Version 4. |
| [BedrockModel](/api/E2E/BedrockModel/) | Amazon Bedrock Converse API client. It signs with AWS Signature Version 4 from the standard AWS environment variables or, with none set, from the AWS credential chain (shared profile, SSO, container and instance roles), as the AI SDK's amazon-bedrock provider does, or sends a Bedrock API key (AWS_BEARER_TOKEN_BEDROCK) as a bearer token when one is set. |
| [BedrockModelOptions](/api/E2E/BedrockModelOptions/) |  |
| [Browser](/api/E2E/Browser/) | The browser the attempt drives, showing one active tab. It needs an engine session that implements IBrowserSession, such as WebEngine; on any other engine each member fails with UNSUPPORTED_CAPABILITY. |
| [BrowserExpect](/api/E2E/BrowserExpect/) | Polling assertions for the Browser: its URL and its title. Like the locator matchers, a passing one verifies the previous agent.act. A read is bounded by the matcher's own deadline, so a page that stops answering ends the assertion at its timeout. |
| [BrowserKeyboard](/api/E2E/BrowserKeyboard/) | Keyboard input for whatever holds focus in the active tab. A call the page does not answer within the action timeout fails with ACTION_MAY_HAVE_COMMITTED: the input may have reached the page. |
| [BrowserMouse](/api/E2E/BrowserMouse/) | Pointer input for the active tab, in CSS pixels. A call the page does not answer within the action timeout fails with ACTION_MAY_HAVE_COMMITTED: the input may have reached the page. |
| [BuiltInAgentOptions](/api/E2E/BuiltInAgentOptions/) | What the built-in agent is built from: an agents entry's system and its tools (upstream's BuiltInAgentOptions). |
| [CacheConfig](/api/E2E/CacheConfig/) |  |
| [CacheEntry](/api/E2E/CacheEntry/) | A recording of one verified act. Its file name is the hash of a trace cache key (TraceCacheKeyHash): the cache schema, the replay policy version, the project, the test, the target, the engine's name and contract version, the app's identity, the instruction's and params' digests, the agent's name, a digest of the agent's redacted context, and which repeat of that same call in the attempt it is. The model id and the engine's package version are not part of the key. |
| [CacheInfo](/api/E2E/CacheInfo/) |  |
| [CacheLookup](/api/E2E/CacheLookup/) |  |
| [ClickOptions](/api/E2E/ClickOptions/) | Options for TapAsync, DoubleTapAsync, and SecondaryTapAsync. Upstream's ClickOptions. |
| [CommandConfig](/api/E2E/CommandConfig/) | The command a target's app.command starts the app with (upstream's CommandConfig). It is a program and its arguments, never a shell line: the executable is started directly and each argument is passed as it is. |
| [ConfigurationException](/api/E2E/ConfigurationException/) | e2e.config.json is invalid. The code is INVALID_CONFIG. |
| [Credentials](/api/E2E/Credentials/) | Reads E2E_USER_{NAME}_USERNAME and E2E_USER_{NAME}_PASSWORD. The name is uppercased and dashes become underscores. |
| [DefaultAgent](/api/E2E/DefaultAgent/) | Upstream's createBuiltInAgent (agent/default-agent.ts): the tool loop with the built-in rules, tools, and opening prompt. |
| [E2EConfig](/api/E2E/E2EConfig/) | The resolved e2e.config.json. The JSON shape follows upstream e2e.config.ts: targets[].app.url, named agents, top-level timeouts in milliseconds, retries, cache, and secrets. Unknown keys fail with INVALID_CONFIG. E2ETest finds and applies this file; a fixture can still override each value. There is no default model. |
| [E2EDefaults](/api/E2E/E2EDefaults/) | The upstream defaults, in one place. E2EConfig, E2ESessionOptions, and the NUnit fixture all start from these values. |
| [E2EException](/api/E2E/E2EException/) | Base exception for the agent and engines. Code is stable. |
| [E2EFixture](/api/E2E/E2EFixture/) | The part of a test base class that every test framework shares. It holds the settings and starts one E2ESession per test. A framework adapter, such as E2E.NUnit.E2ETest, calls StartSessionAsync before the test body and commits the session from the framework's result after it. &lt;p&gt; Settings come from Config, the nearest e2e.config.json above the test assembly or the working directory. Override a property to change one value for a fixture. &lt;/p&gt; |
| [E2EHost](/api/E2E/E2EHost/) | The worker-scoped owner of preparation, app processes, and engine initialization, that opens one isolated session at a time. Upstream's E2EHost (run/open-session.ts). Open one with OpenAsync. |
| [E2ESession](/api/E2E/E2ESession/) | One test attempt: an engine session, the fixtures, and the replay cache. Call Complete after the test body. Disposing without Complete records the attempt as failed. |
| [E2ESessionAttribute](/api/E2E/E2ESessionAttribute/) | Starts a test, or every test of a class, from a session a setup test saved. Upstream's session option of test and describe. The session restores before the test body runs; a restore leaves no page open, so the test opens one first. The setup test must run before the test (in NUnit, give it an earlier [Order]); a session no setup produced fails the test with SESSION_UNAVAILABLE, and an engine that cannot restore state with UNSUPPORTED_CAPABILITY. The closest attribute wins: the method, then the class, then a base class. .NET-only name, as the port has no test() options object. |
| [E2ESessionOptions](/api/E2E/E2ESessionOptions/) | How to open one E2ESession. Retries set Attempt above 1 so the cache records but does not replay, except under CacheStrict, where every attempt replays. |
| [EngineErrorCodes](/api/E2E/EngineErrorCodes/) | Stable engine error codes, as in the upstream engine contract. |
| [EngineException](/api/E2E/EngineException/) | The engine refused an operation or could not drive the app. |
| [EngineState](/api/E2E/EngineState/) | One captured engine state (upstream's EngineState, engine/surface.ts): an opaque, versioned payload an engine can save from a session and restore into another. ExpiresAt is an RFC 3339 timestamp after which the state must not be restored. |
| [ErrorDetails](/api/E2E/ErrorDetails/) | Structured facts of one failure, beside its prose message (upstream's ErrorDetails, internal/errors.ts). An assertion carries what it expected and observed and how many nodes matched. A locator failure carries the locator as written, what it asked for, and how long it waited. A report bounds the text fields. |
| [ExecutorAttempt](/api/E2E/ExecutorAttempt/) | The attempt a step belongs to: identity and lifecycle. It lets an executor keep state across the ActAsync calls of one test, a running conversation or a plan or notes, without mixing it with the next test's or a retry's. |
| [ExecutorModelCall](/api/E2E/ExecutorModelCall/) | Usage detail of one model call an executor made. Every field is optional. |
| [ExecutorNode](/api/E2E/ExecutorNode/) | One node of the redacted semantic tree. Names, text, values, and attribute values are secret-redacted, and a secure node carries no value at all. Id is the node's ref in the observation, which an action names it by. |
| [ExecutorObservation](/api/E2E/ExecutorObservation/) | A redacted, size-bounded observation an executor may show its model. |
| [ExecutorObserveOptions](/api/E2E/ExecutorObserveOptions/) | What an executor asks ObserveAsync to include beyond the text. |
| [ExecutorPixels](/api/E2E/ExecutorPixels/) | Masked viewport pixels cleared for model input: the engine's capture, sized for the model, and its proven mask count. |
| [ExecutorSecret](/api/E2E/ExecutorSecret/) | A secret declared in a step's params: its stable name and what it is for. Never its value. |
| [ExecutorStep](/api/E2E/ExecutorStep/) | One step handed to an executor. An Act plans and executes a flow. An Assert judges a condition and must not change the app. |
| [ExecutorTarget](/api/E2E/ExecutorTarget/) | A node named by its id from the newest observation (upstream's { id }). The id is the node's ref in the text and in Id. A ref the screen no longer lists fails the action instead of acting on the wrong node. |
| [ExecutorTargetInfo](/api/E2E/ExecutorTargetInfo/) | The target a step runs on. Tools scope themselves by Platform, and Verbs is the part of the action grammar the engine declared, so an executor offers a model exactly the vocabulary the surface can honor. |
| [Expect](/api/E2E/Expect/) | Polling assertions for a Locator, expect.soft, and expect.poll. A passing locator assertion verifies the previous agent.act. Plain value matchers are not ported: use NUnit Assert.That, and Assert.EnterMultipleScope for soft value checks. |
| [Explore](/api/E2E/Explore/) | An exploration: a run whose one test is a goal. The config is loaded as a test run loads it, the exploration body runs as the run's only test, and within that test alone the explorer takes the place of the agent the run uses. The exploration's progress reaches OnProgress, and its record is Explore. |
| [ExploreBounds&lt;T&gt;](/api/E2E/ExploreBounds-1/) | The least, the most, and the default of a bound option. Upstream's inline { min, max, default }. |
| [ExploreBudgets](/api/E2E/ExploreBudgets/) | What an exploration was given to spend: its step cap and its wall clock. |
| [ExploreExecutor](/api/E2E/ExploreExecutor/) | The explorer: the built-in agent with exploration guidance appended to the project's and one tool added to the project's vocabulary, report_finding. Nothing else changes: budgets, loop guards, wind-down, secrets, origin policy, and the transcript are the harness's, and each exploration step runs as one ordinary ActAsync step. |
| [ExploreOptions](/api/E2E/ExploreOptions/) | What ExploreAsync explores, as, and with. |
| [ExploreOutcome](/api/E2E/ExploreOutcome/) | What an exploration ended with. Upstream's ExploreOutcome, which extends the runner's RunOutcome. |
| [ExploreProgress](/api/E2E/ExploreProgress/) | The exploration's progress, one moment at a time: the step in progress, each finding the moment it is reported, and the end. ReportExplore is the record; these are its moments. Upstream carries them as explore run events, which its list reporter renders; the port has no run events, so OnProgress receives them. |
| [ExploreProgress.FindingReported](/api/E2E/ExploreProgress.FindingReported/) | One finding, as recorded; its evidence screenshot, when kept, is the one ArtifactId names. Upstream's finding phase. |
| [ExploreProgress.Finished](/api/E2E/ExploreProgress.Finished/) |  |
| [ExploreProgress.Planning](/api/E2E/ExploreProgress.Planning/) | The planner is deciding the next charter, or (Closing) the assessment that ends the run. |
| [ExploreProgress.Started](/api/E2E/ExploreProgress.Started/) | The exploration begins: its test has started and the app is about to open. |
| [ExploreProgress.StepFinished](/api/E2E/ExploreProgress.StepFinished/) |  |
| [ExploreProgress.StepStarted](/api/E2E/ExploreProgress.StepStarted/) |  |
| [ExtractOptions](/api/E2E/ExtractOptions/) | agent.extract: one extraction and one repair round. The type argument of ExtractAsync&lt;T&gt; is the schema. |
| [FileStepCache](/api/E2E/FileStepCache/) | JSON files in a directory, one per cache key. Entries over 1 MiB are ignored. |
| [FrameScreen](/api/E2E/FrameScreen/) | The screen scope FrameLocator(string) returns. Every Screen query answers from inside the frame, and the web-only escape hatches follow it in: Locator(string) for a CSS or XPath selector on a control with no accessible name, and FrameLocator(string) for a frame nested inside this one. Upstream's FrameScreen. |
| [GoogleModel](/api/E2E/GoogleModel/) | Google Gemini API (Generative Language) client. Tool schemas go as parametersJsonSchema, so the agent's JSON Schemas need no conversion. Thought signatures Gemini attaches to a function call are sent back with it. |
| [GoogleModelOptions](/api/E2E/GoogleModelOptions/) |  |
| [GotoOptions](/api/E2E/GotoOptions/) | Options of GotoAsync. |
| [Locator](/api/E2E/Locator/) | A query that resolves against a fresh observation every time it is used. |
| [LocatorExpect](/api/E2E/LocatorExpect/) | Matchers that retry until they pass or the assertion timeout elapses. Each takes an optional timeout that replaces the assertion timeout for that call. |
| [LocatorExpression](/api/E2E/LocatorExpression/) | A locator expression. Query kinds (role, text, label, testid, placeholder, displayValue) search the descendants of Parent when it is set. filter and index narrow Source. union matches either Left or Right, each node once, in document order. selector is a CSS or XPath selector in the engine's own language, and frame scopes Source into the iframe Selector names. |
| [LocatorWaitForOptions](/api/E2E/LocatorWaitForOptions/) | Options for WaitForAsync. |
| [LongPressOptions](/api/E2E/LongPressOptions/) | Options for LongPressAsync. |
| [ModelCallUsage](/api/E2E/ModelCallUsage/) |  |
| [ModelImage](/api/E2E/ModelImage/) | An image a model reads: the encoded bytes of a screenshot, already sized for the model. |
| [ModelMessage](/api/E2E/ModelMessage/) |  |
| [ModelProviders](/api/E2E/ModelProviders/) | Ready-made models for the providers upstream's e2e init and docs name, each reading the key from the environment variable the AI SDK reads. Subscription logins are in Subscriptions. |
| [ModelRequest](/api/E2E/ModelRequest/) |  |
| [ModelResponse](/api/E2E/ModelResponse/) |  |
| [ModelResponses](/api/E2E/ModelResponses/) | Builds the tool calls a ScriptedModel (or a test) returns. |
| [ModelTool](/api/E2E/ModelTool/) |  |
| [ModelToolCall](/api/E2E/ModelToolCall/) |  |
| [OpenAiCompatibleModel](/api/E2E/OpenAiCompatibleModel/) | Chat-completions client for OpenAI and any server that speaks the same tool-call JSON. Set BaseUrl to a local server such as http://127.0.0.1:11434/v1. The API key is optional for servers that do not check one. ModelProviders has presets for OpenRouter, the Vercel AI Gateway, SpaceXAI, and Ollama. |
| [OpenAiCompatibleModelOptions](/api/E2E/OpenAiCompatibleModelOptions/) |  |
| [OpenAiResponsesModel](/api/E2E/OpenAiResponsesModel/) | OpenAI Responses API client: OpenAI itself, Azure OpenAI (AzureOpenAi), and the subscription backends that only speak Responses. Nothing is stored server side, so each turn carries the whole conversation. A server-sent-event answer is folded back into the completed response. |
| [OpenAiResponsesModelOptions](/api/E2E/OpenAiResponsesModelOptions/) |  |
| [OpenedStep](/api/E2E/OpenedStep/) | A step as opened: title and charter bounded to the report's ceilings, which is what the step runs with. |
| [OpenHostOptions](/api/E2E/OpenHostOptions/) | What OpenAsync is given. Upstream's OpenHostOptions (run/open-session.ts). |
| [OpenSessionOptions](/api/E2E/OpenSessionOptions/) | What OpenSessionAsync is given. Upstream's OpenSessionOptions (run/open-session.ts). |
| [PollExpectation&lt;T&gt;](/api/E2E/PollExpectation-1/) | Re-reads a value until the chosen matcher holds or the timeout passes. A read that throws is retried, and its error is reported if the poll times out. A matcher that refuses the read value's type counts as not yet. With NUnit, ToMatchAsync from E2E.NUnit takes any NUnit constraint. |
| [PollOptions](/api/E2E/PollOptions/) | How Poll&lt;T&gt; re-reads a value. |
| [PreparedMessages](/api/E2E/PreparedMessages/) | What PrepareMessages hands back: the history for the next turn, and optionally a reason to stop. The executor has evidence the loop guards cannot see that the step is not progressing. A stop leaves the model only done, like a loop guard. |
| [PreparedTurn](/api/E2E/PreparedTurn/) | What PrepareMessages learns about the turn that is about to run. |
| [PressSequentiallyOptions](/api/E2E/PressSequentiallyOptions/) | Options for PressSequentiallyAsync. |
| [RecordedAction](/api/E2E/RecordedAction/) |  |
| [RecordedTarget](/api/E2E/RecordedTarget/) |  |
| [RecordedVideo](/api/E2E/RecordedVideo/) | One video StopRecordingAsync(CancellationToken) returns: a file, or a link to a recording a hosted service keeps (upstream's RecordedVideo). |
| [RecordedVideoFile](/api/E2E/RecordedVideoFile/) | A video file under the attempt's recordings/ directory (upstream's RecordedVideoFile). |
| [RecordedVideoLink](/api/E2E/RecordedVideoLink/) | A recording a hosted browser or device service keeps, by URL (upstream's RecordedVideoLink). |
| [RecordingOptions](/api/E2E/RecordingOptions/) | How StartRecordingAsync names its file (upstream's RecordingOptions). |
| [ReplayedPrefix](/api/E2E/ReplayedPrefix/) | Upstream's ReplayedPrefix: the mid-step hand-off from a diverged cache replay. The replayed actions already ran against the live app under the same budgets and recording as the executor's own. The executor continues the step from the current state and does not redo them. |
| [ReportExplore](/api/E2E/ReportExplore/) | What an exploration was asked, did, and found: upstream's run.explore block of the report. |
| [ReportExploreFinding](/api/E2E/ReportExploreFinding/) | One finding Explore recorded through its report_finding tool. |
| [ReportExploreStep](/api/E2E/ReportExploreStep/) | One exploration step: a charter the agent planned and then executed as an ActAsync step. |
| [ResolvedRecording](/api/E2E/ResolvedRecording/) | One effective mode and where it came from. Upstream ResolvedRecording. |
| [RoleOptions](/api/E2E/RoleOptions/) | Role query options. A null state is not checked. |
| [Screen](/api/E2E/Screen/) | Semantic queries against the current screen. An action or a single-node assertion fails when the query matches more than one node. Narrow it with Filter(TextMatch), First(), Last(), or Nth(int). |
| [ScreenExpect](/api/E2E/ScreenExpect/) | Matchers on the whole screen: ToHaveScreenshotAsync. |
| [ScreenshotOptions](/api/E2E/ScreenshotOptions/) | Options of ToHaveScreenshotAsync. |
| [ScriptedModel](/api/E2E/ScriptedModel/) | Returns whatever the callback decides for each request. Records every request. A callback that serves both the act loop and the judge may answer a judgment (a request with a Schema) with Done: passed reads as holds, a failed without the code ASSERTION_INCONCLUSIVE as fails, and a blocked or inconclusive one as inconclusive. Judgment answers it directly. |
| [ScrollUntilVisibleOptions](/api/E2E/ScrollUntilVisibleOptions/) | Options for Screen.ScrollUntilVisibleAsync and Locator.ScrollUntilVisibleAsync. |
| [Secret](/api/E2E/Secret/) | A value the model must not see. Prompts, transcripts, reports, and the names and test ids an act records in its replay cache entry receive <secret:name>. The agent fills the real value itself. |
| [Secrets](/api/E2E/Secrets/) | The secrets declared in e2e.config.json. Each value comes from E2E_SECRET_{NAME} when it is set, else from the config. The name is uppercased and every character other than A-Z and 0-9 becomes an underscore. |
| [SkipException](/api/E2E/SkipException/) | Thrown by Skip when a test stops early and is reported skipped. |
| [SoftBrowserExpect](/api/E2E/SoftBrowserExpect/) | The browser matchers of BrowserExpect, but an ASSERTION_FAILED is kept on the session instead of thrown, as SoftLocatorExpect does. |
| [SoftLocatorExpect](/api/E2E/SoftLocatorExpect/) | The locator matchers of LocatorExpect, but an ASSERTION_FAILED is kept on the session instead of thrown, and the test body runs on. Any other error, such as LOCATOR_AMBIGUOUS, still throws. |
| [SoftScreenExpect](/api/E2E/SoftScreenExpect/) | The screen matchers of ScreenExpect, but an ASSERTION_FAILED is kept on the session instead of thrown, as SoftLocatorExpect does. |
| [StepExecutorContext](/api/E2E/StepExecutorContext/) | What an executor receives for one step (upstream's StepExecutorContext). The runtime builds it. A test of an executor can build its own and override the members it reads: each one that does work throws until it is overridden. |
| [StepExecutors](/api/E2E/StepExecutors/) | Upstream's RUNTIME_CODES, BLOCKABLE_CODES and isRuntimeHardStop (agent/executor.ts). |
| [StepTurn](/api/E2E/StepTurn/) | One model turn of an agent step: the tool calls the model made and what came back, each bounded (upstream's StepTurn). |
| [StoredArtifact](/api/E2E/StoredArtifact/) | One artifact handed to PutAsync (upstream's StoredArtifact): the file's bytes, its digest, and the identity the report files it under. Path is the report-relative path the record carries, stable across runs of the same test, so a host may use it as its own key. |
| [StoredArtifactLink](/api/E2E/StoredArtifactLink/) | One provider-hosted recording, handed to PutLinkAsync once the attempt stopped it (upstream's StoredArtifactLink). |
| [SwipeOptions](/api/E2E/SwipeOptions/) | Options for SwipeAsync. |
| [TargetConfig](/api/E2E/TargetConfig/) | One entry of targets. The port drives the web, ios, and android platforms. |
| [TestContext](/api/E2E/TestContext/) | The fixtures for one test. Call Skip before the first step when a precondition is missing. |
| [TestException](/api/E2E/TestException/) | A locator, expectation, or test failed. |
| [TestFilterCandidate](/api/E2E/TestFilterCandidate/) | One test a filter may leave out: what names it, never its body (upstream's TestFilterCandidate). |
| [TestFilterChanges](/api/E2E/TestFilterChanges/) | What a run's test filters judge: every file that differs from the base commit (upstream's TestFilterChanges). |
| [TestFilterContext](/api/E2E/TestFilterContext/) | What the host hands a filter besides its tests (upstream's TestFilterContext). |
| [TestFilterResult](/api/E2E/TestFilterResult/) | A filter's answer: every test it does not name runs (upstream's TestFilterResult). |
| [TextMatch](/api/E2E/TextMatch/) | A string or a regular expression, the upstream TextMatch, used by both screen queries and expectations. In a query, a string matches exactly by default, and exact: false makes it a case-insensitive substring; a Regex is tested against the whitespace-normalized text and ignores exact. Converts implicitly from both. |
| [TextMatchOptions](/api/E2E/TextMatchOptions/) | Options shared by the text-matching queries. |
| [ToolLoop](/api/E2E/ToolLoop/) | Upstream's createToolLoopExecutor (agent/tool-loop.ts): the chassis of an executor that thinks in a loop of model turns and tool calls. It owns everything but the tool vocabulary: the done verdict tool and its closed code policy, the hard stops (a spent budget, a timeout, or a cancel ends the loop, never the model), the loop guards, the shrink of a history that overflowed the model, and the count of model calls. The built-in agent is this chassis plus the grammar tools (CreateBuiltInAgent(BuiltInAgentOptions)). An executor for a device or an API brings other tools and inherits the same discipline. |
| [ToolLoopExecutorOptions](/api/E2E/ToolLoopExecutorOptions/) | Upstream's ToolLoopExecutorOptions. |
| [ToolLoopHelpers](/api/E2E/ToolLoopHelpers/) | Loop services for the tool bodies that run work of their own (upstream's ToolLoopHelpers). |
| [ToolLoopTool](/api/E2E/ToolLoopTool/) | One tool of a loop's vocabulary: what the model is told, and what runs when it calls it. Upstream's tools are AI SDK tools, which the port has no counterpart of, so this is the .NET shape of one entry of its ToolSet. A tool that throws is read by the model as a failed call, except a hard stop of the step, which ends the loop. |
| [TraceKeyContext](/api/E2E/TraceKeyContext/) | Upstream's TraceKeyContext (cache/trace.ts): every part of a cache key outside the step's own identity, as it was when a step was recorded. An entry stores it as keyedBy: provenance, never read by a replay. When a step's key no longer finds the entry, comparing it with the run's says what changed. |
| [UniqueValue](/api/E2E/UniqueValue/) | A value that changes every run, such as a timestamp or a fresh email. The replay cache treats every UniqueValue as the same param and substitutes the current value when it types. |
| [UserCredential](/api/E2E/UserCredential/) | Username plus a Secret password loaded from the environment. |
| [Values](/api/E2E/Values/) | Wraps a fresh value so the replay cache can still match the step. |
| [WaitForOptions](/api/E2E/WaitForOptions/) | agent.waitFor: a judgment at most once per Interval, and only on a changed screen, until Timeout. |
| [WebResponse](/api/E2E/WebResponse/) | A response WaitForResponseAsync matched. |
| [WebRoute](/api/E2E/WebRoute/) | One request a RouteAsync handler intercepted. Take exactly one decision. Each validates its options before it decides, so a rejected option leaves the request undecided, and the request is aborted. |

## Structs

| Type | Summary |
| --- | --- |
| [StepVerdict](/api/E2E/StepVerdict/) | Upstream's StepVerdict: how an executor ended the step. Failed means the app did not behave as the step required. Blocked means the environment, the credentials, or the executor's own budget prevented a verdict about the app, and it always carries a blockable code. |
| [ToolLoopResult](/api/E2E/ToolLoopResult/) | What a tool call answered: the text the model reads, and whether the call failed, which the loop counts toward its failure streak. |

## Interfaces

| Type | Summary |
| --- | --- |
| [IAgentModel](/api/E2E/IAgentModel/) | One turn of the agent model. Tool calls are executed by the agent, never by the model itself. |
| [IArtifactStore](/api/E2E/IArtifactStore/) | Where artifacts go (upstream's ArtifactStore, artifacts.store). The default keeps them under the artifacts directory, and the report records their paths. A host-supplied store (object storage, an API) receives each artifact an attempt registers (a download, a video, an attached screenshot or log) as it is produced, not after the run, and returns its own reference, which the report records as the artifact's ref beside the local path. The cloud seam for evidence, the way IStepCache is for the replay cache. A failed PutAsync never fails the run: the record simply carries no ref. Like every live value, a store never crosses a process boundary. Set it with ArtifactStore, or override E2EFixture.ArtifactStore. |
| [IExecutorActions](/api/E2E/IExecutorActions/) | Upstream's ExecutorActions, with the verbs the port's engines support. Every verb bottoms out in the runtime, which enforces the deadline and the action budget and records the action for the step's cache entry. A ref is only valid against the newest observation: a stale one fails the action rather than acting on the wrong node. Actions and observations run in call order. An engine failure is thrown as an E2EException, and a hard stop of the step as an AgentException with a runtime code. ClearAsync is a verb the port's grammar has and upstream's does not (upstream clears with type). |
| [IExecutorBudgets](/api/E2E/IExecutorBudgets/) | Step budgets, read and reported by the executor and enforced by the runtime. |
| [ISessionSetup](/api/E2E/ISessionSetup/) | What a framework adapter's setup attribute says about a test: it is a setup test, and it saves these sessions (upstream's test.setup(title, { sessions }, fn)). .NET-only: the adapter declares the attribute, because the test framework owns what a test is; E2E.NUnit.E2ESetupAttribute is the NUnit one. |
| [IStepCache](/api/E2E/IStepCache/) |  |
| [IStepExecutor](/api/E2E/IStepExecutor/) | Upstream's StepExecutor (agent/executor.ts): the part of an act step that thinks. The runtime owns the step's cache, budget, deadline, and verdict; an executor reads the screen, acts through the context, and returns a verdict. Swapping executors changes the thinking, never the safety, the budgets, or the verdict grammar. &lt;p&gt; An executor is trusted project code, in the same trust domain as the test project that builds it: it may hold its own model and credentials. What the runtime enforces against it is accounting, not secrecy: the budgets, the deadline, the recording, and the verdict grammar hold whoever thinks. &lt;/p&gt; |
| [ITestFilter](/api/E2E/ITestFilter/) | Leaves collected tests out of a run before anything starts (upstream's TestFilter, types.ts). A host that selects tests calls it through TestFilters.ApplyTestFilters, at most once. A call that throws, returns an invalid result, or runs past 5 minutes leaves nothing out. .NET-only: the interface prefix and the Async suffix. |

## Enums

| Type | Summary |
| --- | --- |
| [ActionSource](/api/E2E/ActionSource/) | Who chose an action an ActionEvent reports. |
| [AppLogLevel](/api/E2E/AppLogLevel/) | How bad a line the app logged was. |
| [AppLogSource](/api/E2E/AppLogSource/) | Where a line the app logged came from (upstream's AppLogSource). |
| [AppPermissionState](/api/E2E/AppPermissionState/) | What a permission is set to before a launch (upstream's AppPermissionState): held, refused, or not asked for yet, so the OS asks again. |
| [CacheMode](/api/E2E/CacheMode/) | How the replay cache is used. |
| [ExecutorCache](/api/E2E/ExecutorCache/) | Trace-cache participation for an executor's steps. Upstream's cache: 'inherit' \| 'off'. |
| [ExecutorStepKind](/api/E2E/ExecutorStepKind/) | Upstream's ExecutorStep.kind. |
| [ExecutorVerb](/api/E2E/ExecutorVerb/) | One verb of IExecutorActions. The port has the verbs its engines can perform. |
| [GotoWaitUntil](/api/E2E/GotoWaitUntil/) | When GotoAsync counts a navigation as done. |
| [RecordingMode](/api/E2E/RecordingMode/) | Which attempts of a test record a trace or a video. Upstream RecordingMode. In e2e.config.json and in E2E_TRACE and E2E_VIDEO a mode is written off, on, retain-on-failure, on-first-retry, or on-all-retries. |
| [RecordingSource](/api/E2E/RecordingSource/) | Where an effective RecordingMode was set. Upstream RecordingSource. A Run mode (the config root or the environment variable) applies to every engine that can record. A Target or Test mode was asked for by name, so the engine must be able to record it. |
| [ReplayHandOffReason](/api/E2E/ReplayHandOffReason/) | Upstream's ReplayHandOffReason, why a cached replay stopped before it finished its trace. ActionBudget and InvalidEntry are the port's: its replay draws on the step's action budget, and reads a recording back from a store that can hold any shape. |
| [ReportExploreEnded](/api/E2E/ReportExploreEnded/) | Why an exploration stopped. |
| [ReportExploreFindingKind](/api/E2E/ReportExploreFindingKind/) | Whether a finding fails the exploration. |
| [ReportExploreStepStatus](/api/E2E/ReportExploreStepStatus/) | How an exploration step ended. |
| [ReportRunStatus](/api/E2E/ReportRunStatus/) | How a run ended (upstream's BuildReportOptions['status'] \| 'blocked'). |
| [RunStatus](/api/E2E/RunStatus/) | How a run ended. Upstream's RunStatus, with the values a single run in the port can have. |
| [StepVerdictStatus](/api/E2E/StepVerdictStatus/) | Upstream's StepVerdict.status. |
| [VisionDegradation](/api/E2E/VisionDegradation/) | Why a vision call fell back to tree-only input. Pixel evidence degrades rather than fails, except under Only, which fails with POLICY_DENIED naming the reason. |
| [VisionMode](/api/E2E/VisionMode/) | What evidence a judgment (AssertAsync, WaitForAsync, ExtractAsync&lt;T&gt;) is given: the semantic tree, a masked screenshot of the current screen, or both. &lt;p&gt; Only exists because a tree sent beside pixels is a cheaper path to an answer, and a model takes it: asked whether a form is covered by an overlay, it can read from the tree that the form is present and named and answer yes, while the pixels show the overlay. For a judgment about what the screen presents, the tree is a distractor, so the mode that means it removes it. It also costs fewer input tokens than On. &lt;/p&gt; &lt;p&gt; In every mode that sends pixels and the tree, pixel evidence degrades away rather than failing the call when it cannot be proven redacted. Only has nothing to degrade to, so it fails with POLICY_DENIED instead of answering the wrong question from the tree. &lt;/p&gt; &lt;p&gt; ActAsync takes no vision mode: the act model works from the tree. &lt;/p&gt; |
| [WaitForState](/api/E2E/WaitForState/) | The state WaitForAsync waits for. |

## Delegates

| Type | Summary |
| --- | --- |
| [ActionHook](/api/E2E/ActionHook/) | The onAction hook of an agent.act call. |
| [ExploreListener](/api/E2E/ExploreListener/) | Receives every change of the exploration, in order. |
