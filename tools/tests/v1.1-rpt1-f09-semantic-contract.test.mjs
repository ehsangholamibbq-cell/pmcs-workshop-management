import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const read = (path) => readFileSync(path, "utf8");
const contract = read("docs/architecture/pmcs-v1.1-rpt1-f09-governance-action-semantic-contract.md");
const issue = read("src/backend/Pmcs.Modules.ActionControl/Domain/ManagementIssue.cs");
const risk = read("src/backend/Pmcs.Modules.ActionControl/Domain/RiskManagement.cs");
const decision = read("src/backend/Pmcs.Modules.ActionControl/Domain/DecisionManagement.cs");
const escalation = read("src/backend/Pmcs.Modules.ActionControl/Domain/SlaAndEscalation.cs");
const action = read("src/backend/Pmcs.Modules.ActionControl/Domain/ManagementAction.cs");
const endpoints = read("src/backend/Pmcs.Modules.ActionControl/Endpoints/GovernanceEndpointSupport.cs");
const roles = read("src/backend/Pmcs.Modules.IdentityAccess/Services/ProjectPermissionService.cs");

test("F09 contract owns five registers and recognizes source chronology gaps", () => {
  assert.match(contract, /PMCS-RPT1-F09-SEMANTIC-001/u);
  assert.match(contract, /ActionControlDbContext[\s\S]*GET \/governance[\s\S]*GET \/actions[\s\S]*ممنوع/u);
  assert.match(endpoints, /Take\(200\)/u);
  assert.match(issue, /ResolvedAt[\s\S]*ClosedAt/u);
  assert.match(risk, /LastReviewedAt[\s\S]*ClosedAt/u);
  assert.match(decision, /DecidedAt[\s\S]*RecordedAt[\s\S]*void Supersede/u);
  assert.match(escalation, /LastRaisedAt = at[\s\S]*CloseFromSource/u);
  assert.match(action, /LastChangedAt[\s\S]*CompletedAt/u);
  assert.match(contract, /latest-row[\s\S]*InsufficientData` با count=`null`/u);
  assert.match(contract, /repeatable-read[\s\S]*manifest/u);
});

test("F09 pins strict input, full permission set and unclassified Action refusal", () => {
  assert.match(contract, /دقیقاً خالی `\{\}`/u);
  for (const permission of ["governance.read", "governance.sensitive.read", "actions.read"]) {
    assert.ok(contract.includes(`\`${permission}\``));
    assert.ok(roles.includes(`"${permission}"`));
  }
  assert.match(contract, /whole-definition[\s\S]*deny/u);
  assert.doesNotMatch(action, /public\s+RecordConfidentiality\s+Confidentiality/u);
  assert.match(contract, /ManagementAction[\s\S]*classification صریح ندارد[\s\S]*کل Run را fail-closed/u);
  assert.match(contract, /MS31 contract-only Runtime \/ Renderer \/ Migration \/ Catalog \/ API \/ Worker change: None/u);
});

test("F09 acceptance IDs are unique and cover five independent registers and guards", () => {
  const ids = [...contract.matchAll(/^\| `(F09-[A-Z]\d{2})` \|/gmu)].map((match) => match[1]);
  assert.equal(ids.length, 27);
  assert.equal(new Set(ids).size, ids.length);
  for (const id of ["F09-I01", "F09-R01", "F09-D01", "F09-E01", "F09-A01",
    "F09-C01", "F09-T01", "F09-P01", "F09-S01", "F09-B01", "F09-X01", "F09-G01"]) {
    assert.ok(ids.includes(id), `Missing ${id}`);
  }
  assert.match(contract, /Micro-Step بعد `S07-MS32` فقط Source مالک و Runtime Core محدود است/u);
});
