"use client";

import { type FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import {
  createAmendment,
  createContract,
  createParty,
  createPurchaseRequest,
  getCommercialState,
  issuePurchaseOrder,
  listAmendments,
  listContracts,
  listParties,
  listPurchaseOrders,
  listPurchaseRequests,
  transitionAmendment,
  transitionContract,
  transitionPurchaseOrder,
  transitionPurchaseRequest,
  type CommercialStateModel,
  type ContractAmendmentModel,
  type ContractAmendmentType,
  type PartyModel,
  type PartyType,
  type ProjectContractModel,
  type ProjectContractType,
  type PurchaseOrderModel,
  type PurchaseRequestModel,
} from "@/lib/commercial";
import { formatAmountFa, toUserMessage } from "@/lib/localization";
import { getSupplyState, type SupplyItemModel } from "@/lib/supply";

interface CommercialControlProps {
  readonly apiBaseUrl: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly isOnline: boolean;
  readonly refreshToken: number;
  readonly onChanged?: () => void;
}

export function CommercialControl(props: CommercialControlProps) {
  const identity = useMemo(() => ({ tenantId: props.tenantId, userId: props.userId }), [props.tenantId, props.userId]);
  const [parties, setParties] = useState<readonly PartyModel[]>([]);
  const [contracts, setContracts] = useState<readonly ProjectContractModel[]>([]);
  const [amendments, setAmendments] = useState<readonly ContractAmendmentModel[]>([]);
  const [requests, setRequests] = useState<readonly PurchaseRequestModel[]>([]);
  const [orders, setOrders] = useState<readonly PurchaseOrderModel[]>([]);
  const [supplyItems, setSupplyItems] = useState<readonly SupplyItemModel[]>([]);
  const [state, setState] = useState<CommercialStateModel | null>(null);
  const [message, setMessage] = useState("در حال دریافت رجیستر قرارداد و خرید…");
  const [busyId, setBusyId] = useState<string | null>(null);

  const [partyCode, setPartyCode] = useState("");
  const [partyName, setPartyName] = useState("");
  const [partyType, setPartyType] = useState<PartyType>("Supplier");

  const [contractPartyId, setContractPartyId] = useState("");
  const [contractNumber, setContractNumber] = useState("");
  const [contractTitle, setContractTitle] = useState("");
  const [contractType, setContractType] = useState<ProjectContractType>("Supply");
  const [contractAmount, setContractAmount] = useState("");

  const [amendmentContractId, setAmendmentContractId] = useState("");
  const [amendmentNumber, setAmendmentNumber] = useState("");
  const [amendmentTitle, setAmendmentTitle] = useState("");
  const [amendmentType, setAmendmentType] = useState<ContractAmendmentType>("ScopeChange");
  const [amendmentAmount, setAmendmentAmount] = useState("");
  const [extensionDays, setExtensionDays] = useState("");

  const [requestNumber, setRequestNumber] = useState("");
  const [requestTitle, setRequestTitle] = useState("");
  const [requestDescription, setRequestDescription] = useState("");
  const [requestEstimate, setRequestEstimate] = useState("");
  const [requestSupplyItemId, setRequestSupplyItemId] = useState("");
  const [requestQuantity, setRequestQuantity] = useState("");
  const [requestUnit, setRequestUnit] = useState("");
  const [requestDeliveryLocation, setRequestDeliveryLocation] = useState("");
  const [requestWorkReference, setRequestWorkReference] = useState("");
  const [requestWbsReference, setRequestWbsReference] = useState("");
  const [requestCriticality, setRequestCriticality] = useState<"Normal" | "Priority" | "Critical" | "Emergency">("Normal");

  const [orderRequestId, setOrderRequestId] = useState("");
  const [orderPartyId, setOrderPartyId] = useState("");
  const [orderContractId, setOrderContractId] = useState("");
  const [orderNumber, setOrderNumber] = useState("");
  const [orderTitle, setOrderTitle] = useState("");
  const [orderAmount, setOrderAmount] = useState("");

  const activeParties = parties.filter((party) => party.status === "Active");
  const activeContracts = contracts.filter((contract) => contract.status === "Active");
  const approvedRequests = requests.filter((request) => request.status === "Approved");

  const load = useCallback(async () => {
    if (!props.isOnline) {
      setMessage("رجیستر قرارداد و خرید برخط‌محور است؛ داده ثبت‌شده سرور تغییری نمی‌کند.");
      return;
    }

    try {
      const [loadedParties, loadedContracts, loadedAmendments, loadedRequests, loadedOrders, loadedState, loadedSupplyState] = await Promise.all([
        listParties(props.apiBaseUrl, identity, props.projectId),
        listContracts(props.apiBaseUrl, identity, props.projectId),
        listAmendments(props.apiBaseUrl, identity, props.projectId),
        listPurchaseRequests(props.apiBaseUrl, identity, props.projectId),
        listPurchaseOrders(props.apiBaseUrl, identity, props.projectId),
        getCommercialState(props.apiBaseUrl, identity, props.projectId),
        getSupplyState(props.apiBaseUrl, identity, props.projectId).catch(() => null),
      ]);
      setParties(loadedParties);
      setContracts(loadedContracts);
      setAmendments(loadedAmendments);
      setRequests(loadedRequests);
      setOrders(loadedOrders);
      setState(loadedState);
      setSupplyItems(loadedSupplyState?.items.filter((item) => item.status === "Active") ?? []);
      setMessage(commercialStateMessage(loadedState));
    } catch (error) {
      setMessage(toUserMessage(error, "دریافت رجیستر قرارداد و خرید ناموفق بود."));
    }
  }, [identity, props.apiBaseUrl, props.isOnline, props.projectId]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timeoutId);
  }, [load, props.refreshToken]);

  async function run(id: string, action: () => Promise<unknown>, success: string): Promise<boolean> {
    setBusyId(id);
    try {
      await action();
      setMessage(success);
      await load();
      props.onChanged?.();
      return true;
    } catch (error) {
      setMessage(toUserMessage(error, "عملیات قرارداد و خرید ناموفق بود."));
      return false;
    } finally {
      setBusyId(null);
    }
  }

  async function submitParty(event: FormEvent) {
    event.preventDefault();
    if (!partyCode.trim() || !partyName.trim()) {
      setMessage("کد و نام طرف قرارداد الزامی است.");
      return;
    }

    const succeeded = await run("new-party", () => createParty(props.apiBaseUrl, identity, props.projectId, {
      code: partyCode,
      legalName: partyName,
      type: partyType,
    }), "طرف قرارداد ثبت شد.");
    if (!succeeded) return;
    setPartyCode("");
    setPartyName("");
  }

  async function submitContract(event: FormEvent) {
    event.preventDefault();
    const amount = optionalPositiveNumber(contractAmount);
    if (!contractPartyId || !contractNumber.trim() || !contractTitle.trim() || amount === false) {
      setMessage("طرف، شماره و عنوان قرارداد را وارد کنید؛ مبلغ در صورت ورود باید مثبت باشد.");
      return;
    }

    const succeeded = await run("new-contract", () => createContract(props.apiBaseUrl, identity, props.projectId, {
      partyId: contractPartyId,
      number: contractNumber,
      title: contractTitle,
      type: contractType,
      originalApprovedAmount: amount ?? undefined,
    }), "پیش‌نویس قرارداد ثبت شد.");
    if (!succeeded) return;
    setContractNumber("");
    setContractTitle("");
    setContractAmount("");
  }

  async function moveContract(item: ProjectContractModel, action: "submit" | "activate" | "return" | "close") {
    const comment = action === "return" ? window.prompt("دلیل عودت قرارداد") ?? "" : "";
    if (action === "return" && !comment.trim()) return;
    await run(item.id, () => transitionContract(
      props.apiBaseUrl, identity, props.projectId, item, action, comment,
    ), "وضعیت قرارداد به‌روزرسانی شد.");
  }

  async function submitAmendment(event: FormEvent) {
    event.preventDefault();
    const amount = optionalSignedNumber(amendmentAmount);
    const days = optionalNonNegativeInteger(extensionDays);
    if (!amendmentContractId || !amendmentNumber.trim() || !amendmentTitle.trim() || amount === false || days === false) {
      setMessage("قرارداد، شماره و عنوان الحاقیه الزامی است؛ مقدارهای عددی را کنترل کنید.");
      return;
    }

    const succeeded = await run("new-amendment", () => createAmendment(props.apiBaseUrl, identity, props.projectId, {
      contractId: amendmentContractId,
      number: amendmentNumber,
      title: amendmentTitle,
      type: amendmentType,
      amountDelta: amount ?? undefined,
      extensionDays: days ?? undefined,
    }), "پیش‌نویس الحاقیه ثبت شد.");
    if (!succeeded) return;
    setAmendmentNumber("");
    setAmendmentTitle("");
    setAmendmentAmount("");
    setExtensionDays("");
  }

  async function moveAmendment(item: ContractAmendmentModel, action: "submit" | "approve" | "return") {
    const comment = action === "return" ? window.prompt("دلیل عودت الحاقیه") ?? "" : "";
    if (action === "return" && !comment.trim()) return;
    await run(item.id, () => transitionAmendment(
      props.apiBaseUrl, identity, props.projectId, item, action, comment,
    ), "وضعیت الحاقیه به‌روزرسانی شد.");
  }

  async function submitRequest(event: FormEvent) {
    event.preventDefault();
    const estimate = optionalPositiveNumber(requestEstimate);
    const quantity = optionalPositiveNumber(requestQuantity);
    const selectedSupplyItem = supplyItems.find((item) => item.id === requestSupplyItemId);
    const supplyBasisInvalid = Boolean(requestSupplyItemId) && (!selectedSupplyItem || quantity === null || quantity === false || !requestDeliveryLocation.trim());
    if (!requestNumber.trim() || !requestTitle.trim() || !requestDescription.trim() || estimate === false || supplyBasisInvalid) {
      setMessage("شماره، عنوان و شرح درخواست الزامی است؛ برآورد در صورت ورود باید مثبت باشد.");
      return;
    }

    const succeeded = await run("new-request", () => createPurchaseRequest(props.apiBaseUrl, identity, props.projectId, {
      number: requestNumber,
      title: requestTitle,
      description: requestDescription,
      estimatedAmount: estimate ?? undefined,
      supplyItemId: selectedSupplyItem?.id,
      requestedQuantity: quantity || undefined,
      unitCode: selectedSupplyItem ? requestUnit || selectedSupplyItem.baseUnit : undefined,
      deliveryLocation: requestDeliveryLocation,
      workItemReference: requestWorkReference,
      wbsReference: requestWbsReference,
      budgetCheckStatus: "NotConfigured",
      criticality: requestCriticality,
    }), "پیش‌نویس درخواست خرید ثبت شد.");
    if (!succeeded) return;
    setRequestNumber("");
    setRequestTitle("");
    setRequestDescription("");
    setRequestEstimate("");
    setRequestSupplyItemId("");
    setRequestQuantity("");
    setRequestUnit("");
    setRequestDeliveryLocation("");
    setRequestWorkReference("");
    setRequestWbsReference("");
    setRequestCriticality("Normal");
  }

  async function moveRequest(item: PurchaseRequestModel, action: "submit" | "approve" | "return") {
    const comment = action === "return" ? window.prompt("دلیل عودت درخواست خرید") ?? "" : "";
    if (action === "return" && !comment.trim()) return;
    await run(item.id, () => transitionPurchaseRequest(
      props.apiBaseUrl, identity, props.projectId, item, action, comment,
    ), "وضعیت درخواست خرید به‌روزرسانی شد.");
  }

  async function submitOrder(event: FormEvent) {
    event.preventDefault();
    const request = approvedRequests.find((item) => item.id === orderRequestId);
    const amount = Number(orderAmount);
    if (!request || !orderPartyId || !orderNumber.trim() || !orderTitle.trim() || !Number.isFinite(amount) || amount <= 0) {
      setMessage("درخواست تأییدشده، طرف، شماره، عنوان و مبلغ مثبت برای صدور تعهد الزامی است.");
      return;
    }

    const succeeded = await run("new-order", () => issuePurchaseOrder(props.apiBaseUrl, identity, props.projectId, {
      purchaseRequestId: request.id,
      purchaseRequestRevision: request.revision,
      partyId: orderPartyId,
      contractId: orderContractId || undefined,
      number: orderNumber,
      title: orderTitle,
      amount,
    }), "سفارش صادر و تعهد قطعی ثبت شد.");
    if (!succeeded) return;
    setOrderNumber("");
    setOrderTitle("");
    setOrderAmount("");
    setOrderRequestId("");
  }

  async function moveOrder(item: PurchaseOrderModel, action: "close" | "cancel") {
    const comment = window.prompt(action === "cancel" ? "دلیل ابطال سفارش" : "توضیح خاتمه سفارش") ?? "";
    if (action === "cancel" && !comment.trim()) return;
    await run(item.id, () => transitionPurchaseOrder(
      props.apiBaseUrl, identity, props.projectId, item, action, comment,
    ), "وضعیت تعهد به‌روزرسانی شد.");
  }

  return (
    <article className="operational-card commercial-control" id="commercial">
      <div className="card-heading">
        <div>
          <p className="eyebrow">کنترل پایه قرارداد و تدارکات</p>
          <h2>رجیستر قرارداد، الحاقیه و تعهد خرید</h2>
        </div>
        <div className="commercial-state-badges">
          <span>{metricStateLabel(state?.contractState)} قرارداد</span>
          <span>{metricStateLabel(state?.procurementState)} خرید</span>
        </div>
      </div>

      <div className="finance-metrics commercial-metrics">
        <Metric label="قرارداد فعال" value={state?.activeContractCount} />
        <Metric label="در انتظار تأیید" value={(state?.pendingContractApprovalCount ?? 0) + (state?.pendingProcurementApprovalCount ?? 0)} />
        <Metric label="تعهد باز" value={state?.openCommitmentCount} />
        <Metric label="تعهد سررسیدگذشته" value={state?.overdueCommitmentCount} warning={(state?.overdueCommitmentCount ?? 0) > 0} />
      </div>
      <p className="microcopy" aria-live="polite">{message}</p>

      <div className="commercial-columns">
        <section>
          <h3>طرف‌ها و قراردادها</h3>
          <form className="capture-form compact-form" onSubmit={(event) => void submitParty(event)}>
            <label className="field">کد طرف<input value={partyCode} onChange={(event) => setPartyCode(event.target.value)} /></label>
            <label className="field">نام حقوقی<input value={partyName} onChange={(event) => setPartyName(event.target.value)} /></label>
            <label className="field">نوع<select value={partyType} onChange={(event) => setPartyType(event.target.value as PartyType)}>
              <option value="Supplier">تأمین‌کننده</option><option value="Subcontractor">پیمانکار جزء</option>
              <option value="Consultant">مشاور</option><option value="LaborCrew">اکیپ دستمزدی</option><option value="Other">سایر</option>
            </select></label>
            <button type="submit" disabled={!props.isOnline || busyId === "new-party"}>ثبت طرف</button>
          </form>

          <form className="capture-form compact-form" onSubmit={(event) => void submitContract(event)}>
            <label className="field">طرف<select value={contractPartyId} onChange={(event) => setContractPartyId(event.target.value)}>
              <option value="">انتخاب کنید</option>{activeParties.map((party) => <option key={party.id} value={party.id}>{party.code} · {party.legalName}</option>)}
            </select></label>
            <label className="field">شماره قرارداد<input value={contractNumber} onChange={(event) => setContractNumber(event.target.value)} /></label>
            <label className="field full-width">عنوان<input value={contractTitle} onChange={(event) => setContractTitle(event.target.value)} /></label>
            <label className="field">نوع<select value={contractType} onChange={(event) => setContractType(event.target.value as ProjectContractType)}>
              <option value="MainContract">قرارداد اصلی</option><option value="Subcontract">پیمان جزء</option>
              <option value="Supply">تأمین</option><option value="ProfessionalService">خدمات تخصصی</option><option value="Labor">دستمزدی</option><option value="Other">سایر</option>
            </select></label>
            <label className="field">سقف اولیه (اختیاری)<input inputMode="decimal" value={contractAmount} onChange={(event) => setContractAmount(event.target.value)} /></label>
            <button type="submit" disabled={!props.isOnline || busyId === "new-contract"}>ثبت پیش‌نویس قرارداد</button>
          </form>

          <div className="finance-list">
            {contracts.slice(0, 6).map((item) => <div className="finance-item" key={item.id}>
              <div><strong>{item.number} · {item.title}</strong><span>{moneyOrUnknown(item.originalApprovedAmount, item.currencyCode)}</span><small>{contractStatusLabel(item.status)} · نسخه {item.revision.toLocaleString("fa-IR")}</small></div>
              <div className="review-actions">
                {(item.status === "Draft" || item.status === "Returned") && <button type="button" disabled={busyId === item.id} onClick={() => void moveContract(item, "submit")}>ارسال</button>}
                {item.status === "Submitted" && <><button type="button" disabled={busyId === item.id} onClick={() => void moveContract(item, "activate")}>فعال‌سازی</button><button className="secondary-button" type="button" disabled={busyId === item.id} onClick={() => void moveContract(item, "return")}>عودت</button></>}
                {item.status === "Active" && <button className="secondary-button" type="button" disabled={busyId === item.id} onClick={() => void moveContract(item, "close")}>خاتمه</button>}
              </div>
            </div>)}
          </div>

          <h3>الحاقیه قرارداد فعال</h3>
          <form className="capture-form compact-form" onSubmit={(event) => void submitAmendment(event)}>
            <label className="field full-width">قرارداد<select value={amendmentContractId} onChange={(event) => setAmendmentContractId(event.target.value)}>
              <option value="">انتخاب کنید</option>{activeContracts.map((item) => <option key={item.id} value={item.id}>{item.number} · {item.title}</option>)}
            </select></label>
            <label className="field">شماره<input value={amendmentNumber} onChange={(event) => setAmendmentNumber(event.target.value)} /></label>
            <label className="field">نوع<select value={amendmentType} onChange={(event) => setAmendmentType(event.target.value as ContractAmendmentType)}>
              <option value="ScopeChange">تغییر دامنه</option><option value="ValueChange">تغییر مبلغ</option><option value="TimeExtension">تمدید زمان</option><option value="Mixed">ترکیبی</option>
            </select></label>
            <label className="field full-width">عنوان<input value={amendmentTitle} onChange={(event) => setAmendmentTitle(event.target.value)} /></label>
            <label className="field">تغییر مبلغ<input inputMode="decimal" value={amendmentAmount} onChange={(event) => setAmendmentAmount(event.target.value)} /></label>
            <label className="field">تمدید روز<input inputMode="numeric" value={extensionDays} onChange={(event) => setExtensionDays(event.target.value)} /></label>
            <button type="submit" disabled={!props.isOnline || busyId === "new-amendment"}>ثبت الحاقیه</button>
          </form>
          <div className="finance-list">
            {amendments.slice(0, 4).map((item) => <div className="finance-item" key={item.id}>
              <div><strong>{item.number} · {item.title}</strong><span>{amendmentValue(item)}</span><small>{amendmentStatusLabel(item.status)}</small></div>
              <div className="review-actions">
                {(item.status === "Draft" || item.status === "Returned") && <button type="button" disabled={busyId === item.id} onClick={() => void moveAmendment(item, "submit")}>ارسال</button>}
                {item.status === "Submitted" && <><button type="button" disabled={busyId === item.id} onClick={() => void moveAmendment(item, "approve")}>تصویب</button><button className="secondary-button" type="button" disabled={busyId === item.id} onClick={() => void moveAmendment(item, "return")}>عودت</button></>}
              </div>
            </div>)}
          </div>
        </section>

        <section>
          <h3>درخواست خرید تا تعهد</h3>
          <form className="capture-form compact-form" onSubmit={(event) => void submitRequest(event)}>
            <label className="field">شماره درخواست<input value={requestNumber} onChange={(event) => setRequestNumber(event.target.value)} /></label>
            <label className="field">عنوان<input value={requestTitle} onChange={(event) => setRequestTitle(event.target.value)} /></label>
            <label className="field full-width">شرح<textarea rows={2} value={requestDescription} onChange={(event) => setRequestDescription(event.target.value)} /></label>
            <label className="field full-width">برآورد (اختیاری)<input inputMode="decimal" value={requestEstimate} onChange={(event) => setRequestEstimate(event.target.value)} /></label>
            <label className="field full-width">قلم تدارکاتی (اختیاری)<select value={requestSupplyItemId} onChange={(event) => { const id = event.target.value; setRequestSupplyItemId(id); const item = supplyItems.find((entry) => entry.id === id); setRequestUnit(item?.baseUnit ?? ""); }}><option value="">درخواست عمومی، بدون قلم موجودی</option>{supplyItems.map((item) => <option key={item.id} value={item.id}>{item.code} · {item.name}</option>)}</select></label>
            {requestSupplyItemId && <><label className="field">مقدار<input inputMode="decimal" value={requestQuantity} onChange={(event) => setRequestQuantity(event.target.value)} /></label><label className="field">واحد<input value={requestUnit} onChange={(event) => setRequestUnit(event.target.value)} /></label><label className="field full-width">محل تحویل<input value={requestDeliveryLocation} onChange={(event) => setRequestDeliveryLocation(event.target.value)} /></label></>}
            <label className="field">مرجع کار (اختیاری)<input value={requestWorkReference} onChange={(event) => setRequestWorkReference(event.target.value)} /></label>
            <label className="field">ساختار شکست کار (WBS) اختیاری<input value={requestWbsReference} onChange={(event) => setRequestWbsReference(event.target.value)} /></label>
            <label className="field full-width">بحرانی‌بودن<select value={requestCriticality} onChange={(event) => setRequestCriticality(event.target.value as typeof requestCriticality)}><option value="Normal">عادی</option><option value="Priority">اولویت‌دار</option><option value="Critical">بحرانی</option><option value="Emergency">اضطراری</option></select></label>
            <button type="submit" disabled={!props.isOnline || busyId === "new-request"}>ثبت درخواست</button>
          </form>
          <div className="finance-list">
            {requests.slice(0, 7).map((item) => <div className="finance-item" key={item.id}>
              <div><strong>{item.number} · {item.title}</strong><span>{item.requestedQuantity ? `${item.requestedQuantity.toLocaleString("fa-IR")} ${item.unitCode ?? ""} · ` : ""}{moneyOrUnknown(item.estimatedAmount, item.currencyCode, "بدون برآورد اولیه")}</span><small>{requestStatusLabel(item.status)} · {item.wbsReference ? "متصل به ساختار شکست کار (WBS)" : "بدون الزام ساختار شکست کار (WBS)"} · نسخه {item.revision.toLocaleString("fa-IR")}</small></div>
              <div className="review-actions">
                {(item.status === "Draft" || item.status === "Returned") && <button type="button" disabled={busyId === item.id} onClick={() => void moveRequest(item, "submit")}>ارسال</button>}
                {item.status === "Submitted" && <><button type="button" disabled={busyId === item.id} onClick={() => void moveRequest(item, "approve")}>تأیید</button><button className="secondary-button" type="button" disabled={busyId === item.id} onClick={() => void moveRequest(item, "return")}>عودت</button></>}
              </div>
            </div>)}
          </div>

          <h3>صدور سفارش و ثبت تعهد</h3>
          <form className="capture-form compact-form" onSubmit={(event) => void submitOrder(event)}>
            <label className="field full-width">درخواست تأییدشده<select value={orderRequestId} onChange={(event) => setOrderRequestId(event.target.value)}>
              <option value="">انتخاب کنید</option>{approvedRequests.map((item) => <option key={item.id} value={item.id}>{item.number} · {item.title}</option>)}
            </select></label>
            <label className="field">طرف<select value={orderPartyId} onChange={(event) => setOrderPartyId(event.target.value)}>
              <option value="">انتخاب کنید</option>{activeParties.map((item) => <option key={item.id} value={item.id}>{item.code} · {item.legalName}</option>)}
            </select></label>
            <label className="field">قرارداد (اختیاری)<select value={orderContractId} onChange={(event) => {
              const selectedId = event.target.value;
              setOrderContractId(selectedId);
              const selected = activeContracts.find((item) => item.id === selectedId);
              if (selected) setOrderPartyId(selected.partyId);
            }}>
              <option value="">بدون اتصال</option>{activeContracts.map((item) => <option key={item.id} value={item.id}>{item.number}</option>)}
            </select></label>
            <label className="field">شماره سفارش<input value={orderNumber} onChange={(event) => setOrderNumber(event.target.value)} /></label>
            <label className="field">مبلغ قطعی<input inputMode="decimal" value={orderAmount} onChange={(event) => setOrderAmount(event.target.value)} /></label>
            <label className="field full-width">عنوان<input value={orderTitle} onChange={(event) => setOrderTitle(event.target.value)} /></label>
            <button type="submit" disabled={!props.isOnline || busyId === "new-order"}>صدور تعهد</button>
          </form>
          <div className="finance-list">
            {orders.slice(0, 7).map((item) => <div className="finance-item" key={item.id}>
              <div><strong>{item.number} · {item.title}</strong><span>{moneyOrUnknown(item.amount, item.currencyCode)}</span><small>{orderStatusLabel(item.status)} · {item.contractId ? "متصل به قرارداد" : "بدون قرارداد"}</small></div>
              {item.status === "Issued" && <div className="review-actions"><button type="button" disabled={busyId === item.id} onClick={() => void moveOrder(item, "close")}>خاتمه</button><button className="secondary-button" type="button" disabled={busyId === item.id} onClick={() => void moveOrder(item, "cancel")}>ابطال</button></div>}
            </div>)}
          </div>
        </section>
      </div>
    </article>
  );
}

function Metric(props: { readonly label: string; readonly value?: number; readonly warning?: boolean }) {
  return <div className={props.warning ? "metric-warning" : undefined}><strong>{props.value?.toLocaleString("fa-IR") ?? "—"}</strong><span>{props.label}</span></div>;
}

function optionalPositiveNumber(value: string): number | null | false {
  if (!value.trim()) return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : false;
}

function optionalSignedNumber(value: string): number | null | false {
  if (!value.trim()) return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : false;
}

function optionalNonNegativeInteger(value: string): number | null | false {
  if (!value.trim()) return null;
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= 0 ? parsed : false;
}

function moneyOrUnknown(value: number | null, currency: string, unknown = "سقف مبلغ ثبت نشده"): string {
  return value === null ? unknown : formatAmountFa(value, currency);
}

function metricStateLabel(value?: CommercialStateModel["contractState"]): string {
  return ({ NotConfigured: "غیرفعال", SetupRequired: "نیازمند راه‌اندازی", NoData: "بدون داده", Available: "دارای داده", Suspended: "تعلیق" })[value ?? "NoData"];
}

function commercialStateMessage(value: CommercialStateModel): string {
  if (value.contractState === "NotConfigured" && value.procurementState === "NotConfigured") return "کنترل قرارداد و خرید برای این پروژه فعال نیست.";
  if (value.contractState === "NoData" && value.procurementState === "NoData") return "هنوز واقعیت قراردادی یا خرید ثبت نشده است؛ نبود داده به معنی وضعیت سالم نیست.";
  if (value.expiredActiveContractCount > 0 || value.overdueCommitmentCount > 0) return "قرارداد منقضی یا تعهد سررسیدگذشته نیازمند تعیین تکلیف است.";
  return "وضعیت تجاری از رجیسترهای نسخه‌دار و گردش‌کارهای تأییدشده ساخته شده است.";
}

function contractStatusLabel(value: ProjectContractModel["status"]): string {
  return ({ Draft: "پیش‌نویس", Submitted: "در انتظار بررسی", Returned: "عودت‌شده", Active: "فعال", Suspended: "تعلیق", Closed: "خاتمه‌یافته", Terminated: "فسخ‌شده" })[value];
}

function amendmentStatusLabel(value: ContractAmendmentModel["status"]): string {
  return ({ Draft: "پیش‌نویس", Submitted: "در انتظار بررسی", Returned: "عودت‌شده", Approved: "مصوب" })[value];
}

function amendmentValue(item: ContractAmendmentModel): string {
  const value = item.amountDelta === null ? "بدون تغییر مبلغ" : formatAmountFa(item.amountDelta, item.currencyCode);
  return item.extensionDays ? `${value} · ${item.extensionDays.toLocaleString("fa-IR")} روز` : value;
}

function requestStatusLabel(value: PurchaseRequestModel["status"]): string {
  return ({ Draft: "پیش‌نویس", Submitted: "در انتظار تأیید", Returned: "عودت‌شده", Approved: "تأییدشده", Ordered: "تبدیل به سفارش", Cancelled: "لغوشده" })[value];
}

function orderStatusLabel(value: PurchaseOrderModel["status"]): string {
  return ({ Issued: "تعهد باز", Closed: "خاتمه‌یافته", Cancelled: "باطل‌شده" })[value];
}
