"use client";

import { type FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import { PersianDateInput } from "@/components/persian-date-input";
import {
  createFinancialObligation,
  createManagementFeePolicy,
  createPettyCashRequest,
  getFinanceControlState,
  listFinancialObligations,
  listManagementFeePolicies,
  listPettyCashRequests,
  recordPettyCashAdvance,
  settleFinancialObligation,
  submitPettyCashReconciliation,
  transitionFinancialObligation,
  transitionManagementFeePolicy,
  transitionPettyCashRequest,
  type FinanceControlStateModel,
  type FinancialObligationModel,
  type FinancialObligationType,
  type ManagementFeePolicyModel,
  type PettyCashRequestModel,
} from "@/lib/finance-control";
import type { FinancialRecordModel } from "@/lib/finance";
import { formatAmountFa, toUserMessage } from "@/lib/localization";
import { formatPersianDate, todayIsoInProjectTimeZone } from "@/lib/persian-date";
import type { ProjectLocationModel } from "@/lib/projects";

interface FinanceCompletionPanelProps {
  readonly apiBaseUrl: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly isOnline: boolean;
  readonly refreshToken: number;
  readonly records: readonly FinancialRecordModel[];
  readonly locations: readonly ProjectLocationModel[];
  readonly onChanged?: () => void;
}

export function FinanceCompletionPanel(props: FinanceCompletionPanelProps) {
  const identity = useMemo(
    () => ({ tenantId: props.tenantId, userId: props.userId }),
    [props.tenantId, props.userId],
  );
  const [control, setControl] = useState<FinanceControlStateModel | null>(null);
  const [obligations, setObligations] = useState<readonly FinancialObligationModel[]>([]);
  const [pettyCash, setPettyCash] = useState<readonly PettyCashRequestModel[]>([]);
  const [policies, setPolicies] = useState<readonly ManagementFeePolicyModel[]>([]);
  const [message, setMessage] = useState("در حال دریافت کنترل‌های تکمیلی مالی…");
  const [busy, setBusy] = useState<string | null>(null);

  const [obligationType, setObligationType] = useState<FinancialObligationType>("Payable");
  const [obligationNumber, setObligationNumber] = useState("");
  const [obligationDescription, setObligationDescription] = useState("");
  const [obligationIssueDate, setObligationIssueDate] = useState(todayIsoInProjectTimeZone);
  const [obligationDueDate, setObligationDueDate] = useState(todayIsoInProjectTimeZone);
  const [obligationAmount, setObligationAmount] = useState("");
  const [obligationLocationId, setObligationLocationId] = useState("");

  const [pettyNumber, setPettyNumber] = useState("");
  const [pettyPurpose, setPettyPurpose] = useState("");
  const [pettyCustodian, setPettyCustodian] = useState("");
  const [pettyRequestDate, setPettyRequestDate] = useState(todayIsoInProjectTimeZone);
  const [pettyDueDate, setPettyDueDate] = useState(todayIsoInProjectTimeZone);
  const [pettyAmount, setPettyAmount] = useState("");
  const [pettyLocationId, setPettyLocationId] = useState("");

  const [feeTitle, setFeeTitle] = useState("");
  const [feeRate, setFeeRate] = useState("");
  const [feeEffectiveFrom, setFeeEffectiveFrom] = useState(todayIsoInProjectTimeZone);

  const load = useCallback(async () => {
    if (!props.isOnline) {
      setMessage("کنترل تعهدات، تنخواه و کارمزد هنگام اتصال به سرور در دسترس است.");
      return;
    }

    try {
      const [nextControl, nextObligations, nextPettyCash, nextPolicies] = await Promise.all([
        getFinanceControlState(props.apiBaseUrl, identity, props.projectId),
        listFinancialObligations(props.apiBaseUrl, identity, props.projectId),
        listPettyCashRequests(props.apiBaseUrl, identity, props.projectId),
        listManagementFeePolicies(props.apiBaseUrl, identity, props.projectId),
      ]);
      setControl(nextControl);
      setObligations(nextObligations);
      setPettyCash(nextPettyCash);
      setPolicies(nextPolicies);
      setMessage("محاسبات تعهد، تنخواه و کارمزد از سرویس قطعی مالی به‌روز شد.");
    } catch (error) {
      setMessage(toUserMessage(error, "کنترل‌های تکمیلی مالی دریافت نشد یا دسترسی این نقش محدود است."));
    }
  }, [identity, props.apiBaseUrl, props.isOnline, props.projectId]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timeoutId);
  }, [load, props.refreshToken]);

  async function createObligation(event: FormEvent) {
    event.preventDefault();
    const amount = Number(obligationAmount);
    if (!obligationNumber.trim() || !obligationDescription.trim() || !Number.isFinite(amount) || amount <= 0) {
      setMessage("شماره، شرح و مبلغ مثبت برای تعهد مالی الزامی است.");
      return;
    }

    await run("new-obligation", "ثبت تعهد مالی ناموفق بود.", async () => {
      await createFinancialObligation(props.apiBaseUrl, identity, props.projectId, {
        type: obligationType,
        number: obligationNumber,
        description: obligationDescription,
        issueDate: obligationIssueDate,
        dueDate: obligationDueDate,
        amount,
        locationId: obligationLocationId,
      });
      setObligationNumber("");
      setObligationDescription("");
      setObligationAmount("");
    });
  }

  async function moveObligation(item: FinancialObligationModel, action: "submit" | "approve" | "return") {
    const comment = action === "return" ? window.prompt("دلیل عودت تعهد مالی") ?? "" : "";
    if (action === "return" && !comment.trim()) return;
    await run(item.id, "تغییر وضعیت تعهد مالی ناموفق بود.", async () => {
      await transitionFinancialObligation(props.apiBaseUrl, identity, props.projectId, item, action, comment);
    });
  }

  async function settle(item: FinancialObligationModel) {
    const expectedType = item.type === "Payable" ? "Payment" : "Receipt";
    const eligible = props.records.filter((record) => record.status === "Posted" && record.type === expectedType);
    const defaultRecord = eligible.find((record) => record.amount >= item.outstandingAmount);
    const recordId = window.prompt("شناسه سند قطعی تسویه", defaultRecord?.id ?? "") ?? "";
    if (!recordId.trim()) return;
    await run(item.id, "تسویه تعهد مالی ناموفق بود.", async () => {
      await settleFinancialObligation(
        props.apiBaseUrl,
        identity,
        props.projectId,
        item,
        recordId,
        item.outstandingAmount,
      );
    });
  }

  async function createPetty(event: FormEvent) {
    event.preventDefault();
    const amount = Number(pettyAmount);
    if (!pettyNumber.trim() || !pettyPurpose.trim() || !pettyCustodian.trim() || !Number.isFinite(amount) || amount <= 0) {
      setMessage("شماره، هدف، تنخواه‌گردان و مبلغ مثبت الزامی است.");
      return;
    }

    await run("new-petty", "ثبت درخواست تنخواه ناموفق بود.", async () => {
      await createPettyCashRequest(props.apiBaseUrl, identity, props.projectId, {
        number: pettyNumber,
        purpose: pettyPurpose,
        custodian: pettyCustodian,
        requestDate: pettyRequestDate,
        reconciliationDueDate: pettyDueDate,
        requestedAmount: amount,
        locationId: pettyLocationId,
      });
      setPettyNumber("");
      setPettyPurpose("");
      setPettyCustodian("");
      setPettyAmount("");
    });
  }

  async function movePetty(item: PettyCashRequestModel, action: "submit" | "approve" | "return" | "reconciliation/approve") {
    const comment = action === "return" ? window.prompt("دلیل عودت درخواست تنخواه") ?? "" : "";
    if (action === "return" && !comment.trim()) return;
    await run(item.id, "تغییر وضعیت درخواست تنخواه ناموفق بود.", async () => {
      await transitionPettyCashRequest(props.apiBaseUrl, identity, props.projectId, item, action, comment);
    });
  }

  async function recordAdvance(item: PettyCashRequestModel) {
    const record = props.records.find((candidate) =>
      candidate.status === "Posted" && candidate.type === "PettyCashFunding" && candidate.amount === item.approvedAmount);
    const recordId = window.prompt("شناسه سند قطعی تأمین تنخواه", record?.id ?? "") ?? "";
    if (!recordId.trim()) return;
    await run(item.id, "ثبت پرداخت علی‌الحساب ناموفق بود.", async () => {
      await recordPettyCashAdvance(props.apiBaseUrl, identity, props.projectId, item, recordId);
    });
  }

  async function reconcile(item: PettyCashRequestModel) {
    const expenseText = window.prompt("مبلغ هزینه‌کرد قطعی", String(item.approvedAmount ?? ""));
    if (expenseText === null) return;
    const expenseAmount = Number(expenseText);
    const returnedAmount = (item.approvedAmount ?? 0) - expenseAmount;
    const expenseRecord = props.records.find((candidate) =>
      candidate.status === "Posted" && candidate.type === "PettyCashExpense" && candidate.amount === expenseAmount);
    const expenseRecordId = expenseAmount > 0
      ? window.prompt("شناسه سند قطعی هزینه تنخواه", expenseRecord?.id ?? "") ?? ""
      : "";
    const returnRecord = props.records.find((candidate) =>
      candidate.status === "Posted" && candidate.type === "Receipt" && candidate.amount === returnedAmount);
    const returnRecordId = returnedAmount > 0
      ? window.prompt("شناسه سند قطعی برگشت وجه", returnRecord?.id ?? "") ?? ""
      : undefined;
    if (!Number.isFinite(expenseAmount) || expenseAmount < 0 || returnedAmount < 0 ||
      (expenseAmount > 0 && !expenseRecordId.trim()) || (returnedAmount > 0 && !returnRecordId?.trim())) {
      setMessage("مبالغ تسویه یا شناسه اسناد قطعی معتبر نیست.");
      return;
    }

    await run(item.id, "ارسال تسویه تنخواه ناموفق بود.", async () => {
      await submitPettyCashReconciliation(
        props.apiBaseUrl,
        identity,
        props.projectId,
        item,
        expenseAmount,
        returnedAmount,
        expenseRecordId,
        returnRecordId,
      );
    });
  }

  async function createFee(event: FormEvent) {
    event.preventDefault();
    const rate = Number(feeRate);
    if (!feeTitle.trim() || !Number.isFinite(rate) || rate <= 0 || rate > 100) {
      setMessage("عنوان و نرخ معتبر کارمزد مدیریت الزامی است.");
      return;
    }

    await run("new-fee", "ثبت قاعده کارمزد مدیریت ناموفق بود.", async () => {
      await createManagementFeePolicy(props.apiBaseUrl, identity, props.projectId, {
        title: feeTitle,
        ratePercent: rate,
        effectiveFrom: feeEffectiveFrom,
      });
      setFeeTitle("");
      setFeeRate("");
    });
  }

  async function moveFee(item: ManagementFeePolicyModel, action: "submit" | "approve" | "return") {
    const comment = action === "return" ? window.prompt("دلیل عودت قاعده کارمزد") ?? "" : "";
    if (action === "return" && !comment.trim()) return;
    await run(item.id, "تغییر وضعیت قاعده کارمزد ناموفق بود.", async () => {
      await transitionManagementFeePolicy(props.apiBaseUrl, identity, props.projectId, item, action, comment);
    });
  }

  async function run(id: string, fallback: string, action: () => Promise<void>) {
    setBusy(id);
    try {
      await action();
      await load();
      props.onChanged?.();
    } catch (error) {
      setMessage(toUserMessage(error, fallback));
    } finally {
      setBusy(null);
    }
  }

  return (
    <section className="finance-completion" data-testid="finance-control-v2">
      <div className="section-title">
        <div>
          <p className="eyebrow">کنترل مالی تکمیلی</p>
          <h3>تعهدات، گردش تنخواه و کارمزد مدیریت</h3>
        </div>
        <span className="section-note">محاسبه قطعی · اتصال پایدار محل پروژه</span>
      </div>
      <div className="finance-metrics" data-testid="finance-control-metrics">
        <Metric label="بدهی باز" value={money(control?.control.aging.openPayableAmount, control?.financialState.currencyCode)} />
        <Metric label="مطالبات باز" value={money(control?.control.aging.openReceivableAmount, control?.financialState.currencyCode)} />
        <Metric label="علی‌الحساب تسویه‌نشده" value={money(control?.control.pettyCash.outstandingAdvanceAmount, control?.financialState.currencyCode)} warning={(control?.control.pettyCash.overdueReconciliationCount ?? 0) > 0} />
        <Metric label="کارمزد محاسبه‌شده" value={control?.control.managementFeeAmount === null ? "پیکربندی‌نشده" : money(control?.control.managementFeeAmount, control?.financialState.currencyCode)} />
      </div>
      <p className="microcopy" aria-live="polite">{message}</p>

      <div className="finance-columns">
        <section>
          <h4>بدهکار و بستانکار</h4>
          <form className="capture-form" onSubmit={(event) => void createObligation(event)} data-testid="finance-obligation-form">
            <label className="field">نوع<select value={obligationType} onChange={(event) => setObligationType(event.target.value as FinancialObligationType)}><option value="Payable">بدهی پرداختنی</option><option value="Receivable">مطالبه دریافتنی</option></select></label>
            <label className="field">شماره<input value={obligationNumber} onChange={(event) => setObligationNumber(event.target.value)} /></label>
            <label className="field">تاریخ صدور<PersianDateInput value={obligationIssueDate} onChange={setObligationIssueDate} required ariaLabel="تاریخ شمسی صدور تعهد مالی" /></label>
            <label className="field">تاریخ سررسید<PersianDateInput value={obligationDueDate} onChange={setObligationDueDate} required ariaLabel="تاریخ شمسی سررسید تعهد مالی" /></label>
            <label className="field">مبلغ<input inputMode="decimal" value={obligationAmount} onChange={(event) => setObligationAmount(event.target.value)} /></label>
            <LocationSelect value={obligationLocationId} onChange={setObligationLocationId} locations={props.locations} />
            <label className="field full-width">شرح<textarea rows={2} value={obligationDescription} onChange={(event) => setObligationDescription(event.target.value)} /></label>
            <button disabled={!props.isOnline || busy === "new-obligation"}>ثبت پیش‌نویس تعهد</button>
          </form>
          <div className="finance-list" data-testid="finance-obligation-list">
            {obligations.slice(0, 8).map((item) => (
              <div className="finance-item" key={item.id} data-entity-id={item.id}>
                <div><strong>{item.number} · {money(item.outstandingAmount, item.currencyCode)}</strong><span>سررسید {formatPersianDate(item.dueDate)} · {item.description}</span><small>{obligationStatus(item.status)}{item.locationCode ? ` · محل ${item.locationCode}` : ""}</small></div>
                <div className="review-actions">
                  {(item.status === "Draft" || item.status === "Returned") && <button disabled={busy === item.id} onClick={() => void moveObligation(item, "submit")}>ارسال</button>}
                  {item.status === "Submitted" && <><button disabled={busy === item.id} onClick={() => void moveObligation(item, "approve")}>تأیید</button><button className="secondary-button" disabled={busy === item.id} onClick={() => void moveObligation(item, "return")}>عودت</button></>}
                  {(item.status === "Approved" || item.status === "PartiallySettled") && <button disabled={busy === item.id} onClick={() => void settle(item)}>ثبت تسویه</button>}
                </div>
              </div>
            ))}
          </div>
        </section>

        <section>
          <h4>درخواست و تسویه تنخواه</h4>
          <form className="capture-form" onSubmit={(event) => void createPetty(event)} data-testid="petty-cash-request-form">
            <label className="field">شماره<input value={pettyNumber} onChange={(event) => setPettyNumber(event.target.value)} /></label>
            <label className="field">تنخواه‌گردان<input value={pettyCustodian} onChange={(event) => setPettyCustodian(event.target.value)} /></label>
            <label className="field">تاریخ درخواست<PersianDateInput value={pettyRequestDate} onChange={setPettyRequestDate} required ariaLabel="تاریخ شمسی درخواست تنخواه" /></label>
            <label className="field">مهلت تسویه<PersianDateInput value={pettyDueDate} onChange={setPettyDueDate} required ariaLabel="تاریخ شمسی مهلت تسویه تنخواه" /></label>
            <label className="field">مبلغ<input inputMode="decimal" value={pettyAmount} onChange={(event) => setPettyAmount(event.target.value)} /></label>
            <LocationSelect value={pettyLocationId} onChange={setPettyLocationId} locations={props.locations} />
            <label className="field full-width">هدف<textarea rows={2} value={pettyPurpose} onChange={(event) => setPettyPurpose(event.target.value)} /></label>
            <button disabled={!props.isOnline || busy === "new-petty"}>ثبت درخواست تنخواه</button>
          </form>
          <div className="finance-list" data-testid="petty-cash-request-list">
            {pettyCash.slice(0, 8).map((item) => (
              <div className="finance-item" key={item.id} data-entity-id={item.id}>
                <div><strong>{item.number} · {money(item.approvedAmount ?? item.requestedAmount, item.currencyCode)}</strong><span>مهلت تسویه {formatPersianDate(item.reconciliationDueDate)} · {item.custodian}</span><small>{pettyStatus(item.status)}</small></div>
                <div className="review-actions">
                  {(item.status === "Draft" || item.status === "Returned") && <button disabled={busy === item.id} onClick={() => void movePetty(item, "submit")}>ارسال</button>}
                  {item.status === "Submitted" && <><button disabled={busy === item.id} onClick={() => void movePetty(item, "approve")}>تأیید</button><button className="secondary-button" disabled={busy === item.id} onClick={() => void movePetty(item, "return")}>عودت</button></>}
                  {item.status === "Approved" && <button disabled={busy === item.id} onClick={() => void recordAdvance(item)}>ثبت علی‌الحساب</button>}
                  {item.status === "Advanced" && <button disabled={busy === item.id} onClick={() => void reconcile(item)}>ارسال تسویه</button>}
                  {item.status === "ReconciliationSubmitted" && <button disabled={busy === item.id} onClick={() => void movePetty(item, "reconciliation/approve")}>تأیید تسویه</button>}
                </div>
              </div>
            ))}
          </div>
        </section>
      </div>

      <section className="budget-panel">
        <h4>قاعده کارمزد مدیریت — اختیاری</h4>
        <form className="capture-form" onSubmit={(event) => void createFee(event)} data-testid="management-fee-form">
          <label className="field">عنوان<input value={feeTitle} onChange={(event) => setFeeTitle(event.target.value)} /></label>
          <label className="field">نرخ درصد<input inputMode="decimal" value={feeRate} onChange={(event) => setFeeRate(event.target.value)} /></label>
          <label className="field">تاریخ اثر<PersianDateInput value={feeEffectiveFrom} onChange={setFeeEffectiveFrom} required ariaLabel="تاریخ شمسی اثر قاعده کارمزد" /></label>
          <button disabled={!props.isOnline || busy === "new-fee"}>ثبت پیش‌نویس قاعده</button>
        </form>
        <div className="finance-list" data-testid="management-fee-list">
          {policies.slice(0, 5).map((item) => (
            <div className="finance-item" key={item.id} data-entity-id={item.id}>
              <div><strong>{item.title} · {item.ratePercent.toLocaleString("fa-IR")}%</strong><span>از {formatPersianDate(item.effectiveFrom)}</span><small>{feeStatus(item.status)}</small></div>
              <div className="review-actions">
                {(item.status === "Draft" || item.status === "Returned") && <button disabled={busy === item.id} onClick={() => void moveFee(item, "submit")}>ارسال</button>}
                {item.status === "Submitted" && <><button disabled={busy === item.id} onClick={() => void moveFee(item, "approve")}>تأیید</button><button className="secondary-button" disabled={busy === item.id} onClick={() => void moveFee(item, "return")}>عودت</button></>}
              </div>
            </div>
          ))}
        </div>
      </section>
    </section>
  );
}

function LocationSelect(props: { readonly value: string; readonly onChange: (value: string) => void; readonly locations: readonly ProjectLocationModel[] }) {
  return <label className="field">محل پروژه (اختیاری)<select value={props.value} onChange={(event) => props.onChange(event.target.value)}><option value="">بدون اتصال</option>{props.locations.filter((item) => item.status === "Active").map((item) => <option key={item.id} value={item.id}>{item.code} · {item.name}</option>)}</select></label>;
}

function Metric(props: { readonly label: string; readonly value: string; readonly warning?: boolean }) {
  return <div className={props.warning ? "metric-warning" : undefined}><strong>{props.value}</strong><span>{props.label}</span></div>;
}

function money(value?: number, currency = "IRR") { return formatAmountFa(value, currency); }
function obligationStatus(status: FinancialObligationModel["status"]) { return ({ Draft: "پیش‌نویس", Submitted: "در انتظار بررسی", Returned: "عودت‌شده", Approved: "تأییدشده", PartiallySettled: "تسویه جزئی", Settled: "تسویه‌شده" })[status]; }
function pettyStatus(status: PettyCashRequestModel["status"]) { return ({ Draft: "پیش‌نویس", Submitted: "در انتظار بررسی", Returned: "عودت‌شده", Approved: "تأییدشده", Advanced: "علی‌الحساب پرداخت‌شده", ReconciliationSubmitted: "تسویه ارسال‌شده", Reconciled: "تسویه تأییدشده" })[status]; }
function feeStatus(status: ManagementFeePolicyModel["status"]) { return ({ Draft: "پیش‌نویس", Submitted: "در انتظار بررسی", Returned: "عودت‌شده", Approved: "مصوب", Superseded: "جایگزین‌شده" })[status]; }
