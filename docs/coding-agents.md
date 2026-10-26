---
title: Coding agents
---

# Coding agents

A coding agent such as Claude Code, Codex, or Cursor can write, run, and debug e2e tests. It learns e2e from the skill, uses the app through the `e2e mcp` server, and reads the output of a failed `dotnet test`. This page ports upstream's `docs/coding-agents.mdx`. The port has no `e2e init`, so each step below is a separate command.

## Install the skill

```bash
npx skills add hardkoded/e2e-dotnet --skill e2e-dotnet
```

`e2e guide [topic]` prints the same skill from the installed `E2E.Cli` tool. For an agent that needs a pointer, add this line to your project instructions:

```md
End-to-end tests use e2e; read .agents/skills/e2e-dotnet/SKILL.md before writing or running one.
```

## Let the agent use the app

`e2e mcp` gives the agent a live session on your app. It checks its locators against the screen before it writes them. Register it once:

```bash
claude mcp add e2e -- e2e mcp
```

See the [README](../README.md#coding-agents-mcp) and [COMPATIBILITY.md](../COMPATIBILITY.md#mcp-server).

## Generate a verification skill for your app

The e2e skill teaches the runner, not your app. The `create-verification-skill` skill writes a project-local `.agents/skills/verify-<app>/` skill that does, and tests it one time before it hands over.

```bash
npx skills add hardkoded/e2e-dotnet --skill create-verification-skill
```

Then ask the agent for `/create-verification-skill`.

## Migrate an existing suite

The `migrate-to-e2e` skill moves a Playwright .NET, Selenium .NET, Appium, Cypress, Detox, or Maestro suite to e2e one slice per session. The old suite keeps running beside the new tests, so you can stop after any session.

```bash
npx skills add hardkoded/e2e-dotnet --skill migrate-to-e2e
```

Then ask the agent for `/migrate-to-e2e`. It ports the slice you pick as NUnit or xUnit v3 tests and runs each ported test three times on every target. A `MIGRATION.md` file records progress for the next session. Optionally, the agent plants a temporary bug in the app, such as a wrong checkout total, and checks whether both suites fail on it. The agent deletes an old test only when you confirm.

Upstream reads a migration guide for the runner first (`docs/migrate/*.mdx`). The port has no such guides, so the skill reads the e2e skill's topics instead. See [COMPATIBILITY.md](../COMPATIBILITY.md#verification-skill).
