# E2E for .NET docs

The documentation site is [Astro Starlight](https://starlight.astro.build/). The published site is [https://www.hardkoded.com/e2e-dotnet/](https://www.hardkoded.com/e2e-dotnet/).

## Preview locally

From this directory:

```bash
npm install
npm run dev
```

Open http://localhost:4321/e2e-dotnet/ . The path includes `/e2e-dotnet/` because that is the hardkoded.com path base (`siteBase` in `site.mjs`). Local preview keeps that prefix so links match https://www.hardkoded.com/e2e-dotnet/. GitHub Pages is retired, so this is the production path base, not a Pages project-site base.

Search is built with the production output:

```bash
npm run build
npm run preview
```

`npm run dev` and `npm run build` copy `README.md`, `COMPATIBILITY.md`, and `CONTRIBUTING.md` into the site before Astro starts. Get started and Samples live in `src/content/docs/`.

The site root is a marketing page (`src/pages/index.astro`), not the Starlight home. Documentation starts at `/docs/`. Get started, the guide, samples, and the API stay on their existing paths.

## Regenerate the API reference

The API pages are generated. They are not edited by hand.

```bash
npm run api
```

That needs the .NET 10 SDK. It restores the local DocFX tool (`dotnet-tools.json`, DocFX 2.80.1), runs `docfx metadata` on `src/E2E`, `src/E2E.NUnit`, and `src/E2E.XUnit.V3`, and writes Markdown into `src/content/docs/api/`. Commit the generated pages and `src/generated/api-sidebar.json`.

`npm run api -- --skip-metadata` rebuilds the Markdown from `docs-site/.api-yaml` without running DocFX again.

## GitHub Pages

GitHub Pages is retired. `.github/workflows/docs.yml` only builds and checks this site on pull requests and on pushes to `main`. It uploads the build as a CI artifact and does not deploy. Vercel is the host. The Vercel Git integration publishes https://www.hardkoded.com/e2e-dotnet/ on push to `main`, through a multi-zone rewrite from the hardkoded blog project.

Old DocFX `.html` URLs redirect to the new paths. Member anchors from DocFX (`#E2E_App_OpenAsync_...`) are kept on the generated pages. A redirect preserves the hash when JavaScript runs.
