---
title: "E2E.OAuth"
description: "Public types in the `E2E.OAuth` namespace."
editUrl: false
sidebar:
  label: "E2E.OAuth"
---

[API reference](/api/)

Public types in the `E2E.OAuth` namespace.

## Classes

| Type | Summary |
| --- | --- |
| [CodexProvider](/api/E2E.OAuth/CodexProvider/) | ChatGPT Plus/Pro through the Codex OAuth client: PKCE authorization code against auth.openai.com with a local callback on port 1455, or OpenAI's user-code flow for a machine without a browser. Requests go to the Codex backend, which speaks the Responses API, requires store: false, and answers only as a stream. |
| [CopilotModel](/api/E2E.OAuth/CopilotModel/) | A GitHub Copilot subscription model: chat completions, or the Responses API when the plan serves the model only there. |
| [CopilotProvider](/api/E2E.OAuth/CopilotProvider/) | GitHub Copilot. The Copilot API accepts a GitHub sign-in as the bearer, so the login is either the token the GitHub CLI already holds or GitHub's device flow for an OAuth App you register. GitHub user tokens do not expire; a 401 means signing in again. |
| [CredentialStores](/api/E2E.OAuth/CredentialStores/) | The stores the subscription models and the CLI use. |
| [DelegatingModel](/api/E2E.OAuth/DelegatingModel/) | A model that settles its protocol on the first call and delegates to the client for it. One lookup serves every concurrent call, under its own timeout, so a caller that gives up never fails the others. Only a lookup that succeeded is remembered: when it could not be read, that call goes over the fallback and the next call asks again. |
| [EnvironmentCredentialStore](/api/E2E.OAuth/EnvironmentCredentialStore/) | Credentials from EnvironmentVariable: readable everywhere, changed nowhere. |
| [FileCredentialStore](/api/E2E.OAuth/FileCredentialStore/) | One JSON file keyed by provider id, mode 0600. Writes hold an advisory lock and replace the file atomically, so two runs refreshing at once neither tear the file nor lose each other's entries. Entries this store does not understand are kept as they are. |
| [OAuthAuthInfo](/api/E2E.OAuth/OAuthAuthInfo/) | Where the user is sent and, for device flows, the code to enter there. |
| [OAuthCredentials](/api/E2E.OAuth/OAuthCredentials/) | What a login returns and a refresh renews. Stored as upstream stores it: access, refresh, expires (epoch milliseconds, 0 when the token does not expire), plus each provider's own fields. |
| [OAuthException](/api/E2E.OAuth/OAuthException/) | Why a subscription login or a request made with it failed. Code is one of the constants. |
| [OAuthHandler](/api/E2E.OAuth/OAuthHandler/) | The handler a subscription model's HttpClient sends through. Per request it reads the stored credentials, refreshes them ahead of expiry, replaces any API key header with the bearer token, and lets the provider adjust the request. On a 401 it refreshes once and retries, or, with nothing to refresh, reports that the user signs in again. One refresh serves every model and every concurrent call over the same store, because vendors that rotate refresh tokens reject the second concurrent refresh. |
| [OAuthLoginCallbacks](/api/E2E.OAuth/OAuthLoginCallbacks/) | How a login flow talks to the terminal, or whatever hosts it. |
| [OAuthLoginOptions](/api/E2E.OAuth/OAuthLoginOptions/) | The e2e login flags; each provider reads the ones meant for it. |
| [OAuthProviders](/api/E2E.OAuth/OAuthProviders/) | The built-in subscription providers by id, and what e2e login, logout, and models do with them. |
| [OpenCodeConsoleModel](/api/E2E.OAuth/OpenCodeConsoleModel/) | An OpenCode Console model. The first call reads the workspace config and talks to the model over its own protocol: chat completions, OpenAI Responses, Anthropic Messages, or Gemini. Each instance names one session, so its calls stay on one upstream and its cache. |
| [OpenCodeConsoleProvider](/api/E2E.OAuth/OpenCodeConsoleProvider/) | OpenCode Zen and Go through an OpenCode Console sign-in. Console speaks RFC 8628 device authorization; approving the code binds the login to one workspace, and its token is a bearer for both the Console API and inference. Refresh tokens rotate. The workspace config says which models are served and over which protocol: Zen is its opencode provider, Go opencode-go. Go ids take a go/ prefix here. |
| [SubscriptionModel](/api/E2E.OAuth/SubscriptionModel/) | One model a subscription serves: the id a config passes, and how the vendor describes it. |
| [Subscriptions](/api/E2E.OAuth/Subscriptions/) | Subscription logins as agent models, upstream's chatgpt(), copilot(), grok(), and opencodeConsole(). Each reads the login e2e login stored (or E2EOAUTHCREDENTIALS), refreshes it as needed, and touches nothing until the first call. Use API keys in CI. |
| [XaiProvider](/api/E2E.OAuth/XaiProvider/) | SuperGrok and X Premium+ through SpaceXAI's OAuth server: RFC 8628 device authorization for the Grok CLI's public client. The token is a plain bearer against the ordinary SpaceXAI API. Refresh tokens rotate. |

## Interfaces

| Type | Summary |
| --- | --- |
| [ICredentialStore](/api/E2E.OAuth/ICredentialStore/) | Where credentials live between runs, keyed by provider id. |
| [IOAuthProvider](/api/E2E.OAuth/IOAuthProvider/) | The contract between a subscription login and the model clients: a provider signs the user in, refreshes what the login returned, and adjusts one API request the vendor's way. The credentials file, the handler that swaps the token in, and the models are shared. |
