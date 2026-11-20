// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using static E2E.Decision.Tests.Decide.DecideTestHelpers;

namespace E2E.Decision.Tests.Decide;

/// <summary>The AI SDK's <c>decide.test.ts</c>, the <c>it</c>s outside any <c>describe</c> that report the lifecycle events.</summary>
public sealed class DecideTests
{
    [Fact]
    public async Task Retries_transient_errors_with_the_configured_retry_limit()
    {
        var (model, calls) = Setup();
        var integration = new RecordingTelemetry();
        model.FailOnce(
            new ApiCallException("Rate limited", 429, new Dictionary<string, string> { ["retry-after-ms"] = "0" })
        );

        var result = await ExperimentalDecide.RunAsync(
            new DecideOptions
            {
                Model = model,
                State = [new DecisionTextPart("text")],
                Questions = QuestionMap,
                MaxRetries = 1,
                Telemetry = new TelemetryOptions { Integrations = [integration] },
            }
        );

        Assert.NotNull(result);
        Assert.Equal(2, calls.Count);
        Assert.Single(integration.ModelCallStarts);
        Assert.Single(integration.ModelCallEnds);
    }

    [Fact]
    public async Task Partial_decides_answers_to_mixed_questions_in_one_call_and_preserves_distributions_and_metadata()
    {
        using var body = System.Text.Json.JsonDocument.Parse("""{"raw":true}""");
        var headers = new Dictionary<string, string> { ["x-request-id"] = "request" };
        var (model, _) = Setup(
            new DecisionModelResult
            {
                Answers = AnswerMap,
                ResponseModelId = "actual-model",
                ResponseHeaders = headers,
                ResponseBody = body.RootElement.Clone(),
            }
        );
        DecideEndEvent? ended = null;

        var result = await ExperimentalDecide.RunAsync(
            new DecideOptions
            {
                Model = model,
                State = [new DecisionTextPart("text")],
                Questions = QuestionMap,
                OnEnd = e =>
                {
                    ended = e;
                    return Task.CompletedTask;
                },
            }
        );

        Assert.Equal("actual-model", result.ResponseModelId);
        Assert.Equal(headers, result.ResponseHeaders);
        Assert.Equal("""{"raw":true}""", result.ResponseBody!.Value.GetRawText());
        Assert.NotNull(ended);
        Assert.Equal(headers, ended.ResponseHeaders);
        Assert.Equal("""{"raw":true}""", ended.ResponseBody!.Value.GetRawText());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("provider-default")]
    [InlineData("none")]
    [InlineData("minimal")]
    [InlineData("low")]
    [InlineData("medium")]
    [InlineData("high")]
    [InlineData("xhigh")]
    [InlineData("max")]
    public async Task Forwards_reasoning_and_reports_it_in_lifecycle_callbacks(string? reasoning)
    {
        var (model, calls) = Setup();
        var starts = new List<DecideStartEvent>();
        var ends = new List<DecideEndEvent>();

        await ExperimentalDecide.RunAsync(
            new DecideOptions
            {
                Model = model,
                State = [new DecisionTextPart("test")],
                Questions = QuestionMap,
                Reasoning = reasoning,
                OnStart = @event =>
                {
                    starts.Add(@event);
                    return Task.CompletedTask;
                },
                OnEnd = @event =>
                {
                    ends.Add(@event);
                    return Task.CompletedTask;
                },
            }
        );

        Assert.Equal(reasoning, Assert.Single(calls).Reasoning);
        Assert.Equal(reasoning, Assert.Single(starts).Reasoning);
        Assert.Equal(reasoning, Assert.Single(ends).Reasoning);
    }

    [Fact]
    public async Task Partial_preserves_structured_questions_for_provider_calls_and_all_lifecycle_events()
    {
        var (model, calls) = Setup();
        var integration = new RecordingTelemetry();
        var starts = new List<DecideStartEvent>();
        var ends = new List<DecideEndEvent>();

        await ExperimentalDecide.RunAsync(
            new DecideOptions
            {
                Model = model,
                State = [new DecisionTextPart("package")],
                Questions = QuestionMap,
                OnStart = @event =>
                {
                    starts.Add(@event);
                    return Task.CompletedTask;
                },
                OnEnd = @event =>
                {
                    ends.Add(@event);
                    return Task.CompletedTask;
                },
                Telemetry = new TelemetryOptions { Integrations = [integration] },
            }
        );

        Assert.Same(QuestionMap, Assert.Single(calls).Questions);
        Assert.Same(QuestionMap, Assert.Single(integration.ModelCallStarts).Questions);
        Assert.Same(QuestionMap, Assert.Single(integration.ModelCallEnds).Questions);
        Assert.Same(QuestionMap, Assert.Single(starts).Questions);
        Assert.Same(QuestionMap, Assert.Single(ends).Questions);
    }

    [Fact]
    public async Task Partial_Downloads_image_URLs_and_sends_their_bytes_to_the_decision_model()
    {
        var (model, calls) = Setup();
        byte[] bytes = [0x89, 0x50, 0x4e, 0x47];
        var fetchCalls = 0;
        Uri? requestedUrl = null;

        await ExperimentalDecide.RunAsync(
            new DecideOptions
            {
                Model = model,
                Questions = QuestionMap,
                ExperimentalDownload = Sdk.CreateDownload.Create(
                    new CreateDownloadOptions
                    {
                        Fetch = (request, _) =>
                        {
                            fetchCalls++;
                            requestedUrl = request.RequestUri;
                            var response = new HttpResponseMessage { Content = new ByteArrayContent(bytes) };
                            response.Content.Headers.ContentType = new("image/png");
                            return Task.FromResult(response);
                        },
                    }
                ),
                State = [new DecisionFileUrlPart("image", new Uri("https://example.com/package.png"))],
            }
        );

        Assert.Equal(1, fetchCalls);
        Assert.Equal(new Uri("https://example.com/package.png"), requestedUrl);
        var file = Assert.IsType<DecisionFilePart>(Assert.Single(Assert.Single(calls).State));
        Assert.Equal("image/png", file.MediaType);
        Assert.Equal(bytes, file.Data);
    }

    // Partial: upstream stubs the global fetch behind createDownload(); the port's default HTTP client cannot be stubbed, so the 404
    // comes through the Fetch option.
    [Fact]
    public async Task Partial_Stops_before_provider_IO_when_an_image_download_fails()
    {
        var (model, calls) = Setup();

        var error = await Assert.ThrowsAsync<DownloadException>(() =>
            ExperimentalDecide.RunAsync(
                new DecideOptions
                {
                    Model = model,
                    Questions = QuestionMap,
                    ExperimentalDownload = Sdk.CreateDownload.Create(
                        new CreateDownloadOptions
                        {
                            Fetch = (_, _) =>
                                Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)),
                        }
                    ),
                    State = [new DecisionFileUrlPart("image/png", new Uri("https://example.com/missing.png"))],
                }
            )
        );

        Assert.Equal(404, error.StatusCode);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task Partial_Rejects_remote_file_state_without_a_download_hook()
    {
        var (model, calls) = Setup();

        var error = await Assert.ThrowsAsync<DownloadException>(() =>
            ExperimentalDecide.RunAsync(
                new DecideOptions
                {
                    Model = model,
                    Questions = QuestionMap,
                    State = [new DecisionFileUrlPart("image/png", new Uri("https://attacker.example/package.png"))],
                }
            )
        );

        Assert.Contains("experimental_download", error.Message);
        Assert.Empty(calls);
    }
}
