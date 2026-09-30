import { createHash } from "node:crypto";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { createRequire } from "node:module";
import { resolve } from "node:path";
import { expect, type Page, type TestInfo } from "@playwright/test";
import type { AxeResults } from "axe-core";

const require = createRequire(import.meta.url);
const budgets = JSON.parse(readFileSync(resolve("../../tools/qa/vx-g5-budgets.json"), "utf8")).browser;
export const viewports = [{ width: 320, height: 900 }, { width: 390, height: 844 },
  { width: 768, height: 1024 }, { width: 1440, height: 900 }];

interface EvidenceFile { name: string; sha256: string; bytes: number; width?: number; height?: number; [key: string]: unknown }
export function evidence(testInfo: TestInfo, suite: string) {
  const output = resolve("artifacts/vx-g5", testInfo.project.name, suite);
  mkdirSync(output, { recursive: true });
  const indexPath = resolve(output, "index.json");
  const previous = existsSync(indexPath) ? JSON.parse(readFileSync(indexPath, "utf8")) : {};
  const files: EvidenceFile[] = previous.files ?? [];
  const measurements: Array<{ name: string; value: unknown }> = previous.measurements ?? [];
  const index = () => writeFileSync(indexPath, JSON.stringify({ contractVersion: 1,
    source: process.env.PMCS_SOURCE_HEAD_SHA ?? null, browser: testInfo.project.name, suite,
    files, measurements }, null, 2) + "\n");
  function file(name: string, bytes: Buffer, metadata: Record<string, unknown> = {}) {
    expect(files.some(item => item.name === name)).toBe(false);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"), bytes: bytes.length, ...metadata });
    index();
  }
  return { file,
    record(name: string, value: unknown) { measurements.push({ name, value }); index(); },
    async capture(page: Page, name: string) {
      await page.evaluate(() => document.fonts.ready);
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - innerWidth);
      expect(overflow).toBeLessThanOrEqual(budgets.globalHorizontalOverflowPixels);
      const bytes = await page.screenshot({ animations: "disabled", caret: "hide", scale: "css" });
      const viewport = page.viewportSize()!;
      expect(bytes.readUInt32BE(16)).toBe(viewport.width);
      expect(bytes.readUInt32BE(20)).toBe(viewport.height);
      file(name, bytes, { width: viewport.width, height: viewport.height });
    },
    async axe(page: Page, name: string) {
      await page.addScriptTag({ path: require.resolve("axe-core/axe.min.js") });
      const result = await page.evaluate(async () => {
        const axe = (window as unknown as { axe: typeof import("axe-core") }).axe;
        return axe.run(document, { runOnly: { type: "tag", values: ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"] } });
      }) as AxeResults;
      file(name + ".axe.json", Buffer.from(JSON.stringify(result, null, 2) + "\n"),
        { violations: result.violations.length, incomplete: result.incomplete.length, passes: result.passes.length });
      expect(result.violations.map(item => ({ id: item.id, impact: item.impact,
        nodes: item.nodes.map(node => ({ target: node.target, failureSummary: node.failureSummary })) }))).toEqual([]);
    },
  };
}

export async function auditSurface(page: Page, report: ReturnType<typeof evidence>, name: string) {
  await page.evaluate(() => document.fonts.ready);
  const result = await page.evaluate(() => {
    const visible = [...document.querySelectorAll("body *")].filter(element => {
      const box = element.getBoundingClientRect();
      return box.width > 0 && box.height > 0 && getComputedStyle(element).visibility !== "hidden";
    });
    const duration = (value: string) => Math.max(0, ...value.split(",").map(part =>
      Number.parseFloat(part) * (part.trim().endsWith("ms") ? 1 : 1000)));
    const styles = visible.flatMap(element => [getComputedStyle(element),
      getComputedStyle(element, "::before"), getComputedStyle(element, "::after")]);
    return { lang: document.documentElement.lang, dir: document.documentElement.dir,
      font: getComputedStyle(document.body).fontFamily,
      fontLoaded: [...document.fonts].some(face => face.family.includes("Vazirmatn") && face.status === "loaded"),
      reducedMotion: matchMedia("(prefers-reduced-motion: reduce)").matches,
      maximumMotionMilliseconds: Math.max(0, ...styles.map(style => Math.max(duration(style.animationDuration), duration(style.transitionDuration)))),
      persistentAnimations: styles.filter(style => style.animationName !== "none" && style.animationIterationCount.includes("infinite")).length,
      overflow: document.documentElement.scrollWidth - innerWidth };
  });
  report.record(name + ":surface", result);
  expect(result.lang).toBe("fa"); expect(result.dir).toBe("rtl");
  expect(result.font).toContain("Vazirmatn"); expect(result.fontLoaded).toBe(true);
  expect(result.reducedMotion).toBe(true);
  expect(result.maximumMotionMilliseconds).toBeLessThanOrEqual(budgets.reducedMotionDurationMilliseconds);
  expect(result.persistentAnimations).toBe(budgets.persistentAnimationCount);
  expect(result.overflow).toBeLessThanOrEqual(budgets.globalHorizontalOverflowPixels);
  await page.evaluate(() => { document.body.tabIndex = -1; document.body.focus(); document.body.removeAttribute("tabindex"); });
  await page.keyboard.press("Tab");
  const focus = await page.evaluate(() => {
    const element = document.activeElement!;
    const style = getComputedStyle(element); const box = element.getBoundingClientRect();
    return { tag: element.tagName, label: element.getAttribute("aria-label") ?? element.textContent?.trim().slice(0, 100),
      width: box.width, height: box.height, outline: style.outlineStyle, outlineWidth: Number.parseFloat(style.outlineWidth) };
  });
  report.record(name + ":keyboard", focus);
  expect(focus.tag).not.toBe("BODY"); expect(focus.width).toBeGreaterThan(0); expect(focus.height).toBeGreaterThan(0);
  expect(["solid", "auto"]).toContain(focus.outline); expect(focus.outlineWidth).toBeGreaterThanOrEqual(2);
  const navigation = page.locator(".sidebar-mobile-navigation > button");
  if (await navigation.isVisible()) {
    await navigation.focus(); await page.keyboard.press("Enter");
    await expect(navigation).toHaveAttribute("aria-expanded", "true");
    await page.keyboard.press("Enter"); await expect(navigation).toHaveAttribute("aria-expanded", "false");
    const latency = await navigation.evaluate(async element => {
      const expanded = new Promise<number>(resolve => {
        const observer = new MutationObserver(() => {
          if (element.getAttribute("aria-expanded") === "true") {
            observer.disconnect(); resolve(performance.now());
          }
        });
        observer.observe(element, { attributes: true, attributeFilter: ["aria-expanded"] });
      });
      const start = performance.now(); (element as HTMLButtonElement).click();
      return (await expanded) - start;
    });
    report.record(name + ":disclosureMilliseconds", latency);
    expect(latency).toBeLessThanOrEqual(budgets.navigationDisclosureMilliseconds);
    await expect(navigation).toHaveAttribute("aria-expanded", "true");
    await navigation.click(); await expect(navigation).toHaveAttribute("aria-expanded", "false");
  }
  await report.axe(page, name);
}

export const routeReadyBudget = budgets.routeReadyMilliseconds as number;
