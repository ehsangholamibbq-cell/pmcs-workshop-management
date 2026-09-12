"use client";

import { type FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import { PersianDateInput } from "@/components/persian-date-input";
import {
  amendMilestoneProgressUpdate,
  amendPlanningBaseline,
  approveMilestoneProgressUpdate,
  approvePlanningBaseline,
  createMilestoneProgressUpdate,
  createPlanningBaseline,
  listMilestoneProgressUpdates,
  listPlanningBaselines,
  returnMilestoneProgressUpdate,
  returnPlanningBaseline,
  submitMilestoneProgressUpdate,
  submitPlanningBaseline,
  type MeasurementItemModel,
  type MilestoneProgressUpdateModel,
  type PlanningBaselineEntryInput,
  type PlanningBaselineKind,
  type PlanningBaselineModel,
  type PlanningEntryKind,
  type PlanningMode,
  type ProgressLedgerModel,
} from "@/lib/planning";
import {
  configureProjectPlanningMode,
  getProject,
  type ProjectModel,
} from "@/lib/projects";
import { toUserMessage } from "@/lib/localization";
import { formatPersianDate } from "@/lib/persian-date";

interface PlanningBasisControlProps {
  readonly apiBaseUrl: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly isOnline: boolean;
  readonly refreshToken: number;
  readonly measurementItems: readonly MeasurementItemModel[];
  readonly ledger: ProgressLedgerModel | null;
  readonly onChanged?: () => void;
}

type BaselineAction = "submit" | "approve" | "return";
type MilestoneAction = "submit" | "approve" | "return";

export function PlanningBasisControl(props: PlanningBasisControlProps) {
  const identity = useMemo(
    () => ({ tenantId: props.tenantId, userId: props.userId }),
    [props.tenantId, props.userId],
  );
  const [project, setProject] = useState<ProjectModel | null>(null);
  const [mode, setMode] = useState<PlanningMode>("None");
  const [baselines, setBaselines] = useState<readonly PlanningBaselineModel[]>([]);
  const [updates, setUpdates] = useState<readonly MilestoneProgressUpdateModel[]>([]);
  const [message, setMessage] = useState("در حال دریافت مبنای رسمی برنامه…");
  const [busy, setBusy] = useState(false);
  const [reviewComment, setReviewComment] = useState("");

  const [editingBaselineId, setEditingBaselineId] = useState<string | null>(null);
  const [versionCode, setVersionCode] = useState("");
  const [baselineTitle, setBaselineTitle] = useState("");
  const [sourceSystem, setSourceSystem] = useState("");
  const [sourceReference, setSourceReference] = useState("");
  const [entries, setEntries] = useState<readonly PlanningBaselineEntryInput[]>([]);
  const [entryKind, setEntryKind] = useState<PlanningEntryKind>("MeasurementItem");
  const [entryCode, setEntryCode] = useState("");
  const [entryTitle, setEntryTitle] = useState("");
  const [entryParentId, setEntryParentId] = useState("");
  const [entryMeasurementId, setEntryMeasurementId] = useState("");
  const [entryStart, setEntryStart] = useState("");
  const [entryFinish, setEntryFinish] = useState("");
  const [entryWeight, setEntryWeight] = useState("");
  const [entryExternalId, setEntryExternalId] = useState("");

  const [editingUpdateId, setEditingUpdateId] = useState<string | null>(null);
  const [milestoneEntryId, setMilestoneEntryId] = useState("");
  const [milestoneDate, setMilestoneDate] = useState("");
  const [milestonePercent, setMilestonePercent] = useState("");
  const [milestoneEvidence, setMilestoneEvidence] = useState("");
  const [milestoneNote, setMilestoneNote] = useState("");

  const load = useCallback(async () => {
    if (!props.isOnline) {
      setMessage("مبنای رسمی فقط هنگام اتصال به سرور به‌روز می‌شود.");
      return;
    }

    try {
      const [nextProject, nextBaselines, nextUpdates] = await Promise.all([
        getProject(props.apiBaseUrl, identity, props.projectId),
        listPlanningBaselines(props.apiBaseUrl, identity, props.projectId),
        listMilestoneProgressUpdates(props.apiBaseUrl, identity, props.projectId),
      ]);
      setProject(nextProject);
      setMode(nextProject.planningMode);
      setEntryKind((current) => compatibleEntryKind(nextProject.planningMode, current));
      setBaselines(nextBaselines);
      setUpdates(nextUpdates);
      setMessage(basisMessage(props.ledger));
    } catch (error) {
      setMessage(toUserMessage(error, "مبنای برنامه دریافت نشد یا دسترسی این کاربر محدود است."));
    }
  }, [identity, props.apiBaseUrl, props.isOnline, props.ledger, props.projectId]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timeoutId);
  }, [load, props.refreshToken]);

  const approvedBaseline = baselines.find((item) => item.status === "Approved") ?? null;
  const milestoneEntries = approvedBaseline?.entries.filter((item) => item.kind === "Milestone") ?? [];
  const summaryEntries = entries.filter((item) => item.kind === "Summary");
  const baselineKind = kindForMode(mode);
  const scheduleMode = mode === "Milestones" || mode === "WbsBaseline" || mode === "ExternalSchedule";

  async function saveMode() {
    if (!project || !props.isOnline) return;
    setBusy(true);
    setMessage("در حال ثبت حالت برنامه‌ریزی…");
    try {
      const updated = await configureProjectPlanningMode(
        props.apiBaseUrl,
        identity,
        props.projectId,
        project.revision,
        mode,
      );
      setProject(updated);
      clearBaselineEditor();
      setMessage(mode === "None"
        ? "حالت بدون برنامه رسمی ثبت شد؛ واقعیت اجرایی و اندازه‌گیری همچنان فعال است."
        : "حالت برنامه‌ریزی ثبت شد؛ برای تولید شاخص رسمی باید مبنای همان حالت تصویب شود.");
      props.onChanged?.();
    } catch (error) {
      setMessage(toUserMessage(error, "ثبت حالت برنامه‌ریزی ناموفق بود."));
    } finally {
      setBusy(false);
    }
  }

  function selectMeasurement(value: string) {
    setEntryMeasurementId(value);
    const selected = props.measurementItems.find((item) => item.id === value);
    if (selected) {
      setEntryCode(selected.code);
      setEntryTitle(selected.title);
    }
  }

  function addEntry() {
    if (!baselineKind) return;
    const resolvedKind = entryKindForMode(mode, entryKind);
    const weight = resolvedKind === "Summary" ? null : Number(entryWeight);
    const method = resolvedKind === "Summary"
      ? "None"
      : resolvedKind === "Milestone"
        ? "ManualPercent"
        : "QuantityBased";
    const measurementItemId = method === "QuantityBased" ? entryMeasurementId || null : null;
    const plannedStart = scheduleMode ? (resolvedKind === "Milestone" ? entryFinish : entryStart) || null : null;
    const plannedFinish = scheduleMode ? entryFinish || null : null;

    if (!entryCode.trim() || !entryTitle.trim()) {
      setMessage("کد و عنوان ردیف برنامه الزامی است.");
      return;
    }
    if (resolvedKind !== "Summary" && (!Number.isFinite(weight) || weight === null || weight <= 0)) {
      setMessage("وزن هر ردیف پیشرفت باید عددی مثبت باشد.");
      return;
    }
    if (method === "QuantityBased" && !measurementItemId) {
      setMessage("برای ردیف مقدارمحور یک قلم اندازه‌گیری فعال انتخاب کنید.");
      return;
    }
    if (scheduleMode && (!plannedStart || !plannedFinish)) {
      setMessage("تاریخ شروع و پایان برنامه برای ردیف زمان‌بندی‌شده الزامی است.");
      return;
    }
    if (mode === "ExternalSchedule" && !entryExternalId.trim()) {
      setMessage("شناسه ردیف در برنامه بیرونی الزامی است.");
      return;
    }

    setEntries((current) => [...current, {
      clientGeneratedId: crypto.randomUUID(),
      parentEntryId: entryParentId || null,
      code: entryCode.trim(),
      title: entryTitle.trim(),
      kind: resolvedKind,
      measurementMethod: method,
      measurementItemId,
      plannedStart,
      plannedFinish,
      weightPercent: weight,
      externalId: mode === "ExternalSchedule" ? entryExternalId.trim() : null,
      sortOrder: current.length + 1,
    }]);
    clearEntryEditor();
    setMessage("ردیف به پیش‌نویس محلی مبنا افزوده شد؛ هنوز رسمی نیست.");
  }

  async function saveBaseline(event: FormEvent) {
    event.preventDefault();
    if (!baselineKind || !project) return;
    const weightTotal = entries.reduce((sum, item) => sum + (item.weightPercent ?? 0), 0);
    if (!versionCode.trim() || !baselineTitle.trim() || entries.length === 0 || Math.abs(weightTotal - 100) > 0.0001) {
      setMessage("نسخه، عنوان و حداقل یک ردیف لازم است و جمع وزن ردیف‌های پیشرفت باید دقیقاً ۱۰۰٪ باشد.");
      return;
    }
    if (mode === "WbsBaseline" && !entries.some((item) => item.kind === "Summary")) {
      setMessage("خط مبنای ساختار شکست کار (WBS) حداقل به یک ردیف خلاصه نیاز دارد.");
      return;
    }
    if (mode === "ExternalSchedule" && (!sourceSystem.trim() || !sourceReference.trim())) {
      setMessage("نام سامانه منبع و مرجع نسخه برنامه بیرونی الزامی است.");
      return;
    }

    setBusy(true);
    setMessage("در حال ذخیره پیش‌نویس نسخه‌دار برنامه…");
    try {
      const editing = baselines.find((item) => item.id === editingBaselineId);
      if (editing) {
        await amendPlanningBaseline(props.apiBaseUrl, identity, props.projectId, editing, {
          title: baselineTitle,
          sourceSystem: sourceSystem.trim() || null,
          sourceReference: sourceReference.trim() || null,
          entries,
        });
      } else {
        await createPlanningBaseline(props.apiBaseUrl, identity, props.projectId, {
          versionCode,
          title: baselineTitle,
          kind: baselineKind,
          sourceSystem: sourceSystem.trim() || null,
          sourceReference: sourceReference.trim() || null,
          entries,
        });
      }
      clearBaselineEditor();
      await load();
      props.onChanged?.();
      setMessage("پیش‌نویس مبنا ذخیره شد؛ برای رسمی‌شدن باید ارسال و تأیید شود.");
    } catch (error) {
      setMessage(toUserMessage(error, "ذخیره مبنای برنامه ناموفق بود."));
    } finally {
      setBusy(false);
    }
  }

  function editBaseline(baseline: PlanningBaselineModel) {
    setEditingBaselineId(baseline.id);
    setVersionCode(baseline.versionCode);
    setBaselineTitle(baseline.title);
    setSourceSystem(baseline.sourceSystem ?? "");
    setSourceReference(baseline.sourceReference ?? "");
    setEntries(baseline.entries.map((item) => ({
      clientGeneratedId: item.id,
      parentEntryId: item.parentEntryId,
      code: item.code,
      title: item.title,
      kind: item.kind,
      measurementMethod: item.measurementMethod,
      measurementItemId: item.measurementItemId,
      plannedStart: item.plannedStart,
      plannedFinish: item.plannedFinish,
      weightPercent: item.weightPercent,
      externalId: item.externalId,
      sortOrder: item.sortOrder,
    })));
    setMessage("نسخه انتخاب‌شده برای اصلاح در فرم بارگذاری شد.");
  }

  async function baselineAction(baseline: PlanningBaselineModel, action: BaselineAction) {
    if (action === "return" && !reviewComment.trim()) {
      setMessage("برای بازگرداندن مبنا، دلیل اصلاح را بنویسید.");
      return;
    }
    setBusy(true);
    try {
      if (action === "submit") await submitPlanningBaseline(props.apiBaseUrl, identity, props.projectId, baseline);
      if (action === "approve") await approvePlanningBaseline(props.apiBaseUrl, identity, props.projectId, baseline, reviewComment);
      if (action === "return") await returnPlanningBaseline(props.apiBaseUrl, identity, props.projectId, baseline, reviewComment);
      setReviewComment("");
      await load();
      props.onChanged?.();
      setMessage(action === "approve" ? "مبنای جدید تصویب و نسخه مصوب قبلی بدون حذف تاریخچه جایگزین شد." : "گردش بررسی مبنا ثبت شد.");
    } catch (error) {
      setMessage(toUserMessage(error, "تغییر وضعیت مبنای برنامه ناموفق بود."));
    } finally {
      setBusy(false);
    }
  }

  async function saveMilestone(event: FormEvent) {
    event.preventDefault();
    if (!approvedBaseline || !milestoneEntryId || !milestoneDate || !milestoneEvidence.trim()) {
      setMessage("نقطه عطف، تاریخ وضعیت و مرجع مدرک الزامی است.");
      return;
    }
    const progressPercent = Number(milestonePercent);
    if (!Number.isFinite(progressPercent) || progressPercent < 0 || progressPercent > 100) {
      setMessage("درصد نقطه عطف باید بین صفر تا ۱۰۰ باشد.");
      return;
    }

    setBusy(true);
    try {
      const editing = updates.find((item) => item.id === editingUpdateId);
      const input = {
        statusDate: milestoneDate,
        progressPercent,
        evidenceReference: milestoneEvidence,
        note: milestoneNote,
      };
      if (editing) {
        await amendMilestoneProgressUpdate(props.apiBaseUrl, identity, props.projectId, editing, input);
      } else {
        await createMilestoneProgressUpdate(props.apiBaseUrl, identity, props.projectId, {
          baselineId: approvedBaseline.id,
          baselineEntryId: milestoneEntryId,
          ...input,
        });
      }
      clearMilestoneEditor();
      await load();
      props.onChanged?.();
      setMessage("گزارش نقطه عطف به‌صورت پیش‌نویس ثبت شد؛ هنوز در پیشرفت رسمی وارد نشده است.");
    } catch (error) {
      setMessage(toUserMessage(error, "ثبت وضعیت نقطه عطف ناموفق بود."));
    } finally {
      setBusy(false);
    }
  }

  function editMilestone(update: MilestoneProgressUpdateModel) {
    setEditingUpdateId(update.id);
    setMilestoneEntryId(update.baselineEntryId);
    setMilestoneDate(update.statusDate);
    setMilestonePercent(String(update.progressPercent));
    setMilestoneEvidence(update.evidenceReference);
    setMilestoneNote(update.note ?? "");
    setMessage("گزارش نقطه عطف برای اصلاح در فرم بارگذاری شد.");
  }

  async function milestoneAction(update: MilestoneProgressUpdateModel, action: MilestoneAction) {
    if (action === "return" && !reviewComment.trim()) {
      setMessage("برای بازگرداندن گزارش نقطه عطف، دلیل اصلاح را بنویسید.");
      return;
    }
    setBusy(true);
    try {
      if (action === "submit") await submitMilestoneProgressUpdate(props.apiBaseUrl, identity, props.projectId, update);
      if (action === "approve") await approveMilestoneProgressUpdate(props.apiBaseUrl, identity, props.projectId, update, reviewComment);
      if (action === "return") await returnMilestoneProgressUpdate(props.apiBaseUrl, identity, props.projectId, update, reviewComment);
      setReviewComment("");
      await load();
      props.onChanged?.();
      setMessage(action === "approve" ? "وضعیت نقطه عطف با تأیید انسانی وارد محاسبه رسمی شد." : "گردش بررسی نقطه عطف ثبت شد.");
    } catch (error) {
      setMessage(toUserMessage(error, "تغییر وضعیت گزارش نقطه عطف ناموفق بود."));
    } finally {
      setBusy(false);
    }
  }

  function clearEntryEditor() {
    setEntryCode("");
    setEntryTitle("");
    setEntryParentId("");
    setEntryMeasurementId("");
    setEntryStart("");
    setEntryFinish("");
    setEntryWeight("");
    setEntryExternalId("");
  }

  function clearBaselineEditor() {
    setEditingBaselineId(null);
    setVersionCode("");
    setBaselineTitle("");
    setSourceSystem("");
    setSourceReference("");
    setEntries([]);
    clearEntryEditor();
  }

  function clearMilestoneEditor() {
    setEditingUpdateId(null);
    setMilestoneEntryId("");
    setMilestoneDate("");
    setMilestonePercent("");
    setMilestoneEvidence("");
    setMilestoneNote("");
  }

  return (
    <div className="planning-basis-control">
      <div className="planning-mode-control">
        <label>
          <span>حالت برنامه‌ریزی پروژه</span>
          <select value={mode} onChange={(event) => {
            const next = event.target.value as PlanningMode;
            setMode(next);
            setEntryKind(defaultEntryKind(next));
            clearBaselineEditor();
          }}>
            <option value="None">بدون برنامه رسمی</option>
            <option value="SimpleWorkList">فهرست ساده و وزن‌دهی اقلام</option>
            <option value="Milestones">برنامه نقاط عطف</option>
            <option value="WbsBaseline">خط مبنای ساختار شکست کار (WBS)</option>
            <option value="ExternalSchedule">برنامه زمان‌بندی بیرونی</option>
          </select>
        </label>
        <button type="button" disabled={!project || busy || !props.isOnline || project.planningMode === mode} onClick={() => void saveMode()}>
          ثبت حالت
        </button>
      </div>

      <p className="calculation-note" aria-live="polite">{message}</p>

      {baselineKind && (
        <details className="planning-editor" open={baselines.length === 0 || editingBaselineId !== null}>
          <summary>{editingBaselineId ? "اصلاح پیش‌نویس مبنا" : "ساخت نسخه جدید مبنای پیشرفت"}</summary>
          <form onSubmit={saveBaseline}>
            <div className="planning-editor-grid">
              <label><span>کد نسخه</span><input value={versionCode} disabled={editingBaselineId !== null} onChange={(event) => setVersionCode(event.target.value)} /></label>
              <label><span>عنوان نسخه</span><input value={baselineTitle} onChange={(event) => setBaselineTitle(event.target.value)} /></label>
              {mode === "ExternalSchedule" && <>
                <label><span>سامانه منبع</span><input value={sourceSystem} onChange={(event) => setSourceSystem(event.target.value)} /></label>
                <label><span>مرجع نسخه منبع</span><input value={sourceReference} onChange={(event) => setSourceReference(event.target.value)} /></label>
              </>}
            </div>

            <fieldset className="planning-line-editor">
              <legend>افزودن ردیف مبنا</legend>
              {(mode === "WbsBaseline" || mode === "ExternalSchedule") && (
                <label><span>نوع ردیف</span><select value={entryKind} onChange={(event) => setEntryKind(event.target.value as PlanningEntryKind)}>
                  <option value="Summary">ردیف خلاصه</option>
                  <option value="Activity">فعالیت مقدارمحور</option>
                  <option value="Milestone">نقطه عطف</option>
                </select></label>
              )}
              <label><span>کد ردیف</span><input value={entryCode} onChange={(event) => setEntryCode(event.target.value)} /></label>
              <label><span>عنوان ردیف</span><input value={entryTitle} onChange={(event) => setEntryTitle(event.target.value)} /></label>
              {entryKindForMode(mode, entryKind) !== "Summary" && entryKindForMode(mode, entryKind) !== "Milestone" && (
                <label><span>قلم اندازه‌گیری</span><select value={entryMeasurementId} onChange={(event) => selectMeasurement(event.target.value)}>
                  <option value="">انتخاب کنید</option>
                  {props.measurementItems.filter((item) => item.status === "Active" && item.targetQuantity !== null).map((item) => (
                    <option key={item.id} value={item.id}>{item.title} · {item.code}</option>
                  ))}
                </select></label>
              )}
              {summaryEntries.length > 0 && <label><span>ردیف والد اختیاری</span><select value={entryParentId} onChange={(event) => setEntryParentId(event.target.value)}>
                <option value="">بدون والد</option>
                {summaryEntries.map((item) => <option key={item.clientGeneratedId} value={item.clientGeneratedId}>{item.title}</option>)}
              </select></label>}
              {scheduleMode && entryKindForMode(mode, entryKind) !== "Milestone" && <label><span>شروع برنامه</span><PersianDateInput value={entryStart} onChange={setEntryStart} required ariaLabel="شروع شمسی برنامه" /></label>}
              {scheduleMode && <label><span>{entryKindForMode(mode, entryKind) === "Milestone" ? "تاریخ نقطه عطف" : "پایان برنامه"}</span><PersianDateInput value={entryFinish} onChange={setEntryFinish} required ariaLabel={entryKindForMode(mode, entryKind) === "Milestone" ? "تاریخ شمسی نقطه عطف" : "پایان شمسی برنامه"} /></label>}
              {entryKindForMode(mode, entryKind) !== "Summary" && <label><span>وزن درصدی</span><input inputMode="decimal" value={entryWeight} onChange={(event) => setEntryWeight(event.target.value)} /></label>}
              {mode === "ExternalSchedule" && <label><span>شناسه در منبع بیرونی</span><input value={entryExternalId} onChange={(event) => setEntryExternalId(event.target.value)} /></label>}
              <button type="button" onClick={addEntry}>افزودن ردیف</button>
            </fieldset>

            {entries.length > 0 && <div className="planning-draft-lines">
              {entries.map((entry) => <div key={entry.clientGeneratedId}>
                <span>{entry.title} · {entry.code}</span>
                <small>{entryKindLabel(entry.kind)} · {entry.weightPercent === null ? "بدون وزن مستقیم" : `${entry.weightPercent.toLocaleString("fa-IR")}٪`}</small>
                <button type="button" onClick={() => setEntries((current) => current.filter((item) => item.clientGeneratedId !== entry.clientGeneratedId))}>حذف از پیش‌نویس</button>
              </div>)}
              <strong>جمع وزن: {entries.reduce((sum, item) => sum + (item.weightPercent ?? 0), 0).toLocaleString("fa-IR")}٪</strong>
            </div>}

            <div className="form-actions">
              <button type="submit" disabled={busy || !props.isOnline}>{busy ? "در حال ذخیره…" : "ذخیره پیش‌نویس مبنا"}</button>
              {editingBaselineId && <button type="button" onClick={clearBaselineEditor}>انصراف از اصلاح</button>}
            </div>
          </form>
        </details>
      )}

      {baselines.length > 0 && <div className="planning-workflow-list">
        <label className="planning-review-comment"><span>یادداشت بررسی یا دلیل بازگشت</span><input value={reviewComment} onChange={(event) => setReviewComment(event.target.value)} /></label>
        {baselines.map((baseline) => <article key={baseline.id}>
          <div><strong>{baseline.title}</strong><small>{baseline.versionCode} · {baselineKindLabel(baseline.kind)} · {workflowLabel(baseline.status)}</small></div>
          <span>{baseline.entries.filter((item) => item.kind !== "Summary").length.toLocaleString("fa-IR")} ردیف پیشرفت</span>
          <div className="planning-actions">
            {(baseline.status === "Draft" || baseline.status === "Returned") && baseline.kind === baselineKind && <button type="button" disabled={busy} onClick={() => editBaseline(baseline)}>اصلاح</button>}
            {(baseline.status === "Draft" || baseline.status === "Returned") && baseline.kind === baselineKind && <button type="button" disabled={busy} onClick={() => void baselineAction(baseline, "submit")}>ارسال برای بررسی</button>}
            {baseline.status === "Submitted" && baseline.kind === baselineKind && <button type="button" disabled={busy} onClick={() => void baselineAction(baseline, "approve")}>تأیید مبنا</button>}
            {baseline.status === "Submitted" && <button type="button" disabled={busy} onClick={() => void baselineAction(baseline, "return")}>بازگرداندن</button>}
          </div>
        </article>)}
      </div>}

      {approvedBaseline && milestoneEntries.length > 0 && <details className="planning-editor">
        <summary>{editingUpdateId ? "اصلاح گزارش نقطه عطف" : "ثبت وضعیت نقطه عطف"}</summary>
        <form className="milestone-update-form" onSubmit={saveMilestone}>
          <label><span>نقطه عطف</span><select value={milestoneEntryId} disabled={editingUpdateId !== null} onChange={(event) => setMilestoneEntryId(event.target.value)}>
            <option value="">انتخاب کنید</option>
            {milestoneEntries.map((item) => <option key={item.id} value={item.id}>{item.title} · {item.code}</option>)}
          </select></label>
          <label><span>تاریخ وضعیت</span><PersianDateInput value={milestoneDate} onChange={setMilestoneDate} required ariaLabel="تاریخ شمسی وضعیت نقطه عطف" /></label>
          <label><span>درصد پیشرفت</span><input inputMode="decimal" value={milestonePercent} onChange={(event) => setMilestonePercent(event.target.value)} /></label>
          <label><span>مرجع مدرک</span><input value={milestoneEvidence} onChange={(event) => setMilestoneEvidence(event.target.value)} /></label>
          <label className="wide"><span>توضیح اختیاری</span><input value={milestoneNote} onChange={(event) => setMilestoneNote(event.target.value)} /></label>
          <button type="submit" disabled={busy || !props.isOnline}>ذخیره پیش‌نویس</button>
          {editingUpdateId && <button type="button" onClick={clearMilestoneEditor}>انصراف</button>}
        </form>
      </details>}

      {updates.length > 0 && <div className="planning-workflow-list milestone-workflow-list">
        {updates.map((update) => <article key={update.id}>
          <div><strong>{milestoneTitle(update, baselines)}</strong><small>{formatDate(update.statusDate)} · {update.progressPercent.toLocaleString("fa-IR")}٪ · {workflowLabel(update.status)}</small></div>
          <span>مدرک: {update.evidenceReference}</span>
          <div className="planning-actions">
            {(update.status === "Draft" || update.status === "Returned") && <button type="button" disabled={busy} onClick={() => editMilestone(update)}>اصلاح</button>}
            {(update.status === "Draft" || update.status === "Returned") && <button type="button" disabled={busy} onClick={() => void milestoneAction(update, "submit")}>ارسال برای بررسی</button>}
            {update.status === "Submitted" && <button type="button" disabled={busy} onClick={() => void milestoneAction(update, "approve")}>تأیید وضعیت</button>}
            {update.status === "Submitted" && <button type="button" disabled={busy} onClick={() => void milestoneAction(update, "return")}>بازگرداندن</button>}
          </div>
        </article>)}
      </div>}
    </div>
  );
}

function kindForMode(mode: PlanningMode): PlanningBaselineKind | null {
  return ({
    None: null,
    SimpleWorkList: "MeasurementWeights",
    Milestones: "MilestonePlan",
    WbsBaseline: "WbsBaseline",
    ExternalSchedule: "ExternalSchedule",
  } as const)[mode];
}

function defaultEntryKind(mode: PlanningMode): PlanningEntryKind {
  if (mode === "SimpleWorkList") return "MeasurementItem";
  if (mode === "Milestones") return "Milestone";
  return "Summary";
}

function compatibleEntryKind(mode: PlanningMode, current: PlanningEntryKind): PlanningEntryKind {
  if (mode === "SimpleWorkList" || mode === "Milestones") return defaultEntryKind(mode);
  if ((mode === "WbsBaseline" || mode === "ExternalSchedule") && current === "MeasurementItem") return "Summary";
  return current;
}

function entryKindForMode(mode: PlanningMode, selected: PlanningEntryKind): PlanningEntryKind {
  if (mode === "SimpleWorkList") return "MeasurementItem";
  if (mode === "Milestones") return "Milestone";
  return selected;
}

function basisMessage(ledger: ProgressLedgerModel | null): string {
  if (!ledger || ledger.officialProgressBasisState === "NotConfigured") return "هنوز مبنای مصوب وزن‌دهی وجود ندارد؛ درصد کل رسمی محاسبه نمی‌شود.";
  if (ledger.officialProgressBasisState === "ModeMismatch") return "نسخه مصوب با حالت فعلی پروژه سازگار نیست و در محاسبات استفاده نمی‌شود.";
  if (ledger.officialProgressBasisState === "IncompleteActualData") return `${ledger.missingActualEntryCount.toLocaleString("fa-IR")} ردیف وزن‌دار هنوز مقدار رسمی قابل محاسبه ندارد؛ درصد کل ناشناخته می‌ماند.`;
  return `مبنای مصوب ${ledger.approvedBaselineVersion ?? "فعال"} در محاسبه رسمی استفاده می‌شود.`;
}

function baselineKindLabel(kind: PlanningBaselineKind): string {
  return ({ MeasurementWeights: "وزن‌دهی اقلام", MilestonePlan: "برنامه نقاط عطف", WbsBaseline: "خط مبنای ساختار شکست کار (WBS)", ExternalSchedule: "برنامه بیرونی" })[kind];
}

function entryKindLabel(kind: PlanningEntryKind): string {
  return ({ Summary: "خلاصه", MeasurementItem: "قلم اندازه‌گیری", Activity: "فعالیت", Milestone: "نقطه عطف" })[kind];
}

function workflowLabel(status: PlanningBaselineModel["status"]): string {
  return ({ Draft: "پیش‌نویس", Submitted: "ارسال‌شده", Returned: "بازگشتی", Approved: "مصوب", Superseded: "جایگزین‌شده" })[status];
}

function milestoneTitle(update: MilestoneProgressUpdateModel, baselines: readonly PlanningBaselineModel[]): string {
  return baselines.find((baseline) => baseline.id === update.baselineId)?.entries.find((entry) => entry.id === update.baselineEntryId)?.title ?? "نقطه عطف نسخه قبلی";
}

function formatDate(value: string): string {
  return formatPersianDate(value);
}
