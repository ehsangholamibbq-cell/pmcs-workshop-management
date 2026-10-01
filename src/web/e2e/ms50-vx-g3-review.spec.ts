import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const desk = resolve(root, "docs/ux/review/ms50/index.html");
const manifestPath = resolve(root, "docs/ux/review/ms50/images.json");
const output = resolve(root, "src/web/artifacts/ms50-vx-g3-review");

type ReviewImage = { file: string; source: string; sourceFile: string; sha256: string; bytes: number; width: number; height: number };
type ReviewManifest = { schemaVersion: number; reviewId: string; sources: Array<{ key: string; sourceCommit: string; artifactId: string; archiveDigest: string }>; images: ReviewImage[] };

test("MS50 review desk preserves artifact evidence and renders at desktop and mobile", async ({ page }) => {
  test.setTimeout(180_000);
  const manifest = JSON.parse(readFileSync(manifestPath, "utf8")) as ReviewManifest;
  expect(manifest.schemaVersion).toBe(1);
  expect(manifest.reviewId).toBe("PMCS-V1.1-UX2-MS50-VX-G3");
  expect(manifest.sources.map((source) => source.key).sort()).toEqual(["active", "font", "foundation", "system"]);
  expect(manifest.images).toHaveLength(10);
  for (const source of manifest.sources) {
    expect(source.sourceCommit).toMatch(/^[0-9a-f]{40}$/u);
    expect(source.artifactId).toMatch(/^\d+$/u);
    expect(source.archiveDigest).toMatch(/^sha256:[0-9a-f]{64}$/u);
  }
  for (const image of manifest.images) {
    expect(image.file).toMatch(/^images\/[a-z-]+\.png$/u);
    expect(manifest.sources.some((source) => source.key === image.source)).toBe(true);
    expect(image.sourceFile).toMatch(/\.png$/u);
    const bytes = readFileSync(resolve(dirname(manifestPath), image.file));
    expect(bytes.length, image.file).toBe(image.bytes);
    expect(createHash("sha256").update(bytes).digest("hex"), image.file).toBe(image.sha256);
    expect(bytes.readUInt32BE(16), image.file).toBe(image.width);
    expect(bytes.readUInt32BE(20), image.file).toBe(image.height);
  }

  const externalRequests: string[] = [];
  page.on("request", (request) => { if (/^https?:/u.test(request.url())) externalRequests.push(request.url()); });
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  for (const [name, width, height] of [["desktop", 1440, 900], ["mobile", 390, 844], ["narrow", 320, 720]] as const) {
    await page.setViewportSize({ width, height });
    await page.goto(pathToFileURL(desk).href);
    await expect(page.getByRole("heading", { name: "بستهٔ بازبینی بصری VX-G3" })).toBeVisible();
    await page.evaluate(() => document.fonts.ready);
    expect(await page.evaluate(() => document.fonts.check("16px Vazirmatn"))).toBe(true);
    expect(await page.locator(".brand").evaluate((image: HTMLImageElement) => image.naturalWidth)).toBeGreaterThan(0);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${name} overflow`).toBe(true);
    const images = page.locator("figure img");
    await expect(images).toHaveCount(manifest.images.length);
    await images.evaluateAll(async (nodes) => {
      await Promise.all(nodes.map((node) => {
        const image = node as HTMLImageElement;
        image.loading = "eager";
        return image.decode();
      }));
    });
    const loaded = await images.evaluateAll((nodes) => nodes.map((node) => ({
      path: new URL((node as HTMLImageElement).src).pathname,
      width: (node as HTMLImageElement).naturalWidth,
      height: (node as HTMLImageElement).naturalHeight,
    })));
    expect(loaded.map((image) => image.path.slice(image.path.lastIndexOf("/images/") + 1))).toEqual(manifest.images.map((image) => image.file));
    for (let index = 0; index < loaded.length; index++) {
      expect(loaded[index].width).toBe(manifest.images[index].width);
      expect(loaded[index].height).toBe(manifest.images[index].height);
    }
    const screenshot = await page.screenshot({ fullPage: true, animations: "disabled", caret: "hide" });
    const filename = `review-${name}.png`;
    writeFileSync(resolve(output, filename), screenshot);
    files.push({ name: filename, sha256: createHash("sha256").update(screenshot).digest("hex"), bytes: screenshot.length, width, height });
  }
  for (const step of ["ms44", "ms47", "ms49"]) {
    const link = page.locator(`a[href="../../prototypes/${step}/index.html"]`);
    await expect(link).toBeVisible();
    await link.click();
    expect(page.url()).toBe(pathToFileURL(resolve(root, `docs/ux/prototypes/${step}/index.html`)).href);
    await expect(page.locator("body")).toBeVisible();
    await page.goBack();
  }
  expect(externalRequests).toEqual([]);
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, reviewId: manifest.reviewId,
    source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    deskSha256: createHash("sha256").update(readFileSync(desk)).digest("hex"),
    manifestSha256: createHash("sha256").update(readFileSync(manifestPath)).digest("hex"),
    files,
  }, null, 2) + "\n");
});
