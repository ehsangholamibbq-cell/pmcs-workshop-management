import { readdirSync, readFileSync } from "node:fs";
import { extname, join, relative } from "node:path";
import ts from "typescript";

const roots = ["app", "components"];
const visibleAttributes = new Set(["alt", "aria-label", "placeholder", "title"]);
const permittedAcronyms = /\b(?:WBS|RFI|HSE)\b/g;
const latin = /[A-Za-z]/;
const persian = /[\u0600-\u06ff]/u;
const failures = [];

for (const root of roots) {
  for (const file of walk(root)) auditFile(file);
}

if (failures.length > 0) {
  console.error("متن لاتین بدون معادل فارسی در رابط کاربری پیدا شد:");
  failures.forEach((failure) => console.error(`- ${failure}`));
  process.exitCode = 1;
} else {
  console.log("ممیزی رابط فارسی با موفقیت انجام شد.");
}

function walk(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) return walk(path);
    return extname(path) === ".tsx" ? [path] : [];
  });
}

function auditFile(file) {
  const source = ts.createSourceFile(file, readFileSync(file, "utf8"), ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);

  function visit(node) {
    if (ts.isJsxText(node)) checkVisible(node.text, node);

    if (ts.isJsxAttribute(node) && visibleAttributes.has(node.name.getText(source))) {
      if (node.initializer && ts.isStringLiteral(node.initializer)) checkVisible(node.initializer.text, node);
      if (node.initializer && ts.isJsxExpression(node.initializer) && node.initializer.expression && ts.isStringLiteral(node.initializer.expression)) {
        checkVisible(node.initializer.expression.text, node);
      }
    }

    if (ts.isStringLiteral(node) && !isModuleSpecifier(node)) {
      checkMixedText(node.text, node);
    }

    ts.forEachChild(node, visit);
  }

  visit(source);

  function checkVisible(value, node) {
    const normalized = value.replace(permittedAcronyms, "").trim();
    if (latin.test(normalized)) addFailure(value, node);
  }

  function checkMixedText(value, node) {
    if (!persian.test(value)) return;
    const normalized = value.replace(permittedAcronyms, "");
    if (latin.test(normalized)) addFailure(value, node);
  }

  function addFailure(value, node) {
    const location = source.getLineAndCharacterOfPosition(node.getStart(source));
    failures.push(`${relative(".", file)}:${location.line + 1} — ${value.trim().replace(/\s+/g, " ")}`);
  }
}

function isModuleSpecifier(node) {
  return (ts.isImportDeclaration(node.parent) || ts.isExportDeclaration(node.parent))
    && node.parent.moduleSpecifier === node;
}
