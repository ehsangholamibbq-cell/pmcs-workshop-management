import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const read = (path) => readFileSync(path, "utf8");
const contract = read("docs/architecture/pmcs-v1.1-rpt1-f08-quality-hse-semantic-contract.md");
const state = read("src/backend/Pmcs.Modules.QualitySafety/Endpoints/QualitySafetySetupEndpoints.cs");
const intake = read("src/backend/Pmcs.Modules.QualitySafety/Domain/QualitySafetyIntake.cs");
const quality = read("src/backend/Pmcs.Modules.QualitySafety/Domain/QualityRecords.cs");
const hse = read("src/backend/Pmcs.Modules.QualitySafety/Domain/HseRecords.cs");
const rules = read("src/backend/Pmcs.Modules.QualitySafety/Domain/QualitySafetyRules.cs");
const roles = read("src/backend/Pmcs.Modules.IdentityAccess/Services/ProjectPermissionService.cs");

test("F08 pins independent owner, bounded source and missing transition chronology", () => {
  assert.match(contract, /PMCS-RPT1-F08-SEMANTIC-001/u);
  assert.match(contract, /Quality و HSE[\s\S]*دو بخش مستقل/u);
  assert.match(contract, /QualitySafetyDbContext[\s\S]*GET \/quality-safety\/state[\s\S]*ممنوع/u);
  assert.match(state, /Take\(100\)[\s\S]*Take\(150\)[\s\S]*Take\(120\)/u);
  assert.match(contract, /InsufficientData[\s\S]*null[\s\S]*latest-row/u);
  assert.match(contract, /repeatable-read[\s\S]*manifest/u);
  assert.match(intake, /TriagedAt[\s\S]*BeginTriage/u);
  assert.match(quality, /ReadinessRecorded[\s\S]*ResultRecorded/u);
  assert.match(hse, /ApprovedAt[\s\S]*PermitStatus\.Active/u);
});

test("F08 has strict empty input, whole-definition permissions and classification refusal", () => {
  assert.match(contract, /دقیقاً خالی `\{\}`/u);
  assert.match(contract, /quality\.read`، `hse\.read` و `hse\.confidential\.read`/u);
  assert.match(roles, /\["ProjectManager"\][\s\S]*"\*"/u);
  assert.match(roles, /\["QualityController"\][\s\S]*"quality\.read"/u);
  assert.match(roles, /\["HseOfficer"\][\s\S]*"hse\.confidential\.read"/u);
  assert.match(rules, /PersonalMedical = 4, LegalInvestigation = 5/u);
  assert.match(contract, /PersonalMedical` و `LegalInvestigation`[\s\S]*کل Run fail-closed/u);
  assert.match(contract, /ExposureBasisIncomplete[\s\S]*hash/u);
  assert.match(contract, /MS27 contract-only Runtime \/ Renderer \/ Migration \/ Catalog \/ API \/ Worker change: None/u);
});

test("F08 acceptance matrix is independent and complete", () => {
  const ids = [...contract.matchAll(/^\| `(F08-[A-Z]\d{2})` \|/gmu)].map((m) => m[1]);
  assert.equal(ids.length, 22);
  assert.equal(new Set(ids).size, ids.length);
  for (const id of ["F08-Q01", "F08-Q07", "F08-H01", "F08-H05", "F08-C01",
    "F08-C03", "F08-P01", "F08-P02", "F08-S01", "F08-B01", "F08-X01", "F08-R01"]) {
    assert.ok(ids.includes(id), `Missing ${id}`);
  }
  assert.match(contract, /Micro-Step بعد `S07-MS28` فقط[\s\S]*Runtime Core/u);
});
