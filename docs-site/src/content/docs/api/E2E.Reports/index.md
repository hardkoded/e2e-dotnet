---
title: "E2E.Reports"
description: "Public types in the `E2E.Reports` namespace."
editUrl: false
sidebar:
  label: "E2E.Reports"
---

[API reference](/api/)

Public types in the `E2E.Reports` namespace.

## Classes

| Type | Summary |
| --- | --- |
| [AppLogRecord](/api/E2E.Reports/AppLogRecord/) | One line the app logged (upstream's AppLogRecord). |
| [AppSetupStep](/api/E2E.Reports/AppSetupStep/) | Upstream's { kind: 'app' } step. Url is the one its readiness probe polls, with a free port already assigned and without userinfo or query; null for a command that is ready when it exits. |
| [ArtifactProducer](/api/E2E.Reports/ArtifactProducer/) | Who made an artifact (upstream's ArtifactRecord['producer']). |
| [ArtifactRecord](/api/E2E.Reports/ArtifactRecord/) | A file or link an attempt kept (upstream's ArtifactRecord, run/records.ts). |
| [CacheRecordedBy](/api/E2E.Reports/CacheRecordedBy/) | The model that recorded a cache entry. |
| [CollectSetupStep](/api/E2E.Reports/CollectSetupStep/) | Upstream's { kind: 'collect' } step. |
| [FailureEvidence](/api/E2E.Reports/FailureEvidence/) | What the runner saw when a failure landed (upstream's FailureEvidence). |
| [FilterSetupStep](/api/E2E.Reports/FilterSetupStep/) | Upstream's { kind: 'filter' } step. |
| [FinishedRun](/api/E2E.Reports/FinishedRun/) | What a reporter receives once the run is over and its report is on disk (upstream's FinishedRun, types.ts). |
| [HookScope](/api/E2E.Reports/HookScope/) | The tests a hook failure leaves unverified (upstream's HookScope). |
| [MarkdownReport](/api/E2E.Reports/MarkdownReport/) | The Report as one markdown page, laid out for a pull request: a headline with the counts and what the run spent, the run-level errors, one block per test that failed with the step it went wrong at and the agent's last word, the flaky tests folded away with the same block each, what the run's test filters left out, folded away with each reason, and every test folded away under that as one table, a row per file with its counts and time above the rows of its tests. A green run is the headline, the folds, and the footer: nothing to read unless the reader opens it. An e2e explore run renders its record instead: the goal, every finding with its evidence, and the assessment. Upstream's report/markdown.ts. Everything a test or the agent wrote (titles, labels, error messages, findings) is untrusted text on its way into markdown a renderer trusts, so it is escaped and clipped, and the whole body never outgrows what GitHub accepts. A reporter that posts the page elsewhere (such as the GitHub comment) renders FinishedRun.Report with it; upstream's markdownReporter (the summary.md writer) is not here. |
| [MarkdownReportOptions](/api/E2E.Reports/MarkdownReportOptions/) | What a markdown page links to and how it names itself (upstream's MarkdownReportOptions). |
| [NoticeEvent](/api/E2E.Reports/NoticeEvent/) | Upstream's notice fact: one line of run-level progress outside any test. |
| [PlanEvent](/api/E2E.Reports/PlanEvent/) | Upstream's plan fact: what the run will execute, emitted once collection, selection, and every target's setup are done. From here the run executes, so the list reporter's clock starts with it. Files is upstream's inline { file, target, tests } list, one tuple per test file on one target. |
| [PrepareSetupStep](/api/E2E.Reports/PrepareSetupStep/) | Upstream's { kind: 'prepare' } step. |
| [Report](/api/E2E.Reports/Report/) | The report-1 document, the shape Report() returns and report.json holds (upstream's Report, an alias of Report1Document, report/build.ts). A report is a record of what happened. Changing it changes nothing, and a caller that wants the document as JSON reads report.json. The port has no serial groups and no carried block, so the document has neither. |
| [ReportAttempt](/api/E2E.Reports/ReportAttempt/) | One attempt of a test (upstream's ReportAttempt). |
| [ReportChanges](/api/E2E.Reports/ReportChanges/) | The change a run's test filters judged, by counts only, or why no filter ran (upstream's ReportChanges): Base and Files once git measured the change, Skipped alone when no filter ran before git was asked. |
| [ReportEngine](/api/E2E.Reports/ReportEngine/) | The engine a target runs on (upstream's ReportTarget['engine']). |
| [ReportEnvironment](/api/E2E.Reports/ReportEnvironment/) | What a run ran on (upstream's run.environment). |
| [Reporter](/api/E2E.Reports/Reporter/) | A reporter, listed in the reporters of e2e.config.json (upstream's Reporter, types.ts). OnRunFinishedAsync runs once, when the test run is over and report.json is final; the run is awaited for one minute, and the rows it returns print under the run's output. The token is cancelled when that budget runs out or the run is forced to stop, so a reporter hands it to its requests and leaves nothing running behind. A reporter can never change the run's outcome: a failure or a timeout is one line on stderr. The test run ends when the run fixture (E2ERunFixture of E2E.NUnit or E2E.XUnit.V3) is torn down, so a run without it calls no reporter. A reporter has a public constructor with no parameters and no side effects; one that takes options also implements IConfigurableReporter. The run fixture builds the reporters when the run starts, so OnEvent(RunEvent) sees the run's events as they happen, and calls the same instances when it ends. |
| [ReportError](/api/E2E.Reports/ReportError/) | An error as a report tells it: the serialized error without its stack (upstream's ReportError). |
| [ReporterSummaryRow](/api/E2E.Reports/ReporterSummaryRow/) | One row a reporter hands back for the terminal summary: a label and its text, a URL or a path (upstream's ReporterSummary element). |
| [ReportFilter](/api/E2E.Reports/ReportFilter/) | One test filter the run applied, in order (upstream's ReportFilter). |
| [ReportLimits](/api/E2E.Reports/ReportLimits/) | The limits the run ran under (upstream's ReportLimits). |
| [ReportProject](/api/E2E.Reports/ReportProject/) | The project a run belongs to (upstream's run.project). |
| [ReportResult](/api/E2E.Reports/ReportResult/) | One test over all its attempts (upstream's ReportResult). |
| [ReportRun](/api/E2E.Reports/ReportRun/) | The run of a report: its id, the targets it ran on, the results, and the counts (upstream's Report1Document['run']). |
| [ReportRunner](/api/E2E.Reports/ReportRunner/) | The runner that made a report (upstream's run.runner). |
| [ReportSource](/api/E2E.Reports/ReportSource/) | A line in a file of the project (upstream's ReportSource). |
| [ReportStep](/api/E2E.Reports/ReportStep/) | One call of the test (upstream's ReportStep). |
| [ReportSummary](/api/E2E.Reports/ReportSummary/) | The run's counts by result (upstream's ReportSummary). Passed, Failed (timed out included), Interrupted, Flaky, and Skipped count selected results only and add up to Selected. |
| [ReportTarget](/api/E2E.Reports/ReportTarget/) | One target of the run (upstream's ReportTarget). |
| [ReportUsage](/api/E2E.Reports/ReportUsage/) | What the run used, against ReportLimits (upstream's ReportUsage). |
| [RunErrorEvent](/api/E2E.Reports/RunErrorEvent/) | Upstream's run-error fact: a run-level error, which aborts before, between, or after test execution. |
| [RunEvent](/api/E2E.Reports/RunEvent/) | Structured run events: the run's single event spine (upstream's run/events.ts), as a Reporter sees it through OnEvent(RunEvent). The list reporter consumes the same stream (ListReporter.Handle), so no two consumers can drift. The port's test run fixtures and sessions emit them: the run's start, plan, notices, setup steps, started tests, step progress, finished tests, errors, interrupts, and end. Not ported: output (the test framework captures stdout), serial-group (the port has no serial groups), and explore, which feeds the live window the port has no use for yet. A new event type may be added, so a reporter handles the ones it knows and ignores the rest. |
| [RunEventFact](/api/E2E.Reports/RunEventFact/) | One fact about the run; the emitter wraps it in the RunEvent envelope. One record per upstream event type. |
| [RunEventResult](/api/E2E.Reports/RunEventResult/) | Upstream's RunEventResult: a test's result with its target's identity. The attempts are the report's. |
| [RunFinishedEvent](/api/E2E.Reports/RunFinishedEvent/) | Upstream's run-finished fact. |
| [RunInterruptedEvent](/api/E2E.Reports/RunInterruptedEvent/) | Upstream's run-interrupted fact. |
| [RunStartedEvent](/api/E2E.Reports/RunStartedEvent/) | Upstream's run-started fact. |
| [RunStoppedEvent](/api/E2E.Reports/RunStoppedEvent/) | Upstream's run-stopped fact: the run reached its failure limit. |
| [ServiceSetupStep](/api/E2E.Reports/ServiceSetupStep/) | Upstream's { kind: 'service' } step. |
| [SetupEvent](/api/E2E.Reports/SetupEvent/) | Upstream's setup fact: the work before the tests start. Upstream has two members of one type, started and finished; here DurationMs and Outcome are set only when State is Finished. |
| [SetupStep](/api/E2E.Reports/SetupStep/) | What one setup step is about (upstream's SetupStep, run/events.ts). collect loads the test files; filter is one test filter, by its name; prepare is one target's engine provisioning; service and app are the processes the run owns, labelled the way the runner names them (service "postgres", target "web" command). |
| [SkipInfo](/api/E2E.Reports/SkipInfo/) | Why a test or an attempt skipped (upstream's SkipInfo). |
| [StepCacheRecord](/api/E2E.Reports/StepCacheRecord/) | How an agent step used the cache (upstream's StepCacheRecord). |
| [StepEvent](/api/E2E.Reports/StepEvent/) | One thing a step did (upstream's StepEvent). |
| [StepMetrics](/api/E2E.Reports/StepMetrics/) | What an agent step used (upstream's StepMetrics). |
| [StepModelInfo](/api/E2E.Reports/StepModelInfo/) | The model an agent step called and what it cost (upstream's StepModelInfo). |
| [StepProgress](/api/E2E.Reports/StepProgress/) | Live progress notification for one step, streamed as the step runs (upstream's StepProgress, run/steps.ts). Derived from the same records the report persists; the report stays the canonical record. Which members are set depends on Phase. It lives in E2E.Reports with the report types it uses, because the port's own records for the same parts keep upstream's names in E2E. |
| [StepProgressEvent](/api/E2E.Reports/StepProgressEvent/) | Upstream's step fact: one moment of a running test's step, routed to its pair by test id, agent, target, and repeat. .NET-only name: StepEvent is the report's model-turn record. |
| [StepProgressIdentity](/api/E2E.Reports/StepProgressIdentity/) | Whose progress a notification is: IDs join live progress to the report. Step indexes are local to each test (upstream's StepProgressIdentity). |
| [StepScreen](/api/E2E.Reports/StepScreen/) | How the screen a step saw differs from the one before (upstream's StepScreen). |
| [StepTurn](/api/E2E.Reports/StepTurn/) | One turn of an agent step (upstream's StepTurn). |
| [StepViewport](/api/E2E.Reports/StepViewport/) | The viewport a step ran in. |
| [TestFinishedEvent](/api/E2E.Reports/TestFinishedEvent/) | Upstream's test-finished fact. |
| [TestIdentity](/api/E2E.Reports/TestIdentity/) | Who a test is (upstream's TestIdentity, collect/collect.ts). |
| [TestStartedEvent](/api/E2E.Reports/TestStartedEvent/) | Upstream's test-started fact: a test began executing. A test that runs as several agents, on several targets, or under repeat-each starts once per pair. SerialId is the test's serial group; the port has no serial groups, so the list reporter does not read it. |
| [VcsInfo](/api/E2E.Reports/VcsInfo/) | Where the code under test came from (upstream's VcsInfo, internal/vcs.ts). |
| [ViewportSize](/api/E2E.Reports/ViewportSize/) | The size of a viewport in pixels (upstream's inline FailureEvidence['viewport']). |

## Interfaces

| Type | Summary |
| --- | --- |
| [IConfigurableReporter](/api/E2E.Reports/IConfigurableReporter/) | A Reporter that takes the options of its e2e.config.json entry. .NET-only: upstream's config lists reporter objects built in code (github(options)), and a JSON entry holds no code, so the options arrive as JSON. |

## Enums

| Type | Summary |
| --- | --- |
| [AppLogLevel](/api/E2E.Reports/AppLogLevel/) | How serious an app log line is. |
| [AppLogSource](/api/E2E.Reports/AppLogSource/) | Where an app log line came from. |
| [ArtifactCapability](/api/E2E.Reports/ArtifactCapability/) | What an engine can capture. |
| [ArtifactKind](/api/E2E.Reports/ArtifactKind/) | What kind of file an artifact is. |
| [ArtifactProducerKind](/api/E2E.Reports/ArtifactProducerKind/) | Whether a step or the attempt made an artifact. |
| [ArtifactRedaction](/api/E2E.Reports/ArtifactRedaction/) | Whether every secret was proven out of an artifact. |
| [AttemptCleanup](/api/E2E.Reports/AttemptCleanup/) | How an attempt's teardown ended. |
| [AttemptStatus](/api/E2E.Reports/AttemptStatus/) | How an attempt ended. |
| [CacheDerived](/api/E2E.Reports/CacheDerived/) | What a replayed action found its target by. |
| [CacheMissReason](/api/E2E.Reports/CacheMissReason/) | Why a cached step did not replay. |
| [CacheMode](/api/E2E.Reports/CacheMode/) | How an agent step used the cache. |
| [CacheNotRecorded](/api/E2E.Reports/CacheNotRecorded/) | Why a recording was not kept. |
| [CacheWrite](/api/E2E.Reports/CacheWrite/) | What happened to the cache entry after the step. |
| [ErrorCategory](/api/E2E.Reports/ErrorCategory/) | Whose fault an error is. |
| [ErrorPhase](/api/E2E.Reports/ErrorPhase/) | The part of a run an error came from. |
| [FilterOutcome](/api/E2E.Reports/FilterOutcome/) | Why a filter left nothing out without answering (upstream's FilterOutcome). |
| [InterruptMode](/api/E2E.Reports/InterruptMode/) | How an interrupt landed: the running test is interrupted and its teardown runs, or (a second interrupt) every worker is torn down at once. |
| [PolicyDecision](/api/E2E.Reports/PolicyDecision/) | Whether a policy allowed a proposal. |
| [ResultKind](/api/E2E.Reports/ResultKind/) | Whether a result is a test or a setup. |
| [ResultStatus](/api/E2E.Reports/ResultStatus/) | How a test ended, over all its attempts. A pass after an earlier attempt is Flaky, and a retry that was interrupted keeps the Failed or TimedOut verdict of the attempt before it. |
| [SetupOutcome](/api/E2E.Reports/SetupOutcome/) | How a setup step ended other than plainly done (upstream's SetupOutcome): Reused when a service or app command attached to a process already serving its URL instead of spawning; Failed and TimedOut for a test filter (upstream's FilterOutcome). |
| [SetupState](/api/E2E.Reports/SetupState/) | Whether a SetupEvent opens its step or closes it. |
| [SkipCause](/api/E2E.Reports/SkipCause/) | The cause of a skip. |
| [StepActivity](/api/E2E.Reports/StepActivity/) | The work a running agent step can be waiting on (upstream's StepActivity). |
| [StepEventKind](/api/E2E.Reports/StepEventKind/) | What kind of thing a step event records. |
| [StepEventStatus](/api/E2E.Reports/StepEventStatus/) | How a step event ended. |
| [StepKind](/api/E2E.Reports/StepKind/) | What kind of call a step is. |
| [StepPhase](/api/E2E.Reports/StepPhase/) | The hook a step ran in; a step of the test body has none. |
| [StepProgressPhase](/api/E2E.Reports/StepProgressPhase/) | What a progress notification says (upstream's StepProgress['phase']). |
| [StepStatus](/api/E2E.Reports/StepStatus/) | How a step ended. |
| [TargetEnvironment](/api/E2E.Reports/TargetEnvironment/) | What the app a target tests is. |
| [TokenAccounting](/api/E2E.Reports/TokenAccounting/) | How a model call counted its tokens. |
| [VisionDegradation](/api/E2E.Reports/VisionDegradation/) | Why a step's pixels were not trusted. |
