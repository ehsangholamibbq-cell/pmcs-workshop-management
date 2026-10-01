import assert from "node:assert/strict";
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { dirname, relative, resolve } from "node:path";

const root = resolve(import.meta.dirname, "../..");
const web = resolve(root, "src/web");
function walk(path) {
  return readdirSync(path, { withFileTypes: true }).flatMap(entry =>
    entry.isDirectory() ? walk(resolve(path, entry.name)) : [resolve(path, entry.name)]);
}
const entries = walk(resolve(web, "app")).filter(path => /\/(?:page|layout|loading|error|not-found)\.tsx$/u.test(path));
const routes = entries.filter(path => path.endsWith("/page.tsx")).map(path =>
  "/" + relative(resolve(web, "app"), dirname(path))).sort();
const visited = new Set();
function visit(path) {
  if (visited.has(path)) return;
  visited.add(path);
  const source = readFileSync(path, "utf8");
  for (const match of source.matchAll(/(?:from\s*|import\s*\(?)["']([^"']+)["']/gu)) {
    const specifier = match[1];
    const base = specifier.startsWith("@/") ? resolve(web, specifier.slice(2))
      : specifier.startsWith(".") ? resolve(dirname(path), specifier) : null;
    if (!base) continue;
    const resolved = [base, base + ".tsx", base + ".ts", resolve(base, "index.tsx"), resolve(base, "index.ts")]
      .find(candidate => /\.tsx?$/u.test(candidate) && existsSync(candidate));
    if (resolved) visit(resolved);
  }
}
entries.forEach(visit);
const consumers = [...visited].filter(path => path.includes("/components/") && path.endsWith(".tsx"))
  .map(path => relative(root, path)).sort();
const ledgerPath = resolve(root, "tools/qa/vx-g4-consumers.json");
const ledger = JSON.parse(readFileSync(ledgerPath, "utf8"));
assert.equal(ledger.contractVersion, 1);
assert.deepEqual(ledger.consumers.map(item => item.path).sort(), consumers,
  "Active consumer ledger drifted: inspect new or removed route imports before claiming G4.");
assert.ok(ledger.consumers.every(item => item.checkpoints.length > 0));
assert.equal(routes.length, 11);
const result = { contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
  scope: "Static import reachability, not proof of runtime acceptance or visual qualification.",
  routes, consumers: ledger.consumers, inactiveConsumers: ledger.inactiveConsumers };
const output = resolve(root, "artifacts/qa/vx-g4-consumers.json");
mkdirSync(dirname(output), { recursive: true });
writeFileSync(output, JSON.stringify(result, null, 2) + "\n");
console.log(JSON.stringify({ routes: routes.length, activeConsumers: consumers.length, output }));
