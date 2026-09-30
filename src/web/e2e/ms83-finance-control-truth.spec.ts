import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";
import { projectId } from "./support";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms83-finance-control-truth");
const recordId = "70000000-0000-4000-8000-000000000083";

test("finance control hides an old record and commands after failed or forbidden read", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  async function capture(name: string, width: number, height: number,
    state: "current" | "loading" | "unavailable" | "forbidden") {
    const panel = page.getByTestId("finance-control");
    let bytes: Buffer | null = null;
    for (let attempt = 0; attempt < 8; attempt += 1) {
      await expect(panel).toHaveAttribute("data-read-state", state);
      await page.evaluate(() => document.fonts.ready);
      const headingTop = () => panel.getByRole("heading", { name: "واقعیت مالی و کنترل تنخواه" })
        .evaluate((element) => element.getBoundingClientRect().top);
      await expect.poll(async () => {
        const top = await headingTop();
        if (top < 120 || top >= 250) await panel.evaluate((element) =>
          window.scrollTo({ top: Math.max(0, window.scrollY + element.getBoundingClientRect().top - 150),
            behavior: "instant" }));
        await page.waitForTimeout(150);
        const settled = await headingTop();
        return settled >= 120 && settled < 250;
      }, { timeout: 10_000 }).toBe(true);
      await page.waitForTimeout(400);
      if (await panel.getAttribute("data-read-state") !== state) continue;
      const candidate = await page.screenshot({ animations: "disabled", caret: "hide" });
      if (await panel.getAttribute("data-read-state") !== state || candidate.length < 10_000) continue;
      bytes = candidate;
      break;
    }
    if (!bytes) throw new Error(`Finance control did not remain ${state} during ${name}`);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width, height });
  }

  const record = { id: recordId, projectId, type: "Payment", transactionDate: "2026-09-29",
    amount: 100000, currencyCode: "IRR", description: "سند شاهد مالی", counterparty: null,
    documentNumber: null, contractReference: null, contractId: null, commitmentId: null,
    costCenterCode: null, partyId: null, locationId: null, locationCode: null,
    wbsReference: null, status: "Draft", reviewComment: null, revision: 1 };
  let responseStatus = 200;
  let releaseFailure: () => void = () => {};
  const failureGate = new Promise<void>(resolve => { releaseFailure = resolve; });
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/finance/records`, async route => {
    if (responseStatus === 503) await failureGate;
    return route.fulfill(responseStatus === 200
      ? { status: 200, contentType: "application/json", body: JSON.stringify([record]) }
      : { status: responseStatus, contentType: "application/problem+json",
        body: JSON.stringify({ status: responseStatus, title: "Sensitive finance detail" }) });
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/projects/${projectId}`);
  const panel = page.getByTestId("finance-control");
  await expect(panel).toHaveAttribute("data-read-state", "current");
  await expect(panel.locator(".finance-list")).toContainText(record.description);
  await capture("finance-390-current.png", 390, 844, "current");

  responseStatus = 503;
  await page.reload();
  await expect(panel).toHaveAttribute("data-read-state", "loading");
  await expect(panel.locator(".finance-list")).toHaveCount(0);
  await page.setViewportSize({ width: 320, height: 720 });
  await capture("finance-320-refreshing.png", 320, 720, "loading");
  releaseFailure();
  await expect(panel).toHaveAttribute("data-read-state", "unavailable");
  await expect(panel.getByRole("alert")).not.toContainText("Sensitive finance detail");
  await expect(panel.getByRole("alert")).toHaveCSS("background-color", "rgb(255, 240, 237)");
  await expect(panel.locator(".finance-list")).toHaveCount(0);
  await expect(panel.getByRole("button", { name: "ثبت پیش‌نویس", exact: true })).toHaveCount(0);
  await capture("finance-320-read-error.png", 320, 720, "unavailable");

  responseStatus = 403;
  await page.reload();
  await expect(panel).toHaveAttribute("data-read-state", "forbidden");
  await expect(panel.getByRole("alert")).toContainText("دادهٔ قبلی نمایش داده نمی‌شود");
  await expect(panel.getByRole("alert")).toHaveCSS("background-color", "rgb(255, 240, 237)");
  await capture("finance-320-access-revoked.png", 320, 720, "forbidden");

  responseStatus = 200;
  await page.reload();
  await expect(panel).toHaveAttribute("data-read-state", "current");
  await expect(panel.locator(".finance-list")).toContainText(record.description);
  await capture("finance-320-restored.png", 320, 720, "current");

  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    route: `/projects/${projectId}`, owner: "VX-G4 finance control read truth", files,
  }, null, 2) + "\n");
});
