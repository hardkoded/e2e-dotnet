import { mkdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { redirectHtml } from "./redirect.mjs";

const docsSite = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const repoRoot = path.resolve(docsSite, "..");
const contentDir = path.join(docsSite, "src/content/docs");

const pages = [
    {
        source: "README.md",
        output: "guide.md",
        title: "Guide",
        label: "Guide",
        legacy: "README.html",
        target: "/guide/",
    },
    {
        source: "COMPATIBILITY.md",
        output: "compatibility.md",
        title: "Compatibility",
        label: "Compatibility",
        legacy: "COMPATIBILITY.html",
        target: "/compatibility/",
    },
    {
        source: "CONTRIBUTING.md",
        output: "contributing.md",
        title: "Contributing",
        label: "Contributing",
        legacy: "CONTRIBUTING.html",
        target: "/contributing/",
    },
];

const pageTargets = new Map(pages.map((page) => [page.source, page.target]));

const sectionRedirects = [
    ["get-started/web.html", "/get-started/web/"],
    ["get-started/mobile.html", "/get-started/mobile/"],
    ["get-started/maui.html", "/get-started/maui/"],
    ["get-started/windows.html", "/get-started/windows/"],
    ["samples/dariten.html", "/samples/dariten/"],
    ["samples/todomvc.html", "/samples/todomvc/"],
    ["samples/mobile-app.html", "/samples/mobile-app/"],
    ["samples/winapp-benchmark.html", "/samples/winapp-benchmark/"],
    ["toc.html", "/"],
];

await mkdir(contentDir, { recursive: true });

for (const page of pages) {
    const raw = await readFile(path.join(repoRoot, page.source), "utf8");
    const body = rewriteLinks(stripTitle(raw.replace(/^\uFEFF/, "")));
    const description = describe(body);
    const frontmatter = [
        "---",
        `title: ${JSON.stringify(page.title)}`,
        `description: ${JSON.stringify(description)}`,
        `editUrl: ${JSON.stringify(`https://github.com/hardkoded/e2e-dotnet/edit/main/${page.source}`)}`,
        "sidebar:",
        `  label: ${JSON.stringify(page.label)}`,
        "---",
        "",
    ].join("\n");
    await writeFile(path.join(contentDir, page.output), `${frontmatter}${body.trim()}\n`);
    await writeLegacy(page.legacy, page.target);
}

for (const [legacy, target] of sectionRedirects) {
    await writeLegacy(legacy, target);
}

console.log(`Synced ${pages.length} guide pages into ${path.relative(repoRoot, contentDir)}`);

function stripTitle(markdown) {
    return markdown.replace(/^#\s+[^\n]*\n+/, "");
}

function describe(markdown) {
    const paragraph = markdown
        .split(/\n\s*\n/)
        .map((block) => block.trim())
        .find((block) => block && !block.startsWith("#") && !block.startsWith("```") && !block.startsWith("|"));
    const text = (paragraph ?? "E2E for .NET")
        .replace(/\[([^\]]+)\]\([^)]+\)/g, "$1")
        .replace(/[`*_]/g, "")
        .replace(/\s+/g, " ")
        .trim();
    return text.length > 160 ? `${text.slice(0, 157).trimEnd()}...` : text;
}

function rewriteLinks(markdown) {
    return markdown.replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, (full, text, href) => {
        if (/^[a-z][a-z0-9+.-]*:/i.test(href) || href.startsWith("#") || href.startsWith("/")) return full;
        const hashAt = href.indexOf("#");
        const file = hashAt >= 0 ? href.slice(0, hashAt) : href;
        const hash = hashAt >= 0 ? href.slice(hashAt) : "";
        const target = pageTargets.get(file);
        if (!target) return full;
        return `[${text}](${target}${hash})`;
    });
}

async function writeLegacy(relativePath, target) {
    const destination = path.join(docsSite, "public", relativePath);
    await mkdir(path.dirname(destination), { recursive: true });
    await writeFile(destination, redirectHtml(target));
}
