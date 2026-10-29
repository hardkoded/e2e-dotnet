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
                { label: "Docs", slug: "docs" },
                {
                    label: "Get started",
                    items: [
                        { label: "Overview", slug: "get-started" },
                        { label: "Web", slug: "get-started/web" },
                        { label: "Mobile", slug: "get-started/mobile" },
                        { label: ".NET MAUI", slug: "get-started/maui" },
                        { label: "Windows", slug: "get-started/windows" },
                    ],
                },
                {
                    label: "Samples",
                    items: [
                        { label: "Overview", slug: "samples" },
                        { label: "Dariten (web)", slug: "samples/dariten" },
                        { label: "TodoMVC (web)", slug: "samples/todomvc" },
                        { label: "Mobile app (MAUI)", slug: "samples/mobile-app" },
                        { label: "WinApp benchmark", slug: "samples/winapp-benchmark" },
                    ],
                },
                { label: "Coding agents", slug: "coding-agents" },
                { label: "Integrations", items: [{ label: "Limrun", slug: "integrations/limrun" }] },
                { label: "Guide", slug: "guide" },
                {
                    label: "API",
                    collapsed: true,
                    items: apiSidebar,
                },
                { label: "Compatibility", slug: "compatibility" },
                { label: "Contributing", slug: "contributing" },
            ],
        }),
    ],
});
