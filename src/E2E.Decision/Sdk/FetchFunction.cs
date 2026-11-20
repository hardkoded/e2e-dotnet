// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>
/// The AI SDK's <c>FetchFunction</c> (<c>@ai-sdk/provider-utils</c>): the transport a download goes through. It must not follow redirects
/// itself, because every hop is validated before it is requested.
/// </summary>
public delegate Task<HttpResponseMessage> FetchFunction(
    HttpRequestMessage request,
    CancellationToken cancellationToken
);
