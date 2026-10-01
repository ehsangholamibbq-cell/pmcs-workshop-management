import assert from "node:assert/strict";
import { mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from "node:fs";
import { dirname, relative, resolve } from "node:path";
import { gzipSync } from "node:zlib";

const root = resolve(import.meta.dirname, "../..");
const contract = JSON.parse(readFileSync(resolve(root, "tools/qa/vx-g5-budgets.json"), "utf8"));
function walk(path) { return readdirSync(path, { withFileTypes: true }).flatMap(entry =>
  entry.isDirectory() ? walk(resolve(path, entry.name)) : [resolve(path, entry.name)]); }
const files = walk(resolve(root, "src/web/.next/static")).filter(path => /\.(?:js|css)$/u.test(path));
const assets = files.map(path => ({ path: relative(root, path), bytes: statSync(path).size,
  gzipBytes: gzipSync(readFileSync(path)).length }));
const measurements = {
  allJavaScriptGzipBytes: assets.filter(asset => asset.path.endsWith(".js")).reduce((sum, asset) => sum + asset.gzipBytes, 0),
  allCssGzipBytes: assets.filter(asset => asset.path.endsWith(".css")).reduce((sum, asset) => sum + asset.gzipBytes, 0),
  fontBytes: statSync(resolve(root, "src/web/public/typography/Vazirmatn-variable.woff2")).size,
};
const output = resolve(root, "artifacts/qa/vx-g5-build-budget.json");
mkdirSync(dirname(output), { recursive: true });
writeFileSync(output, JSON.stringify({ contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
  scope: contract.scope, budgets: contract.build, measurements, assets }, null, 2) + "\n");
for (const [key, measured] of Object.entries(measurements)) {
  assert.ok(measured > 0 && measured <= contract.build[key], `${key} exceeded budget: ${measured}/${contract.build[key]}`);
}
console.log(JSON.stringify({ budgets: contract.build, measurements }));
