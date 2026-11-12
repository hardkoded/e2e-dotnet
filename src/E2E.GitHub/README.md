# E2E.GitHub

A GitHub pull request comment reporter for the community .NET port of the [e2e](https://github.com/tester-army/e2e) agentic testing framework. It ports `@e2e-dev/github`. Not an official TesterArmy product.

```
dotnet add package E2E.GitHub
```

List the reporter in `e2e.config.json`:

```json
{ "reporters": ["E2E.GitHub.GitHubReporter, E2E.GitHub"] }
```

On GitHub Actions, when the run ends, `GitHubReporter` writes the run to the job summary and posts one comment on the pull request, then edits that comment on the next run. Pass the token to the step (`GITHUB_TOKEN: ${{ github.token }}`) and give the job `permissions: pull-requests: write`. Outside GitHub Actions it does nothing.

To tell matrix jobs apart, give the entry a key, `{ "type": "E2E.GitHub.GitHubReporter, E2E.GitHub", "options": { "key": "chromium" } }`, or set `E2E_GITHUB_KEY` in the job.

See the [repository](https://github.com/hardkoded/e2e-dotnet) and its `COMPATIBILITY.md` for what differs from upstream.
