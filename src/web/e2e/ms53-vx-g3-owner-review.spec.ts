import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const reviewDir = resolve(root, "docs/ux/review/ms53");
const desk = resolve(reviewDir, "index.html");
const manifestPath = resolve(reviewDir, "manifest.json");
const output = resolve(root, "src/web/artifacts/ms53-vx-g3-owner-review");

type Source = { key: string; sourceCommit: string; runId: string; artifactId: string; archiveDigest: string };
type File = { file: string; source: string; sourceFile: string; sha256: string; bytes: number; width?: number; height?: number; pages?: number; format?: string };
type Manifest = { schemaVersion: number; reviewId: string; priorReview: { path: string; sha256: string; imageCount: number }; sources: Source[]; files: File[] };
type PriorManifest = { images: Array<{ file: string; sha256: string; bytes: number; width: number; height: number }> };

const digest = (bytes: Buffer) => createHash("sha256").update(bytes).digest("hex");

test("MS53 owner review keeps source evidence, local links and responsive rendering", async ({ page }) => {
  test.setTimeout(180_000);
  const manifest = JSON.parse(readFileSync(manifestPath, "utf8")) as Manifest;
  expect(manifest.schemaVersion).toBe(1);
  expect(manifest.reviewId).toBe("PMCS-V1.1-UX2-MS53-VX-G3");
  expect(manifest.sources.map((source) => source.key).sort()).toEqual(["composition", "overlays"]);
  expect(manifest.files).toHaveLength(5);
  for (const source of manifest.sources) {
    expect(source.sourceCommit).toMatch(/^[0-9a-f]{40}$/u);
    expect(source.runId).toMatch(/^\d+$/u);
    expect(source.artifactId).toMatch(/^\d+$/u);
    expect(source.archiveDigest).toMatch(/^sha256:[0-9a-f]{64}$/u);
  }

  const priorPath = resolve(reviewDir, manifest.priorReview.path);
  const priorBytes = readFileSync(priorPath);
  expect(digest(priorBytes)).toBe(manifest.priorReview.sha256);
  const prior = JSON.parse(priorBytes.toString("utf8")) as PriorManifest;
  expect(prior.images).toHaveLength(manifest.priorReview.imageCount);
  for (const image of prior.images) {
    const bytes = readFileSync(resolve(dirname(priorPath), image.file));
    expect(bytes.length, image.file).toBe(image.bytes);
    expect(digest(bytes), image.file).toBe(image.sha256);
    expect(bytes.readUInt32BE(16), image.file).toBe(image.width);
    expect(bytes.readUInt32BE(20), image.file).toBe(image.height);
  }
  for (const file of manifest.files) {
    expect(manifest.sources.some((source) => source.key === file.source)).toBe(true);
    expect(file.sourceFile.endsWith(file.file.endsWith(".pdf") ? ".pdf" : ".png")).toBe(true);
    const bytes = readFileSync(resolve(reviewDir, file.file));
    expect(bytes.length, file.file).toBe(file.bytes);
    expect(digest(bytes), file.file).toBe(file.sha256);
    if (file.file.endsWith(".png")) {
      expect(bytes.readUInt32BE(16), file.file).toBe(file.width);
      expect(bytes.readUInt32BE(20), file.file).toBe(file.height);
    } else {
      expect(bytes.subarray(0, 5).toString()).toBe("%PDF-");
      expect(file.pages).toBe(1);
      expect(file.format).toBe("A4");
    }
  }

  const externalRequests: string[] = [];
  page.on("request", (request) => { if (/^https?:/u.test(request.url())) externalRequests.push(request.url()); });
  mkdirSync(output, { recursive: true });
  const captures: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  for (const [name, width, height] of [["desktop", 1440, 900], ["mobile", 390, 844], ["narrow", 320, 720]] as const) {
    await page.setViewportSize({ width, height });
    await page.goto(pathToFileURL(desk).href);
    await expect(page.getByRole("heading", { name: "مرور تصمیم VX-G3" })).toBeVisible();
    await expect(page.getByText("آمادهٔ بازبینی، نه Gate پذیرفته‌شده.")).toBeVisible();
    await page.evaluate(() => document.fonts.ready);
    expect(await page.evaluate(() => document.fonts.check("16px Vazirmatn"))).toBe(true);
    expect(await page.locator(".brand").evaluate((image: HTMLImageElement) => image.naturalWidth)).toBeGreaterThan(0);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${name} overflow`).toBe(true);
    const images = page.locator("figure img");
    await expect(images).toHaveCount(8);
    await images.evaluateAll(async (nodes) => {
      await Promise.all(nodes.map((node) => {
        const image = node as HTMLImageElement;
        image.loading = "eager";
        return image.decode();
      }));
    });
    const loaded = await images.evaluateAll((nodes) => nodes.map((node) => ({
      path: decodeURIComponent(new URL((node as HTMLImageElement).src).pathname),
      width: (node as HTMLImageElement).naturalWidth,
      height: (node as HTMLImageElement).naturalHeight,
    })));
    for (const image of loaded) {
      const current = manifest.files.find((file) => image.path.endsWith(`/ms53/${file.file}`));
      const earlier = prior.images.find((file) => image.path.endsWith(`/ms50/${file.file}`));
      expect(current ?? earlier, image.path).toBeDefined();
      expect(image.width).toBe((current ?? earlier)?.width);
      expect(image.height).toBe((current ?? earlier)?.height);
    }
    const screenshot = await page.screenshot({ fullPage: true, animations: "disabled", caret: "hide" });
    const filename = `review-${name}.png`;
    writeFileSync(resolve(output, filename), screenshot);
    captures.push({ name: filename, sha256: digest(screenshot), bytes: screenshot.length, width, height });
  }
  for (const step of ["ms44", "ms47", "ms49", "ms51", "ms52"]) {
    const link = page.locator(`a[href="../../prototypes/${step}/index.html"]`);
    await expect(link).toBeVisible();
    await link.click();
    expect(page.url()).toBe(pathToFileURL(resolve(root, `docs/ux/prototypes/${step}/index.html`)).href);
    await expect(page.locator("body")).toBeVisible();
    await page.goBack();
  }
  for (const href of ["../ms50/index.html", "../../pmcs-v1.1-component-state-matrix.md", "manifest.json", "../ms50/images.json", "composition-a4.pdf"]) {
    const link = page.locator(`a[href="${href}"]`).first();
    await expect(link).toBeVisible();
    expect(readFileSync(resolve(reviewDir, href)).length).toBeGreaterThan(0);
  }
  expect(externalRequests).toEqual([]);
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, reviewId: manifest.reviewId,
    source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    deskSha256: digest(readFileSync(desk)), manifestSha256: digest(readFileSync(manifestPath)),
    priorManifestSha256: manifest.priorReview.sha256, files: captures,
  }, null, 2) + "\n");
});
