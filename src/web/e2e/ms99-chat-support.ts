import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { resolve } from "node:path";
import { expect, type Locator, type Page } from "@playwright/test";

export function chatEvidence(page: Page, suite: string) {
  const output = resolve("artifacts/ms99-chat-command-draft", suite);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  return async (name: string, focus: Locator) => {
    await page.evaluate(() => document.fonts.ready);
    await focus.evaluate(element => window.scrollTo({ top: Math.max(0,
      window.scrollY + element.getBoundingClientRect().top - 150), behavior: "instant" }));
    await expect(focus).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    const bytes = await page.screenshot({ animations: "disabled", caret: "hide" });
    const viewport = page.viewportSize()!;
    expect(bytes.readUInt32BE(16)).toBe(viewport.width);
    expect(bytes.readUInt32BE(20)).toBe(viewport.height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width: viewport.width, height: viewport.height });
    writeFileSync(resolve(output, "index.json"), JSON.stringify({ contractVersion: 1,
      source: process.env.PMCS_SOURCE_HEAD_SHA ?? null, suite, files }, null, 2) + "\n");
  };
}
