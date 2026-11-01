export function decode(value) {
    return String(value)
        .replace(/&#x([0-9a-f]+);/gi, (_, hex) => String.fromCodePoint(Number.parseInt(hex, 16)))
        .replace(/&#(\d+);/g, (_, dec) => String.fromCodePoint(Number(dec)))
        .replace(/&lt;/g, "<")
        .replace(/&gt;/g, ">")
        .replace(/&quot;/g, '"')
        .replace(/&apos;/g, "'")
        .replace(/&amp;/g, "&");
}

// Flattens rendered comment markdown to one line of MDX-safe text.
// Keeps underscores (AWS_BEARER_TOKEN_BEDROCK) and keeps &lt; and &gt; escaped, because the text lands in MDX bodies.
export function plainText(markdown) {
    return markdown
        .replace(/\[([^\]]+)\]\([^)]*\)/g, "$1")
        .replace(/<a\s+id="[^"]+"><\/a>/g, "")
        .replace(/[`*]/g, "")
        .replace(/\s+/g, " ")
        .trim();
}
