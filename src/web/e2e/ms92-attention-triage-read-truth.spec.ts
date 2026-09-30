import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";
import { projectId } from "./support";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms92-attention-triage-read-truth");

test("attention commands require a current command snapshot and revocation hides the old item", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  async function capture(name: string, width: number, height: number, state: string) {
    await expect(page.locator(".project-print-shell")).toHaveAttribute("data-command-read-state", state);
    await page.evaluate(() => document.fonts.ready);
    if (["current", "cached"].includes(state)) {
      await page.locator("#attention").evaluate((element) => window.scrollTo({
        top: Math.max(0, window.scrollY + element.getBoundingClientRect().top - 150), behavior: "instant",
      }));
      await page.waitForTimeout(300);
    }
    await expect(page.locator(".project-print-shell")).toHaveAttribute("data-command-read-state", state);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    const bytes = await page.screenshot({ animations: "disabled", caret: "hide" });
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width, height });
  }

  const item = { sourceReportId: "80000000-0000-4000-8000-000000000090",
    sourceFactId: "81000000-0000-4000-8000-000000000090", reportDate: "2026-09-29",
    kind: "Issue", description: "بررسی پیشرفت تست", category: null, locationName: "کارگاه",
    locationId: null, observedImpact: null, priority: "Unassessed", ageDays: 1,
    ageBand: "New", status: "NeedsTriage", referenceCode: null, disposition: "NeedsTriage",
    actionId: null, dispositionReason: null, dispositionAt: null };
  let responseStatus = 200;
  let commands = 0;
  let releaseFailure: () => void = () => {};
  const failureGate = new Promise<void>((resolve) => { releaseFailure = resolve; });
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/attention/**`, (route) => {
    commands += 1;
    return route.fulfill({ status: 403, contentType: "application/problem+json",
      body: JSON.stringify({ status: 403, title: "Sensitive attention detail" }) });
  });
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/command-center`, async (route) => {
    if (responseStatus === 503) await failureGate;
    if (responseStatus !== 200) return route.fulfill({ status: responseStatus,
      contentType: "application/problem+json",
      body: JSON.stringify({ status: responseStatus, title: "Sensitive project detail" }) });
    const response = await route.fetch();
    const model = await response.json();
    return route.fulfill({ response, contentType: "application/json", body: JSON.stringify({
      ...model, canTriage: true, hasSnapshot: true,
      snapshot: { ...(model.snapshot ?? {}), snapshotId: "82000000-0000-4000-8000-000000000090",
        calculationVersion: "test", asOfDate: "2026-09-29", calculatedAt: "2026-09-29T12:00:00Z",
        operationalStatus: "Watch", approvedReportDays: 1, coverageStatus: "Sufficient",
        coveragePercent: 50, coverageBasis: "SevenCalendarDays", expectedReportDays: 7,
        attentionItems: [item] },
    }) });
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/projects/${projectId}`);
  const triage = page.locator(".command-attention-list");
  await expect(triage.getByRole("button", { name: "تبدیل به اقدام" })).toBeVisible();
  await capture("attention-390-current.png", 390, 844, "current");

  await page.context().setOffline(true);
  await expect(page.locator(".project-print-shell")).toHaveAttribute("data-command-read-state", "cached");
  await expect(triage.getByRole("button", { name: "تبدیل به اقدام" })).toHaveCount(0);
  await expect(triage.getByRole("status")).toContainText("نسخهٔ محلی فقط برای مرور است");
  await page.setViewportSize({ width: 320, height: 720 });
  await capture("attention-320-cached.png", 320, 720, "cached");

  responseStatus = 503;
  await page.context().setOffline(false);
  await expect(page.locator(".project-print-shell")).toHaveAttribute("data-command-read-state", "loading");
  await expect(triage).toHaveCount(0);
  await capture("attention-320-refreshing.png", 320, 720, "loading");
  releaseFailure();
  await expect(page.locator(".project-print-shell")).toHaveAttribute("data-command-read-state", "cached");
  await expect(triage.getByRole("button", { name: "تبدیل به اقدام" })).toHaveCount(0);
  await capture("attention-320-read-error-cached.png", 320, 720, "cached");
  expect(commands).toBe(0);

  responseStatus = 403;
  await page.reload();
  await expect(page.locator(".project-print-shell")).toHaveAttribute("data-command-read-state", "forbidden");
  await expect(triage).toHaveCount(0);
  await expect(page.locator(".project-print-shell .collaboration-state[role='alert']"))
    .not.toContainText("Sensitive project detail");
  await capture("attention-320-access-revoked.png", 320, 720, "forbidden");

  responseStatus = 200;
  await page.reload();
  await expect(triage.getByRole("button", { name: "بستن با دلیل" })).toBeVisible();
  await expect.poll(async () => {
    if (commands > 0) return commands;
    const open = triage.getByRole("button", { name: "بستن با دلیل" });
    if (await open.isVisible().catch(() => false)) {
      await open.click({ timeout: 1_000 }).catch(() => undefined);
    }
    const reason = triage.getByLabel("دلیل بستن");
    if (await reason.isVisible().catch(() => false)) {
      await reason.fill("نیازمند بررسی", { timeout: 1_000 }).catch(() => undefined);
    }
    const submit = triage.getByRole("button", { name: "ثبت تصمیم" });
    if (await submit.isVisible().catch(() => false)) {
      await submit.evaluate((element: HTMLButtonElement) => element.click()).catch(() => undefined);
    }
    return commands;
  }, { timeout: 30_000 }).toBe(1);
  await expect(page.locator(".project-print-shell")).toHaveAttribute("data-command-read-state", "forbidden");
  expect(commands).toBe(1);
  await capture("attention-320-command-revoked.png", 320, 720, "forbidden");

  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    route: `/projects/${projectId}`, owner: "VX-G4 attention triage read truth", files,
  }, null, 2) + "\n");
});
