import { defineCollection } from 'astro:content';
import { docsLoader } from '@astrojs/starlight/loaders';
import { docsSchema } from '@astrojs/starlight/schema';

export const collections = {
	docs: defineCollection({
		loader: docsLoader({
			// Keep namespace case and dots (api/E2E.NUnit). The default slugger
			// lowercases and deletes them, so E2E.Engine and E2E become one path.
			generateId({ entry }) {
				const normalized = entry.replace(/\\/g, "/");
				const withoutExt = normalized.replace(/\.(md|mdx|markdown|mdown|mkdn|mkd|mdwn|mdoc)$/i, "");
				if (withoutExt === "index" || withoutExt.endsWith("/index")) {
					return withoutExt.slice(0, -"index".length).replace(/\/$/, "") || "index";
				}
				return withoutExt;
			},
		}),
		schema: docsSchema(),
	}),
};
