// @ts-check
import { defineConfig } from "astro/config";
import starlight from "@astrojs/starlight";
import { satteri } from "@astrojs/markdown-satteri";
import { baseLinksPlugin } from "./src/base-links.mjs";
import { siteBase, siteOrigin } from "./site.mjs";
import apiSidebar from "./src/generated/api-sidebar.json";

// https://astro.build/config
export default defineConfig({
    site: siteOrigin,
    base: siteBase,
    trailingSlash: "always",
    markdown: {
        processor: satteri({
            mdastPlugins: [baseLinksPlugin()],
        }),
    },
    integrations: [
        starlight({
            title: "E2E for .NET",
            description: "Agentic end-to-end tests for .NET. Describe a goal, drive the app, then check the result with locators.",
            logo: {
                src: "./src/assets/logo.png",
                alt: "E2E",
            },
            favicon: "/favicon.png",
            customCss: [
                "@fontsource/inter/400.css",
                "@fontsource/inter/500.css",
                "@fontsource/inter/600.css",
                "@fontsource/inter/700.css",
                "@fontsource/dm-mono/400.css",
                "./src/styles/custom.css",
            ],
            expressiveCode: {
                themes: ["github-light", "github-dark"],
                useStarlightDarkModeSwitch: true,
                useStarlightUiThemeColors: true,
            },
            social: [
                { icon: "github", label: "GitHub", href: "https://github.com/hardkoded/e2e-dotnet" },
            ],
            editLink: {
                baseUrl: "https://github.com/hardkoded/e2e-dotnet/edit/main/docs-site/",
            },
            sidebar: [
                {
                    label: "Get started",
                    items: [
                        { label: "Introduction", slug: "docs" },
                        { label: "Quickstart", slug: "get-started" },
                        { label: "Core concepts", slug: "get-started/core-concepts" },
                        { label: "Coding agents", slug: "coding-agents" },
                    ],
                },
                {
                    label: "Platforms",
                    items: [
                        { label: "Web", slug: "get-started/web" },
                        { label: "Mobile", slug: "get-started/mobile" },
                        { label: ".NET MAUI", slug: "get-started/maui" },
                        { label: "Windows", slug: "get-started/windows" },
                    ],
                },
                {
                    label: "Features",
                    items: [
                        { label: "Signing in / sessions", slug: "features/signing-in" },
                        { label: "Caching & self-healing", slug: "features/caching" },
                        { label: "Test filters", slug: "docs/test-filters" },
                        { label: "Bug bashes", slug: "docs/bug-bash" },
                        { label: "Soft assertions & evidence", slug: "features/assertions" },
                        { label: "Video & screenshots", slug: "features/video" },
                        { label: "Working with the browser", slug: "features/browser" },
                    ],
                },
                {
                    label: "Examples",
                    items: [
                        { label: "Overview", slug: "examples" },
                        { label: "Three languages", slug: "examples/i18n" },
                        { label: "File uploads", slug: "examples/file-upload" },
                        { label: "Location", slug: "examples/location" },
                        { label: "Face ID and fingerprint", slug: "examples/biometrics" },
                        { label: "Deep links", slug: "examples/deep-links" },
                    ],
                },
                {
                    label: "Migrate",
                    items: [
                        { label: "Overview", slug: "migrate" },
                        { label: "Playwright .NET", slug: "migrate/playwright" },
                        { label: "Cypress", slug: "migrate/cypress" },
                        { label: "Selenium .NET", slug: "migrate/selenium" },
                        { label: "Detox", slug: "migrate/detox" },
                        { label: "Maestro", slug: "migrate/maestro" },
                        { label: "TesterArmy", slug: "migrate/testerarmy" },
                    ],
                },
                { label: "Integrations", items: [{ label: "Limrun", slug: "integrations/limrun" }, { label: "BrowserStack", slug: "integrations/browserstack" }, { label: "Pull request comments", slug: "integrations/github" }] },
                {
                    label: "Reference",
                    items: [
                        {
                            label: "API",
                            collapsed: true,
                            items: apiSidebar,
                        },
                    ],
                },
                {
                    label: "Project",
                    items: [
                        { label: "Compatibility", slug: "compatibility" },
                        {
                            label: "Releases",
                            items: [
                                { label: "Overview", slug: "releases" },
                                { label: "v0.8.0", slug: "releases/v0-8-0" },
                            ],
                        },
                        { label: "Contributing", slug: "contributing" },
                    ],
                },
            ],
        }),
    ],
});
