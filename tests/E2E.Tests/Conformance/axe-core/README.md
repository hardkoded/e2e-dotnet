Test cases derived from the axe-core test suite
(https://github.com/dequelabs/axe-core/tree/develop/test), taken as Playwright
vendors them in `tests/assets/axe-core/` at microsoft/playwright
`b9a34ac7783a1b6c2e1dfff0c08ac744c048fa59`. Mozilla Public License 2.0, see
`LICENSE`.

- `implicit-role.json`: cases extracted from `/test/commons/aria/implicit-role.js`.
- `accessible-text.json`: cases extracted from `/test/commons/aria/accessible-text.js`.

Upstream keeps Playwright's files byte for byte as `.cjs` modules
(`packages/web/tests/conformance/axe-core/`). .NET cannot require them, so these
are the same arrays as JSON. Regenerate them by copying the upstream file again and running
`node -e "process.stdout.write(JSON.stringify(require('./implicit-role.cjs'), null, 1) + '\n')"`,
never by hand; a case the engine reads differently belongs in `../expected.txt` with a reason.
