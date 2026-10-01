import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const read = (path) => readFileSync(path, "utf8");
const contract = read("docs/architecture/pmcs-v1.1-rpt1-f07-technical-office-semantic-contract.md");
const rules = read("src/backend/Pmcs.Modules.TechnicalOffice/Domain/TechnicalOfficeRules.cs");
const document = read("src/backend/Pmcs.Modules.TechnicalOffice/Domain/TechnicalDocument.cs");
const transmittal = read("src/backend/Pmcs.Modules.TechnicalOffice/Domain/TechnicalTransmittal.cs");
const rfi = read("src/backend/Pmcs.Modules.TechnicalOffice/Domain/TechnicalRfi.cs");
const submittal = read("src/backend/Pmcs.Modules.TechnicalOffice/Domain/TechnicalSubmittal.cs");
const endpoints = read("src/backend/Pmcs.Modules.TechnicalOffice/Endpoints/TechnicalOfficeEndpoints.cs");

test("F07 distinguishes four owner lifecycles and rejects current-state historical inference", () => {
  assert.match(contract, /PMCS-RPT1-F07-SEMANTIC-001/u);
  assert.match(contract, /Document \/ RFI \/ Submittal \/ Transmittal/u);
  assert.match(contract, /GET \/state[\s\S]*۵۰۰ Document، ۱۰۰۰ Revision و ۵۰۰/u);
  assert.match(endpoints, /Take\(500\)[\s\S]*Take\(1_000\)[\s\S]*Take\(500\)/u);
  assert.match(contract, /HistoricalTransitionUnavailable[\s\S]*SourceCoverageIncomplete/u);
  assert.match(contract, /current-only[\s\S]*completeness proof/u);
  assert.match(document, /IssuedThroughTransmittalId[\s\S]*IssuedAt[\s\S]*SupersededAt/u);
  assert.match(contract, /Purpose=Approved[\s\S]*Status=Approved[\s\S]*صدور رسمی/u);
  assert.match(transmittal, /TransmittalStatus\.Issued[\s\S]*TransmittalStatus\.Acknowledged/u);
  assert.match(contract, /Ack پس از cutoff[\s\S]*`Issued`/u);
  assert.match(rfi, /RfiStatus\.Answered[\s\S]*RfiStatus\.ResponseAccepted[\s\S]*RfiStatus\.Closed/u);
  assert.match(contract, /Answered[\s\S]*ResponseAccepted[\s\S]*Closed/u);
  assert.match(submittal, /SubmittalReviewOutcome\.ForInformation => SubmittalStatus\.ApprovedAsNoted/u);
  assert.match(contract, /ForInformation[\s\S]*ReviewOutcome[\s\S]*Approval/u);
});

test("F07 pins strict input, both technical read permissions and no unsafe joins", () => {
  assert.ok(contract.includes("دقیقاً JSON object خالی `{}`"));
  assert.match(contract, /technical\.read` و `technical\.confidential\.read`/u);
  assert.match(endpoints, /"technical\.read"/u);
  assert.match(endpoints, /"technical\.confidential\.read"/u);
  assert.match(contract, /حداقل `Confidential`/u);
  assert.match(contract, /classification کمتر منتشر نمود/u);
  assert.match(contract, /هیچ گردش مالی\/تعهد F05[\s\S]*قرارداد\/خرید\/تأمین F06/u);
  assert.match(contract, /Runtime \/ Renderer \/ Migration \/ Catalog \/ API \/ Worker change: None/u);
  assert.match(rules, /public enum RfiStatus[\s\S]*ClarificationRequired[\s\S]*Closed/u);
  assert.match(rules, /public enum SubmittalReviewOutcome[\s\S]*ForInformation/u);
});

test("F07 acceptance matrix covers source truth, history, security and deterministic bounds", () => {
  const ids = [...contract.matchAll(/^\| `(F07-[A-Z]\d{2})` \|/gmu)].map((match) => match[1]);
  assert.equal(ids.length, 27);
  assert.equal(new Set(ids).size, ids.length);
  for (const id of [
    "F07-D01", "F07-T01", "F07-R02", "F07-S02",
    "F07-C01", "F07-C02", "F07-P01", "F07-P03",
    "F07-H01", "F07-B01", "F07-X01",
  ]) {
    assert.ok(ids.includes(id), "Missing " + id);
  }
  assert.match(contract, /گام بعدی `S07-MS23` فقط Runtime Core محدود F07/u);
});
