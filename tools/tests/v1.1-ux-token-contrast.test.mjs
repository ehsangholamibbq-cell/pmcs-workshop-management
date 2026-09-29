import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
const css = readFileSync(resolve(root, "src/web/app/globals.css"), "utf8");
const rootBlock = css.match(/^:root\s*\{([^}]+)\}/u)?.[1];
assert.ok(rootBlock, "The active root token block must exist");
const tokens = new Map([...rootBlock.matchAll(/(--[a-z0-9-]+):\s*(#[0-9a-f]{6})\s*;/gu)].map(([, name, color]) => [name, color]));

function luminance(hex) {
  const [r, g, b] = [1, 3, 5].map(index => parseInt(hex.slice(index, index + 2), 16) / 255)
    .map(value => value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4);
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}
function contrast(first, second) {
  const a = luminance(first), b = luminance(second);
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
}

test("active semantic text tokens meet the project's 4.5 contrast floor on intended surfaces", () => {
  const pairs = [
    ["--text-primary", "--surface-card"],
    ["--text-primary", "--surface-canvas"],
    ["--text-secondary", "--surface-card"],
    ["--text-secondary", "--surface-canvas"],
    ["--text-secondary", "--surface-subtle"],
    ["--status-info", "--status-info-surface"],
    ["--status-success", "--status-success-surface"],
    ["--status-warning", "--status-warning-surface"],
    ["--status-danger", "--status-danger-surface"],
    ...["draft", "offline", "stale", "no-data", "no-permission"].map(name => [`--state-${name}`, "--surface-card"]),
    ["--insight-ai", "--surface-card"],
  ];
  for (const [foreground, background] of pairs) {
    assert.ok(tokens.has(foreground) && tokens.has(background), `${foreground}/${background} must have a concrete value`);
    const ratio = contrast(tokens.get(foreground), tokens.get(background));
    assert.ok(ratio >= 4.5, `${foreground} on ${background}: ${ratio.toFixed(2)} < 4.5`);
  }
  const primaryAction = contrast(tokens.get("--brand-green-700"), tokens.get("--surface-card"));
  assert.ok(primaryAction >= 4.5, `Primary action text contrast: ${primaryAction.toFixed(2)}`);
});

test("active focus and every G3 contract prototype retain an explicit reduced-motion branch", () => {
  assert.match(css, /:where\(a, button, input, select, textarea, summary, \[tabindex\]\):focus-visible\s*\{\s*outline:\s*3px solid var\(--focus-color\)/u);
  assert.match(css, /@media \(prefers-reduced-motion: reduce\)\s*\{[\s\S]*?animation-duration:\s*0\.01ms !important;[\s\S]*?transition-duration:\s*0\.01ms !important;/u);
  for (const number of [47, 49, 51, 52, 55, 56, 57, 58, 59, 60]) {
    const prototype = readFileSync(resolve(root, `docs/ux/prototypes/ms${number}/index.html`), "utf8");
    assert.match(prototype, /@media \(prefers-reduced-motion: reduce\)/u, `MS${number} must expose its motion override`);
  }
});
