# E2E.GitHub

A GitHub pull request comment reporter for the community .NET port of the [e2e](https://github.com/tester-army/e2e) agentic testing framework. It ports `@e2e-dev/github`. Not an official TesterArmy product.

```
dotnet add package E2E.GitHub
```

Register the reporter once, in the test assembly:

```csharp
using E2E.GitHub;
using E2E.Reports;

[assembly: E2EReporter(typeof(GitHubReporter))]
```

On GitHub Actions, when the run ends, `GitHubReporter` writes the run to the job summary and posts one comment on the pull request, then edits that comment on the next run. Pass the token to the step (`GITHUB_TOKEN: ${{ github.token }}`) and give the job `permissions: pull-requests: write`. Outside GitHub Actions it does nothing.

To tell matrix jobs apart, set `E2E_GITHUB_KEY` in the job, or derive from `GitHubReporter` and pass `new GitHubOptions { Key = ... }` to its constructor.

See the [repository](https://github.com/hardkoded/e2e-dotnet) and its `COMPATIBILITY.md` for what differs from upstream.
