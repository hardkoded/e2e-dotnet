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
| [ArtifactProducer](/api/E2E.Reports/ArtifactProducer/) | Who made an artifact (upstream's ArtifactRecord['producer']). |
| [ArtifactRecord](/api/E2E.Reports/ArtifactRecord/) | A file or link an attempt kept (upstream's ArtifactRecord, run/records.ts). |
| [CacheRecordedBy](/api/E2E.Reports/CacheRecordedBy/) | The model that recorded a cache entry. |
| [E2EReporterAttribute](/api/E2E.Reports/E2EReporterAttribute/) | Registers a Reporter for the test assembly: [assembly: E2EReporter(typeof(MyReporter))]. .NET-only: upstream registers reporter objects in the reporters field of its config module, and the port's JSON config holds no code. |
| [FailureEvidence](/api/E2E.Reports/FailureEvidence/) | What the runner saw when a failure landed (upstream's FailureEvidence). |
| [FinishedRun](/api/E2E.Reports/FinishedRun/) | What a reporter receives once the run is over and its report is on disk (upstream's FinishedRun, types.ts). |
| [HookScope](/api/E2E.Reports/HookScope/) | The tests a hook failure leaves unverified (upstream's HookScope). |
| [Report](/api/E2E.Reports/Report/) | The report-1 document, the shape Report() returns and report.json holds (upstream's Report, an alias of Report1Document, report/build.ts). A report is a record of what happened. Changing it changes nothing, and a caller that wants the document as JSON reads report.json. The port has no serial groups and no carried block, so the document has neither. |
| [ReportAttempt](/api/E2E.Reports/ReportAttempt/) | One attempt of a test (upstream's ReportAttempt). |
| [ReportChanges](/api/E2E.Reports/ReportChanges/) | The change a run's test filters judged, by counts only, or why no filter ran (upstream's ReportChanges): Base and Files once git measured the change, Skipped alone when no filter ran before git was asked. |
| [ReportEngine](/api/E2E.Reports/ReportEngine/) | The engine a target runs on (upstream's ReportTarget['engine']). |
| [ReportEnvironment](/api/E2E.Reports/ReportEnvironment/) | What a run ran on (upstream's run.environment). |
| [ReportError](/api/E2E.Reports/ReportError/) | An error as a report tells it: the serialized error without its stack (upstream's ReportError). |
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
| [SkipInfo](/api/E2E.Reports/SkipInfo/) | Why a test or an attempt skipped (upstream's SkipInfo). |
| [StepCacheRecord](/api/E2E.Reports/StepCacheRecord/) | How an agent step used the cache (upstream's StepCacheRecord). |
| [StepEvent](/api/E2E.Reports/StepEvent/) | One thing a step did (upstream's StepEvent). |
| [StepMetrics](/api/E2E.Reports/StepMetrics/) | What an agent step used (upstream's StepMetrics). |
| [StepModelInfo](/api/E2E.Reports/StepModelInfo/) | The model an agent step called and what it cost (upstream's StepModelInfo). |
| [StepProgress](/api/E2E.Reports/StepProgress/) | Live progress notification for one step, streamed as the step runs (upstream's StepProgress, run/steps.ts). Derived from the same records the report persists; the report stays the canonical record. Which members are set depends on Phase. It lives in E2E.Reports with the report types it uses, because the port's own records for the same parts keep upstream's names in E2E. |
| [StepProgressIdentity](/api/E2E.Reports/StepProgressIdentity/) | Whose progress a notification is: IDs join live progress to the report. Step indexes are local to each test (upstream's StepProgressIdentity). |
| [StepScreen](/api/E2E.Reports/StepScreen/) | How the screen a step saw differs from the one before (upstream's StepScreen). |
| [StepTurn](/api/E2E.Reports/StepTurn/) | One turn of an agent step (upstream's StepTurn). |
| [StepViewport](/api/E2E.Reports/StepViewport/) | The viewport a step ran in. |
| [VcsInfo](/api/E2E.Reports/VcsInfo/) | Where the code under test came from (upstream's VcsInfo, internal/vcs.ts). |
| [ViewportSize](/api/E2E.Reports/ViewportSize/) | The size of a viewport in pixels (upstream's inline FailureEvidence['viewport']). |

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
| [PolicyDecision](/api/E2E.Reports/PolicyDecision/) | Whether a policy allowed a proposal. |
| [ResultKind](/api/E2E.Reports/ResultKind/) | Whether a result is a test or a setup. |
| [ResultStatus](/api/E2E.Reports/ResultStatus/) | How a test ended, over all its attempts. A pass after an earlier attempt is Flaky, and a retry that was interrupted keeps the Failed or TimedOut verdict of the attempt before it. |
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
