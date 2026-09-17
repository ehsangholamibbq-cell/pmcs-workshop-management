import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const auditPath = new URL("../../docs/ux/pmcs-v1.1-visual-audit.md", import.meta.url);
const designPath = new URL("../../docs/ux/pmcs-v1.1-design-system-contract.md", import.meta.url);
const checkpointPath = new URL("../../docs/checkpoints/v1.1-ux1-review-candidate.md", import.meta.url);
const manifestPath = new URL("../../release/pmcs-v1.1-ux1-candidate.json", import.meta.url);

test("UX1 candidate remains review-only and does not claim qualification", async () => {
  const manifest = JSON.parse(await readFile(manifestPath, "utf8"));
  assert.equal(manifest.status, "owner-review-required");
  assert.equal(manifest.runtimeChanged, false);
  assert.equal(manifest.gates["VX-G2"], "approved");
  assert.equal(manifest.gates["VX-G3"], "awaiting-owner-review");
  assert.notEqual(manifest.gates["VX-G5"], "approved");
});

test("UX1 candidate preserves the locked product constraints", async () => {
  const manifest = JSON.parse(await readFile(manifestPath, "utf8"));
  assert.equal(manifest.parentCommit, "4e401ab9e2bfab5bd197e9789d7a87e91e8a5784");
  assert.equal(manifest.constraints.persianRtl, true);
  assert.equal(manifest.constraints.jalali, true);
  assert.equal(manifest.constraints.reducedMotion, true);
  assert.equal(manifest.constraints.arbitraryLoginCodeAllowed, false);
  assert.equal(manifest.constraints.destinationStartsAsDraft, true);
  assert.equal(manifest.constraints.duplicateOperationalData, false);
  assert.equal(manifest.constraints.duplicateFinancialData, false);
  assert.equal(manifest.constraints.duplicateMessages, false);
  assert.equal(manifest.constraints.duplicateFiles, false);
  assert.equal(manifest.constraints.duplicateAuditOrHistory, false);
});

test("design contract covers the approved visual and accessibility boundaries", async () => {
  const design = await readFile(designPath, "utf8");
  for (const required of [
    "مدیریت ممتاز",
    "Vazirmatn",
    "prefers-reduced-motion",
    "Login Experience Contract",
    "Focus-visible",
    "ProjectDuplicationStepper",
    "Agent هرگز به Chat box ساده تقلیل داده نمی‌شود"
  ]) {
    assert.match(design, new RegExp(required, "u"));
  }
  assert.doesNotMatch(design, /customHtml|customCss|customJavaScript/u);
});

test("audit and checkpoint keep open gates explicit", async () => {
  const [audit, checkpoint] = await Promise.all([
    readFile(auditPath, "utf8"),
    readFile(checkpointPath, "utf8")
  ]);
  assert.match(audit, /۶۳ مقدار Hex/u);
  assert.match(audit, /screenshot baseline/u);
  assert.match(checkpoint, /Owner Review Required/u);
  assert.match(checkpoint, /هیچ Runtime، Migration، Business Rule یا Permission را تغییر نمی‌دهد/u);
  assert.match(checkpoint, /VX-G3.*قبل از تأیید مالک محصول بسته اعلام نمی‌کند/u);
});

