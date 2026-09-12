"use client";

import { type FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import { PersianDateInput } from "@/components/persian-date-input";
import {
  amendBudgetBaseline,
  amendFinancialRecord,
  createBudgetBaseline,
  createFinancialRecord,
  getFinancialState,
  listBudgetBaselines,
  listFinancialRecords,
  transitionBudgetBaseline,
  transitionFinancialRecord,
  type BudgetBaselineModel,
  type FinancialRecordModel,
  type FinancialRecordType,
  type FinancialStateModel,
} from "@/lib/finance";
import {
  listContracts,
  listPurchaseOrders,
  type ProjectContractModel,
  type PurchaseOrderModel,
} from "@/lib/commercial";
import { formatAmountFa, toUserMessage } from "@/lib/localization";
import { formatPersianDate, todayIsoInProjectTimeZone } from "@/lib/persian-date";

interface FinanceControlProps {
  readonly apiBaseUrl: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly isOnline: boolean;
  readonly refreshToken: number;
  readonly onChanged?: () => void;
}

export function FinanceControl(props: FinanceControlProps) {
  const [records, setRecords] = useState<readonly FinancialRecordModel[]>([]);
  const [baselines, setBaselines] = useState<readonly BudgetBaselineModel[]>([]);
  const [state, setState] = useState<FinancialStateModel | null>(null);
  const [contracts, setContracts] = useState<readonly ProjectContractModel[]>([]);
  const [commitments, setCommitments] = useState<readonly PurchaseOrderModel[]>([]);
  const [type, setType] = useState<FinancialRecordType>("Payment");
  const [transactionDate, setTransactionDate] = useState(todayIsoInProjectTimeZone);
  const [amount, setAmount] = useState("");
  const [description, setDescription] = useState("");
  const [counterparty, setCounterparty] = useState("");
  const [documentNumber, setDocumentNumber] = useState("");
  const [contractReference, setContractReference] = useState("");
  const [contractId, setContractId] = useState("");
  const [commitmentId, setCommitmentId] = useState("");
  const [costCenterCode, setCostCenterCode] = useState("");
  const [baselineTitle, setBaselineTitle] = useState("");
  const [baselineAmount, setBaselineAmount] = useState("");
  const [baselineNotes, setBaselineNotes] = useState("");
  const [message, setMessage] = useState("در حال دریافت وضعیت مالی…");
  const [busyId, setBusyId] = useState<string | null>(null);

  const identity = useMemo(
    () => ({ tenantId: props.tenantId, userId: props.userId }),
    [props.tenantId, props.userId],
  );

  const load = useCallback(async () => {
    if (!props.isOnline) {
      setMessage("کنترل مالی پایه در این نسخه برای واحد مالی و هنگام اتصال به سرور در دسترس است.");
      return;
    }

    try {
      const [currentRecords, currentBaselines, currentState, currentContracts, currentCommitments] = await Promise.all([
        listFinancialRecords(props.apiBaseUrl, identity, props.projectId),
        listBudgetBaselines(props.apiBaseUrl, identity, props.projectId),
        getFinancialState(props.apiBaseUrl, identity, props.projectId),
        listContracts(props.apiBaseUrl, identity, props.projectId),
        listPurchaseOrders(props.apiBaseUrl, identity, props.projectId),
      ]);
      setRecords(currentRecords);
      setBaselines(currentBaselines);
      setState(currentState);
      setContracts(currentContracts);
      setCommitments(currentCommitments);
      setMessage(financialStateMessage(currentState));
    } catch (error) {
      setMessage(toUserMessage(error, "داده مالی از سرور دریافت نشد یا دسترسی این کاربر محدود است."));
    }
  }, [identity, props.apiBaseUrl, props.isOnline, props.projectId]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timeoutId);
  }, [load, props.refreshToken]);

  async function createRecord(event: FormEvent) {
    event.preventDefault();
    const parsedAmount = Number(amount);
    if (!Number.isFinite(parsedAmount) || parsedAmount <= 0 || !description.trim()) {
      setMessage("مبلغ مثبت و شرح واقعی سند الزامی است.");
      return;
    }

    setBusyId("new-record");
    setMessage("در حال ثبت پیش‌نویس مالی…");
    try {
      await createFinancialRecord(props.apiBaseUrl, identity, props.projectId, {
        type,
        transactionDate,
        amount: parsedAmount,
        description,
        counterparty,
        documentNumber,
        contractReference,
        contractId,
        commitmentId,
        costCenterCode,
      });
      setAmount("");
      setDescription("");
      setCounterparty("");
      setDocumentNumber("");
      setContractReference("");
      setContractId("");
      setCommitmentId("");
      setCostCenterCode("");
      await load();
      props.onChanged?.();
    } catch (error) {
      setMessage(toUserMessage(error, "ثبت سند مالی ناموفق بود."));
    } finally {
      setBusyId(null);
    }
  }

  async function moveRecord(record: FinancialRecordModel, action: "submit" | "post" | "return") {
    const comment = action === "return" ? window.prompt("دلیل عودت سند برای اصلاح") ?? "" : "";
    if (action === "return" && !comment.trim()) {
      setMessage("عودت سند بدون دلیل انجام نشد.");
      return;
    }

    setBusyId(record.id);
    try {
      await transitionFinancialRecord(props.apiBaseUrl, identity, props.projectId, record, action, comment);
      await load();
      props.onChanged?.();
    } catch (error) {
      setMessage(toUserMessage(error, "تغییر وضعیت سند ناموفق بود."));
    } finally {
      setBusyId(null);
    }
  }

  async function editRecord(record: FinancialRecordModel) {
    const revisedDescription = window.prompt("شرح اصلاح‌شده سند", record.description);
    if (revisedDescription === null || !revisedDescription.trim()) return;
    const revisedAmountText = window.prompt("مبلغ اصلاح‌شده", String(record.amount));
    const revisedAmount = Number(revisedAmountText);
    if (revisedAmountText === null || !Number.isFinite(revisedAmount) || revisedAmount <= 0) {
      setMessage("مبلغ اصلاح‌شده معتبر نیست.");
      return;
    }

    setBusyId(record.id);
    try {
      await amendFinancialRecord(
        props.apiBaseUrl,
        identity,
        props.projectId,
        record,
        revisedAmount,
        revisedDescription,
      );
      await load();
    } catch (error) {
      setMessage(toUserMessage(error, "اصلاح سند ناموفق بود."));
    } finally {
      setBusyId(null);
    }
  }

  async function createBaseline(event: FormEvent) {
    event.preventDefault();
    const parsedAmount = Number(baselineAmount);
    if (!baselineTitle.trim() || !Number.isFinite(parsedAmount) || parsedAmount <= 0) {
      setMessage("عنوان و مبلغ مثبت برای پیش‌نویس بودجه الزامی است.");
      return;
    }

    setBusyId("new-baseline");
    try {
      await createBudgetBaseline(
        props.apiBaseUrl,
        identity,
        props.projectId,
        baselineTitle,
        parsedAmount,
        baselineNotes,
      );
      setBaselineTitle("");
      setBaselineAmount("");
      setBaselineNotes("");
      await load();
    } catch (error) {
      setMessage(toUserMessage(error, "ثبت پیش‌نویس بودجه ناموفق بود."));
    } finally {
      setBusyId(null);
    }
  }

  async function moveBaseline(baseline: BudgetBaselineModel, action: "submit" | "approve" | "return") {
    const comment = action === "return" ? window.prompt("دلیل عودت بودجه برای اصلاح") ?? "" : "";
    if (action === "return" && !comment.trim()) {
      setMessage("عودت بودجه بدون دلیل انجام نشد.");
      return;
    }

    setBusyId(baseline.id);
    try {
      await transitionBudgetBaseline(props.apiBaseUrl, identity, props.projectId, baseline, action, comment);
      await load();
      props.onChanged?.();
    } catch (error) {
      setMessage(toUserMessage(error, "تغییر وضعیت بودجه ناموفق بود."));
    } finally {
      setBusyId(null);
    }
  }

  async function editBaseline(baseline: BudgetBaselineModel) {
    const revisedTitle = window.prompt("عنوان اصلاح‌شده بودجه", baseline.title);
    if (revisedTitle === null || !revisedTitle.trim()) return;
    const revisedAmountText = window.prompt("مبلغ اصلاح‌شده بودجه", String(baseline.amount));
    const revisedAmount = Number(revisedAmountText);
    if (revisedAmountText === null || !Number.isFinite(revisedAmount) || revisedAmount <= 0) {
      setMessage("مبلغ اصلاح‌شده بودجه معتبر نیست.");
      return;
    }

    setBusyId(baseline.id);
    try {
      await amendBudgetBaseline(
        props.apiBaseUrl,
        identity,
        props.projectId,
        baseline,
        revisedAmount,
        revisedTitle,
      );
      await load();
    } catch (error) {
      setMessage(toUserMessage(error, "اصلاح بودجه ناموفق بود."));
    } finally {
      setBusyId(null);
    }
  }

  return (
    <article className="operational-card finance-control" id="finance">
      <div className="card-heading">
        <div>
          <p className="eyebrow">کنترل مالی پایه</p>
          <h2>واقعیت مالی و کنترل تنخواه</h2>
        </div>
        <span className={`finance-state state-${(state?.status ?? "NoData").toLowerCase()}`}>
          {financialStateLabel(state?.status)}
        </span>
      </div>

      <div className="finance-metrics">
        <Metric label="دریافت قطعی" value={money(state?.totalReceipts, state?.currencyCode)} />
        <Metric label="پرداخت مستقیم" value={money(state?.directPayments, state?.currencyCode)} />
        <Metric label="هزینه شناسایی‌شده" value={money(state?.recognizedSpend, state?.currencyCode)} />
        <Metric label="مانده تنخواه" value={money(state?.pettyCashBalance, state?.currencyCode)} warning={state?.dataQualityStatus === "NeedsAttention"} />
      </div>
      <p className="microcopy" aria-live="polite">{message}</p>

      <div className="finance-columns">
        <section>
          <h3>ثبت سند مالی</h3>
          <form className="capture-form" onSubmit={(event) => void createRecord(event)}>
            <label className="field">
              نوع واقعیت
              <select value={type} onChange={(event) => setType(event.target.value as FinancialRecordType)}>
                <option value="Receipt">دریافت</option>
                <option value="Payment">پرداخت مستقیم</option>
                <option value="PettyCashFunding">تأمین تنخواه</option>
                <option value="PettyCashExpense">هزینه تنخواه</option>
              </select>
            </label>
            <label className="field">
              تاریخ
              <PersianDateInput value={transactionDate} onChange={setTransactionDate} required ariaLabel="تاریخ شمسی سند مالی" />
            </label>
            <label className="field">
              مبلغ
              <input inputMode="decimal" value={amount} onChange={(event) => setAmount(event.target.value)} placeholder="مثلاً 125000000" />
            </label>
            <label className="field">
              طرف حساب
              <input value={counterparty} onChange={(event) => setCounterparty(event.target.value)} />
            </label>
            <label className="field full-width">
              شرح واقعیت مالی
              <textarea rows={2} value={description} onChange={(event) => setDescription(event.target.value)} />
            </label>
            <label className="field">
              شماره سند
              <input value={documentNumber} onChange={(event) => setDocumentNumber(event.target.value)} />
            </label>
            <label className="field">
              مرکز هزینه (اختیاری)
              <input value={costCenterCode} onChange={(event) => setCostCenterCode(event.target.value)} />
            </label>
            <label className="field full-width">
              مرجع متنی قدیمی (اختیاری)
              <input value={contractReference} onChange={(event) => setContractReference(event.target.value)} />
            </label>
            <label className="field">
              قرارداد واقعی (اختیاری)
              <select value={contractId} onChange={(event) => setContractId(event.target.value)}>
                <option value="">بدون اتصال</option>
                {contracts.map((contract) => (
                  <option key={contract.id} value={contract.id}>{contract.number} · {contract.title}</option>
                ))}
              </select>
            </label>
            <label className="field">
              تعهد خرید واقعی (اختیاری)
              <select value={commitmentId} onChange={(event) => {
                const selectedId = event.target.value;
                setCommitmentId(selectedId);
                const selected = commitments.find((item) => item.id === selectedId);
                if (selected?.contractId) setContractId(selected.contractId);
              }}>
                <option value="">بدون اتصال</option>
                {commitments.map((item) => (
                  <option key={item.id} value={item.id}>{item.number} · {item.title}</option>
                ))}
              </select>
            </label>
            <button type="submit" disabled={!props.isOnline || busyId === "new-record"}>ثبت پیش‌نویس</button>
          </form>

          <div className="finance-list">
            {records.slice(0, 8).map((record) => (
              <div className="finance-item" key={record.id}>
                <div>
                  <strong>{recordTypeLabel(record.type)} · {money(record.amount, record.currencyCode)}</strong>
                  <span>{formatFinancialDate(record.transactionDate)} · {record.description}</span>
                  <small>{recordStatusLabel(record.status)} · نسخه {record.revision.toLocaleString("fa-IR")}</small>
                </div>
                <div className="review-actions">
                  {(record.status === "Draft" || record.status === "Returned") && (
                    <>
                      <button className="secondary-button" type="button" disabled={busyId === record.id} onClick={() => void editRecord(record)}>اصلاح</button>
                      <button type="button" disabled={busyId === record.id} onClick={() => void moveRecord(record, "submit")}>ارسال</button>
                    </>
                  )}
                  {record.status === "Submitted" && (
                    <>
                      <button type="button" disabled={busyId === record.id} onClick={() => void moveRecord(record, "post")}>ثبت قطعی</button>
                      <button className="secondary-button" type="button" disabled={busyId === record.id} onClick={() => void moveRecord(record, "return")}>عودت</button>
                    </>
                  )}
                </div>
              </div>
            ))}
          </div>
        </section>

        <section className="budget-panel">
          <h3>بودجه مبنا — اختیاری</h3>
          <p className="microcopy">ثبت مالی بدون بودجه ادامه دارد؛ فقط مقایسه مصرف بودجه تا زمان تأیید بودجه مبنا ساخته نمی‌شود.</p>
          <form className="capture-form" onSubmit={(event) => void createBaseline(event)}>
            <label className="field full-width">
              عنوان نسخه بودجه
              <input value={baselineTitle} onChange={(event) => setBaselineTitle(event.target.value)} />
            </label>
            <label className="field full-width">
              مبلغ کل
              <input inputMode="decimal" value={baselineAmount} onChange={(event) => setBaselineAmount(event.target.value)} />
            </label>
            <label className="field full-width">
              توضیح
              <textarea rows={2} value={baselineNotes} onChange={(event) => setBaselineNotes(event.target.value)} />
            </label>
            <button type="submit" disabled={!props.isOnline || busyId === "new-baseline"}>ثبت پیش‌نویس بودجه</button>
          </form>
          <div className="finance-list">
            {baselines.slice(0, 5).map((baseline) => (
              <div className="finance-item" key={baseline.id}>
                <div>
                  <strong>{baseline.title}</strong>
                  <span>{money(baseline.amount, baseline.currencyCode)}</span>
                  <small>{baselineStatusLabel(baseline.status)} · نسخه {baseline.revision.toLocaleString("fa-IR")}</small>
                </div>
                <div className="review-actions">
                  {(baseline.status === "Draft" || baseline.status === "Returned") && (
                    <>
                      <button className="secondary-button" type="button" disabled={busyId === baseline.id} onClick={() => void editBaseline(baseline)}>اصلاح</button>
                      <button type="button" disabled={busyId === baseline.id} onClick={() => void moveBaseline(baseline, "submit")}>ارسال</button>
                    </>
                  )}
                  {baseline.status === "Submitted" && (
                    <>
                      <button type="button" disabled={busyId === baseline.id} onClick={() => void moveBaseline(baseline, "approve")}>تأیید بودجه</button>
                      <button className="secondary-button" type="button" disabled={busyId === baseline.id} onClick={() => void moveBaseline(baseline, "return")}>عودت</button>
                    </>
                  )}
                </div>
              </div>
            ))}
          </div>
        </section>
      </div>
    </article>
  );
}

function Metric(props: { readonly label: string; readonly value: string; readonly warning?: boolean }) {
  return (
    <div className={props.warning ? "metric-warning" : undefined}>
      <strong>{props.value}</strong>
      <span>{props.label}</span>
    </div>
  );
}

function formatFinancialDate(value: string): string {
  return formatPersianDate(value);
}

function money(value?: number, currency = "IRR"): string {
  return formatAmountFa(value, currency);
}

function financialStateLabel(status?: FinancialStateModel["status"]): string {
  return ({ NotConfigured: "پیکربندی‌نشده", NoData: "بدون داده قطعی", Available: "داده قطعی موجود" })[status ?? "NoData"];
}

function financialStateMessage(state: FinancialStateModel): string {
  if (state.status === "NotConfigured") return "کنترل مالی پایه برای این پروژه فعال نیست.";
  if (state.status === "NoData") return "هنوز سند قطعی وجود ندارد؛ پیش‌نویس و سند ارسال‌شده وارد شاخص‌ها نشده‌اند.";
  if (state.dataQualityStatus === "NeedsAttention") return "مانده تنخواه منفی است؛ ثبت تأمین تنخواه یا اصلاح اسناد باید بررسی شود.";
  if (state.budgetComparisonState !== "Available") return "وضعیت مالی از اسناد قطعی ساخته شده؛ مقایسه بودجه به‌دلیل نبود بودجه مبنا نمایش داده نمی‌شود.";
  return `وضعیت مالی رسمی با ${state.postedRecordCount.toLocaleString("fa-IR")} سند قطعی ساخته شده است.`;
}

function recordTypeLabel(type: FinancialRecordType): string {
  return ({ Receipt: "دریافت", Payment: "پرداخت", PettyCashFunding: "تأمین تنخواه", PettyCashExpense: "هزینه تنخواه" })[type];
}

function recordStatusLabel(status: FinancialRecordModel["status"]): string {
  return ({ Draft: "پیش‌نویس", Submitted: "در انتظار بررسی", Returned: "عودت‌شده", Posted: "قطعی" })[status];
}

function baselineStatusLabel(status: BudgetBaselineModel["status"]): string {
  return ({ Draft: "پیش‌نویس", Submitted: "در انتظار بررسی", Returned: "عودت‌شده", Approved: "مصوب", Superseded: "جایگزین‌شده" })[status];
}
