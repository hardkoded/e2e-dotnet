# E2E for .NET docs

The documentation site is [Astro Starlight](https://starlight.astro.build/). The published site is [https://hardkoded.github.io/e2e-dotnet/](https://hardkoded.github.io/e2e-dotnet/).

## Preview locally

From this directory:

```bash
npm install
npm run dev
```

Open http://localhost:4321/e2e-dotnet/ . The path includes `/e2e-dotnet/` because that is the GitHub Pages project-site base.

Search is built with the production output:

```bash
npm run build
npm run preview
```

`npm run dev` and `npm run build` copy `README.md`, `COMPATIBILITY.md`, and `CONTRIBUTING.md` into the site before Astro starts. Get started and Samples live in `src/content/docs/`.

## Regenerate the API reference

The API pages are generated. They are not edited by hand.

```bash
npm run api
```

That needs the .NET 10 SDK. It restores the local DocFX tool (`dotnet-tools.json`, DocFX 2.80.1), runs `docfx metadata` on `src/E2E`, `src/E2E.NUnit`, and `src/E2E.XUnit.V3`, and writes Markdown into `src/content/docs/api/`. Commit the generated pages and `src/generated/api-sidebar.json`.

`npm run api -- --skip-metadata` rebuilds the Markdown from `docs-site/.api-yaml` without running DocFX again.

## GitHub Pages

`.github/workflows/docs.yml` builds this site on every pull request and on every push to `main`. It deploys only from a push to `main`. Merging the docs change is what publishes it. The workflow does not change the repository's Pages source setting.

Old DocFX `.html` URLs redirect to the new paths. Member anchors from DocFX (`#E2E_App_OpenAsync_...`) are kept on the generated pages. A redirect preserves the hash when JavaScript runs.
