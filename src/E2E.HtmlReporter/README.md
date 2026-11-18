# E2E.HtmlReporter

The HTML reporter for the community .NET port of the [e2e](https://github.com/tester-army/e2e) agentic testing framework. It ports `@e2e-dev/html-reporter`. Not an official TesterArmy product.

```
dotnet add package E2E.HtmlReporter
```

List it in `e2e.config.json`:

```json
{ "reporters": ["list", "E2E.HtmlReporter.HtmlReporter, E2E.HtmlReporter"] }
```

At the end of a run it writes `.e2e/report.html`: one page that opens from disk with no server. It lists the tests by file, with status tabs and a search box. Each test's page shows its attempts, error, steps, screenshots, and video, and each step links to its moment in the video.

By default the page links screenshots and videos from `.e2e/results/`, so keep the output directory together when you upload it from CI. The `media` option changes that: `"embed"` puts them in the page, so the one file is the whole report, and `"none"` leaves them out for a small, text-only page. `outputFile` writes the page somewhere else, as a path from the project root.

```json
{ "type": "E2E.HtmlReporter.HtmlReporter, E2E.HtmlReporter", "options": { "media": "embed", "outputFile": "reports/e2e.html" } }
```

## Development

The page is upstream's React app in `app/`, built by Vite into one HTML file with its scripts, styles, and fonts inlined. `dotnet build` runs `npm ci` and `npm run build` (Node 22 or later) and embeds the result in the assembly. `npm test` in this directory runs the page's unit tests (`tests/unit/data.test.ts`). `app/report.ts` is generated from `schema/report-v1.schema.json` by `node scripts/generate-report-types.mjs`.

The page uses Geist and JetBrains Mono, both under the SIL Open Font License 1.1.
