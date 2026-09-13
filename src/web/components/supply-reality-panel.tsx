"use client";

import { type FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import { listPurchaseOrders, type PurchaseOrderModel } from "@/lib/commercial";
import { scopedStorageKey } from "@/lib/field-database";
import { toUserMessage } from "@/lib/localization";
import { formatPersianDateTime, todayIsoInProjectTimeZone } from "@/lib/persian-date";
import {
  acknowledgeMaterialIssue,
  createGoodsReceipt,
  createInventoryLocation,
  createMaterialIssue,
  createServiceAcceptance,
  createSupplyItem,
  getSupplyState,
  inspectReceipt,
  postInventoryAdjustment,
  proposeInventoryAdjustment,
  reconcileMaterialIssue,
  reviewInventoryAdjustment,
  transitionReceipt,
  transferInventory,
  type GoodsReceiptModel,
  type InventoryAdjustmentModel,
  type InventoryLocationType,
  type MaterialIssueModel,
  type SupplyItemKind,
  type SupplyStateModel,
  type SupplyTrackingPolicy,
} from "@/lib/supply";

interface SupplyRealityPanelProps {
  readonly apiBaseUrl: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly isOnline: boolean;
  readonly refreshToken: number;
  readonly onChanged?: () => void;
}

export function SupplyRealityPanel(props: SupplyRealityPanelProps) {
  const identity = useMemo(() => ({ tenantId: props.tenantId, userId: props.userId }), [props.tenantId, props.userId]);
  const cacheKey = useMemo(() => scopedStorageKey(`pmcs-supply-state:${props.projectId}`), [props.projectId]);
  const [state, setState] = useState<SupplyStateModel | null>(() => readCache(cacheKey));
  const [orders, setOrders] = useState<readonly PurchaseOrderModel[]>([]);
  const [message, setMessage] = useState("در حال دریافت واقعیت تدارکات و موجودی…");
  const [busyId, setBusyId] = useState<string | null>(null);

  const [itemCode, setItemCode] = useState("");
  const [itemName, setItemName] = useState("");
  const [itemCategory, setItemCategory] = useState("");
  const [itemUnit, setItemUnit] = useState("");
  const [itemKind, setItemKind] = useState<SupplyItemKind>("Material");
  const [trackingPolicy, setTrackingPolicy] = useState<SupplyTrackingPolicy>("Quantity");
  const [inspectionRequired, setInspectionRequired] = useState(true);

  const [locationCode, setLocationCode] = useState("");
  const [locationName, setLocationName] = useState("");
  const [locationType, setLocationType] = useState<InventoryLocationType>("SiteStore");

  const [receiptOrderId, setReceiptOrderId] = useState("");
  const [receiptLocationId, setReceiptLocationId] = useState("");
  const [dispatchNote, setDispatchNote] = useState("");
  const [receivedQuantity, setReceivedQuantity] = useState("");
  const [damagedQuantity, setDamagedQuantity] = useState("0");
  const [deliveryEvidence, setDeliveryEvidence] = useState("");
  const [excessReason, setExcessReason] = useState("");

  const [inspectionReceiptId, setInspectionReceiptId] = useState("");
  const [acceptedQuantity, setAcceptedQuantity] = useState("");
  const [rejectedQuantity, setRejectedQuantity] = useState("0");
  const [quarantinedQuantity, setQuarantinedQuantity] = useState("0");
  const [inspectionType, setInspectionType] = useState("بازرسی ظاهری و کمی");
  const [inspectionReference, setInspectionReference] = useState("");
  const [inspectionComment, setInspectionComment] = useState("");

  const [issueItemId, setIssueItemId] = useState("");
  const [issueLocationId, setIssueLocationId] = useState("");
  const [issueQuantity, setIssueQuantity] = useState("");
  const [issuedTo, setIssuedTo] = useState("");
  const [issueDestination, setIssueDestination] = useState("");
  const [issueWorkReference, setIssueWorkReference] = useState("");
  const [issueWbsReference, setIssueWbsReference] = useState("");
  const [issueEvidence, setIssueEvidence] = useState("");

  const [reconcileIssueId, setReconcileIssueId] = useState("");
  const [consumedQuantity, setConsumedQuantity] = useState("0");
  const [returnedQuantity, setReturnedQuantity] = useState("0");
  const [wasteQuantity, setWasteQuantity] = useState("0");
  const [wasteReason, setWasteReason] = useState("");
  const [reconcileEvidence, setReconcileEvidence] = useState("");

  const [transferItemId, setTransferItemId] = useState("");
  const [transferSourceId, setTransferSourceId] = useState("");
  const [transferDestinationId, setTransferDestinationId] = useState("");
  const [transferQuantity, setTransferQuantity] = useState("");
  const [transferReason, setTransferReason] = useState("");

  const [countItemId, setCountItemId] = useState("");
  const [countLocationId, setCountLocationId] = useState("");
  const [countedQuantity, setCountedQuantity] = useState("");
  const [countReason, setCountReason] = useState("");
  const [countEvidence, setCountEvidence] = useState("");

  const [serviceOrderId, setServiceOrderId] = useState("");
  const [serviceDelivered, setServiceDelivered] = useState("");
  const [serviceAccepted, setServiceAccepted] = useState("");
  const [serviceRejected, setServiceRejected] = useState("0");
  const [serviceCriteria, setServiceCriteria] = useState("");
  const [serviceEvidence, setServiceEvidence] = useState("");
  const [serviceComment, setServiceComment] = useState("");

  const load = useCallback(async () => {
    if (!props.isOnline) {
      setMessage(readCache(cacheKey) ? "نمای ذخیره‌شده نمایش داده می‌شود؛ اقدام رسمی تا اتصال مجدد غیرفعال است." : "برای دریافت واقعیت تدارکات، اتصال به سرور لازم است.");
      return;
    }
    try {
      const [nextState, nextOrders] = await Promise.all([
        getSupplyState(props.apiBaseUrl, identity, props.projectId),
        listPurchaseOrders(props.apiBaseUrl, identity, props.projectId),
      ]);
      setState(nextState);
      setOrders(nextOrders);
      window.localStorage.setItem(cacheKey, JSON.stringify(nextState));
      setMessage(supplyStateMessage(nextState));
    } catch (error) {
      setMessage(toUserMessage(error, "دریافت واقعیت تدارکات و موجودی ناموفق بود."));
    }
  }, [cacheKey, identity, props.apiBaseUrl, props.isOnline, props.projectId]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timeoutId);
  }, [load, props.refreshToken]);

  async function run(id: string, action: () => Promise<unknown>, success: string) {
    if (!props.isOnline) {
      setMessage("ثبت رسمی در حالت آفلاین مجاز نیست؛ داده موجود روی سرور تغییری نکرد.");
      return false;
    }
    setBusyId(id);
    try {
      await action();
      setMessage(success);
      await load();
      props.onChanged?.();
      return true;
    } catch (error) {
      setMessage(toUserMessage(error, "عملیات تدارکات و موجودی ناموفق بود."));
      return false;
    } finally {
      setBusyId(null);
    }
  }

  const activeMaterialItems = state?.items.filter((item) => item.status === "Active" && item.kind === "Material" && item.trackingPolicy !== "None") ?? [];
  const activeServiceItems = state?.items.filter((item) => item.status === "Active" && item.kind !== "Material") ?? [];
  const activeStockLocations = state?.locations.filter((item) => item.status === "Active" && item.allowsAvailableStock) ?? [];
  const issuedMaterialOrders = orders.filter((order) => order.status === "Issued" && order.supplyItemId && activeMaterialItems.some((item) => item.id === order.supplyItemId));
  const issuedServiceOrders = orders.filter((order) => order.status === "Issued" && order.supplyItemId && activeServiceItems.some((item) => item.id === order.supplyItemId));
  const pendingInspection = state?.receipts.filter((receipt) => receipt.status === "PendingInspection") ?? [];
  const openIssues = state?.materialIssues.filter((issue) => issue.status !== "Reconciled") ?? [];

  async function submitItem(event: FormEvent) {
    event.preventDefault();
    if (!itemCode.trim() || !itemName.trim() || !itemCategory.trim() || !itemUnit.trim()) {
      setMessage("کد، نام، دسته و واحد پایه قلم الزامی است."); return;
    }
    const succeeded = await run("new-item", () => createSupplyItem(props.apiBaseUrl, identity, props.projectId, {
      code: itemCode, name: itemName, category: itemCategory, baseUnit: itemUnit, kind: itemKind,
      trackingPolicy: itemKind === "Material" ? trackingPolicy : "None", inspectionRequired,
    }), "قلم تدارکاتی نسخه‌دار ثبت شد.");
    if (succeeded) { setItemCode(""); setItemName(""); setItemCategory(""); setItemUnit(""); }
  }

  async function submitLocation(event: FormEvent) {
    event.preventDefault();
    if (!locationCode.trim() || !locationName.trim()) { setMessage("کد و نام محل نگهداری الزامی است."); return; }
    const allowsAvailableStock = !["QuarantineArea", "RejectedArea"].includes(locationType);
    const succeeded = await run("new-location", () => createInventoryLocation(props.apiBaseUrl, identity, props.projectId, {
      code: locationCode, name: locationName, type: locationType, allowsAvailableStock,
    }), "محل نگهداری ثبت شد.");
    if (succeeded) { setLocationCode(""); setLocationName(""); }
  }

  async function submitReceipt(event: FormEvent) {
    event.preventDefault();
    const order = issuedMaterialOrders.find((item) => item.id === receiptOrderId);
    const received = positive(receivedQuantity);
    const damaged = nonNegative(damagedQuantity);
    if (!order?.supplyItemId || !receiptLocationId || received === null || damaged === null || !dispatchNote.trim() || !deliveryEvidence.trim()) {
      setMessage("سفارش کالایی، انبار، مقدار معتبر، حواله و مدرک تحویل الزامی است."); return;
    }
    const supplyItemId = order.supplyItemId;
    const succeeded = await run("new-receipt", () => createGoodsReceipt(props.apiBaseUrl, identity, props.projectId, {
      purchaseOrderId: order.id, itemId: supplyItemId, stockLocationId: receiptLocationId,
      dispatchNote, arrivedAt: new Date().toISOString(), deliveryLocation: order.deliveryLocation ?? "محل تحویل پروژه",
      shippedQuantity: received, receivedQuantity: received, damagedQuantity: damaged,
      unitCode: order.unitCode ?? undefined, excessApprovalReason: excessReason,
      evidenceReferences: evidenceList(deliveryEvidence),
    }), "دریافت فیزیکی ثبت شد؛ هنوز موجودی قابل مصرف ایجاد نشده است.");
    if (succeeded) { setDispatchNote(""); setReceivedQuantity(""); setDamagedQuantity("0"); setDeliveryEvidence(""); setExcessReason(""); }
  }

  async function submitInspection(event: FormEvent) {
    event.preventDefault();
    const receipt = pendingInspection.find((item) => item.id === inspectionReceiptId);
    const accepted = nonNegative(acceptedQuantity), rejected = nonNegative(rejectedQuantity), quarantined = nonNegative(quarantinedQuantity);
    if (!receipt || accepted === null || rejected === null || quarantined === null || !inspectionType.trim()) {
      setMessage("دریافت در انتظار بازرسی و مقادیر معتبر الزامی است."); return;
    }
    await run(`inspect-${receipt.id}`, () => inspectReceipt(props.apiBaseUrl, identity, props.projectId, receipt, {
      acceptedBaseQuantity: accepted, rejectedBaseQuantity: rejected, quarantinedBaseQuantity: quarantined,
      inspectionType, inspectionReference, comment: inspectionComment,
    }), "نتیجه بازرسی ثبت شد؛ فقط مقدار پذیرفته‌شده امکان ورود صریح به موجودی دارد.");
  }

  async function submitIssue(event: FormEvent) {
    event.preventDefault();
    const quantity = positive(issueQuantity);
    const item = activeMaterialItems.find((entry) => entry.id === issueItemId);
    if (!item || !issueLocationId || quantity === null || !issuedTo.trim() || !issueDestination.trim() || !issueEvidence.trim()) {
      setMessage("قلم، انبار، مقدار، تحویل‌گیرنده، مقصد و مدرک تحویل الزامی است."); return;
    }
    const succeeded = await run("new-issue", () => createMaterialIssue(props.apiBaseUrl, identity, props.projectId, {
      itemId: item.id, sourceLocationId: issueLocationId, quantity, unitCode: item.baseUnit,
      issuedTo, destinationLocation: issueDestination, workItemReference: issueWorkReference,
      wbsReference: issueWbsReference, evidenceReferences: evidenceList(issueEvidence),
    }), "تحویل ماده ثبت شد؛ مصرف یا نصب هنوز تأیید نشده است.");
    if (succeeded) { setIssueQuantity(""); setIssuedTo(""); setIssueDestination(""); setIssueEvidence(""); }
  }

  async function submitReconciliation(event: FormEvent) {
    event.preventDefault();
    const issue = openIssues.find((item) => item.id === reconcileIssueId);
    const consumed = nonNegative(consumedQuantity), returned = nonNegative(returnedQuantity), waste = nonNegative(wasteQuantity);
    if (!issue || consumed === null || returned === null || waste === null || consumed + returned + waste <= 0) {
      setMessage("تحویل باز و حداقل یک مقدار مصرف، برگشت یا پرت الزامی است."); return;
    }
    await run(`reconcile-${issue.id}`, () => reconcileMaterialIssue(props.apiBaseUrl, identity, props.projectId, issue, {
      consumedBaseQuantity: consumed, returnedBaseQuantity: returned, wasteBaseQuantity: waste,
      wasteReason, evidenceReferences: evidenceList(reconcileEvidence),
    }), "تسویه امانی ثبت شد؛ سابقه قبلی بدون بازنویسی باقی ماند.");
  }

  async function submitTransfer(event: FormEvent) {
    event.preventDefault();
    const item = activeMaterialItems.find((entry) => entry.id === transferItemId);
    const quantity = positive(transferQuantity);
    if (!item || !transferSourceId || !transferDestinationId || quantity === null || !transferReason.trim()) {
      setMessage("قلم، مبدأ، مقصد، مقدار و علت انتقال الزامی است."); return;
    }
    await run("new-transfer", () => transferInventory(props.apiBaseUrl, identity, props.projectId, {
      itemId: item.id, sourceLocationId: transferSourceId, destinationLocationId: transferDestinationId,
      quantity, unitCode: item.baseUnit, reason: transferReason,
    }), "انتقال با دو رخداد متوازن در دفتر موجودی ثبت شد.");
  }

  async function submitCount(event: FormEvent) {
    event.preventDefault();
    const quantity = nonNegative(countedQuantity);
    if (!countItemId || !countLocationId || quantity === null || !countReason.trim() || !countEvidence.trim()) {
      setMessage("قلم، محل، مقدار شمارش‌شده، علت و مدرک شمارش الزامی است."); return;
    }
    const succeeded = await run("new-adjustment", () => proposeInventoryAdjustment(props.apiBaseUrl, identity, props.projectId, {
      itemId: countItemId, locationId: countLocationId, cutoffAt: new Date().toISOString(),
      countedBaseQuantity: quantity, reason: countReason, evidenceReferences: evidenceList(countEvidence),
    }), "پیشنهاد اصلاح از مقایسه شمارش با دفتر سرور ساخته شد؛ هنوز موجودی تغییر نکرده است.");
    if (succeeded) { setCountedQuantity(""); setCountReason(""); setCountEvidence(""); }
  }

  async function submitServiceAcceptance(event: FormEvent) {
    event.preventDefault();
    const order = issuedServiceOrders.find((item) => item.id === serviceOrderId);
    const delivered = positive(serviceDelivered), accepted = nonNegative(serviceAccepted), rejected = nonNegative(serviceRejected);
    if (!order?.supplyItemId || delivered === null || accepted === null || rejected === null ||
        !serviceCriteria.trim() || !serviceEvidence.trim() || (rejected > 0 && !serviceComment.trim())) {
      setMessage("سفارش خدمت، مقادیر معتبر، معیار پذیرش و مدرک الزامی است؛ مقدار ردشده علت می‌خواهد."); return;
    }
    const supplyItemId = order.supplyItemId;
    const today = todayIsoInProjectTimeZone();
    await run("new-service", () => createServiceAcceptance(props.apiBaseUrl, identity, props.projectId, {
      purchaseOrderId: order.id, itemId: supplyItemId, periodStart: today, periodEnd: today,
      deliveredQuantity: delivered, acceptedQuantity: accepted, rejectedQuantity: rejected,
      unitCode: order.unitCode ?? undefined, acceptanceCriteria: serviceCriteria,
      evidenceReferences: evidenceList(serviceEvidence), comment: serviceComment,
    }), "پذیرش خدمت ثبت شد و هیچ موجودی کالایی ایجاد نکرد.");
  }

  return <article className="operational-card supply-reality" id="supply">
    <div className="card-heading"><div><p className="eyebrow">واقعیت تدارکات و موجودی</p><h2>از دریافت فیزیکی تا مصرف مستند</h2></div><span className={props.isOnline ? "status-chip positive" : "status-chip warning"}>{props.isOnline ? "برخط و رسمی" : "نمای ذخیره‌شده"}</span></div>
    <p className="microcopy" aria-live="polite">{message}</p>
    <div className="finance-metrics supply-metrics">
      <Metric label="در انتظار بازرسی" value={state?.pendingInspectionCount} warning={(state?.pendingInspectionCount ?? 0) > 0} />
      <Metric label="قرنطینه یا مردود" value={(state?.quarantinedReceiptCount ?? 0) + (state?.rejectedReceiptCount ?? 0)} warning={(state?.quarantinedReceiptCount ?? 0) + (state?.rejectedReceiptCount ?? 0) > 0} />
      <Metric label="امانت تسویه‌نشده" value={state?.unreconciledMaterialIssueCount} warning={(state?.unreconciledMaterialIssueCount ?? 0) > 0} />
      <Metric label="اصلاح در انتظار" value={state?.pendingAdjustmentApprovalCount} warning={(state?.pendingAdjustmentApprovalCount ?? 0) > 0} />
    </div>

    <details open><summary>قلم و محل نگهداری</summary><div className="supply-form-grid">
      <form className="capture-form compact-form" onSubmit={(event) => void submitItem(event)}>
        <h3>قلم نسخه‌دار</h3>
        <label className="field">کد<input value={itemCode} onChange={(event) => setItemCode(event.target.value)} /></label>
        <label className="field">نام<input value={itemName} onChange={(event) => setItemName(event.target.value)} /></label>
        <label className="field">دسته<input value={itemCategory} onChange={(event) => setItemCategory(event.target.value)} /></label>
        <label className="field">واحد پایه<input value={itemUnit} onChange={(event) => setItemUnit(event.target.value)} /></label>
        <label className="field">نوع<select value={itemKind} onChange={(event) => { const value = event.target.value as SupplyItemKind; setItemKind(value); setTrackingPolicy(value === "Material" ? "Quantity" : "None"); }}><option value="Material">کالا یا ماده</option><option value="Service">خدمت</option><option value="EquipmentRental">اجاره تجهیز</option></select></label>
        <label className="field">رهگیری<select value={trackingPolicy} disabled onChange={(event) => setTrackingPolicy(event.target.value as SupplyTrackingPolicy)}>{itemKind === "Material" ? <option value="Quantity">مقداری</option> : <option value="None">بدون موجودی کالایی</option>}</select></label>
        <label className="field checkbox-field"><input type="checkbox" checked={inspectionRequired} onChange={(event) => setInspectionRequired(event.target.checked)} />نیازمند بازرسی</label>
        <button disabled={!props.isOnline || busyId === "new-item"}>ثبت قلم</button>
      </form>
      <form className="capture-form compact-form" onSubmit={(event) => void submitLocation(event)}>
        <h3>محل موجودی</h3>
        <label className="field">کد<input value={locationCode} onChange={(event) => setLocationCode(event.target.value)} /></label>
        <label className="field">نام<input value={locationName} onChange={(event) => setLocationName(event.target.value)} /></label>
        <label className="field full-width">نوع<select value={locationType} onChange={(event) => setLocationType(event.target.value as InventoryLocationType)}>{locationTypeOptions.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
        <p className="microcopy full-width">قرنطینه و محل مردودی هرگز در موجودی قابل مصرف محاسبه نمی‌شوند.</p>
        <button disabled={!props.isOnline || busyId === "new-location"}>ثبت محل</button>
      </form>
    </div></details>

    <details open><summary>دریافت و بازرسی</summary><div className="supply-form-grid">
      <form className="capture-form compact-form" onSubmit={(event) => void submitReceipt(event)}>
        <h3>ثبت دریافت فیزیکی</h3>
        <label className="field full-width">سفارش کالایی<select value={receiptOrderId} onChange={(event) => setReceiptOrderId(event.target.value)}><option value="">انتخاب کنید</option>{issuedMaterialOrders.map((order) => <option key={order.id} value={order.id}>{order.number} · {order.title}</option>)}</select></label>
        <label className="field">انبار مقصد<select value={receiptLocationId} onChange={(event) => setReceiptLocationId(event.target.value)}><option value="">انتخاب کنید</option>{activeStockLocations.map((location) => <option key={location.id} value={location.id}>{location.code} · {location.name}</option>)}</select></label>
        <label className="field">حواله حمل<input value={dispatchNote} onChange={(event) => setDispatchNote(event.target.value)} /></label>
        <label className="field">مقدار رسیده<input inputMode="decimal" value={receivedQuantity} onChange={(event) => setReceivedQuantity(event.target.value)} /></label>
        <label className="field">مقدار آسیب‌دیده<input inputMode="decimal" value={damagedQuantity} onChange={(event) => setDamagedQuantity(event.target.value)} /></label>
        <label className="field full-width">شناسه یا پیوند مدرک تحویل<input value={deliveryEvidence} onChange={(event) => setDeliveryEvidence(event.target.value)} placeholder="هر مدرک در یک خط" /></label>
        <label className="field full-width">علت اضافه‌تحویل (در صورت نیاز)<input value={excessReason} onChange={(event) => setExcessReason(event.target.value)} /></label>
        <button disabled={!props.isOnline || busyId === "new-receipt"}>ثبت دریافت، بدون ورود به موجودی</button>
      </form>
      <form className="capture-form compact-form" onSubmit={(event) => void submitInspection(event)}>
        <h3>تصمیم بازرسی</h3>
        <label className="field full-width">دریافت در انتظار<select value={inspectionReceiptId} onChange={(event) => { setInspectionReceiptId(event.target.value); const receipt = pendingInspection.find((item) => item.id === event.target.value); if (receipt) setAcceptedQuantity(String(receipt.baseReceivedQuantity)); }}><option value="">انتخاب کنید</option>{pendingInspection.map((receipt) => <option key={receipt.id} value={receipt.id}>{receipt.number} · {receipt.baseReceivedQuantity.toLocaleString("fa-IR")} {receipt.baseUnit}</option>)}</select></label>
        <label className="field">پذیرفته<input inputMode="decimal" value={acceptedQuantity} onChange={(event) => setAcceptedQuantity(event.target.value)} /></label>
        <label className="field">مردود<input inputMode="decimal" value={rejectedQuantity} onChange={(event) => setRejectedQuantity(event.target.value)} /></label>
        <label className="field">قرنطینه<input inputMode="decimal" value={quarantinedQuantity} onChange={(event) => setQuarantinedQuantity(event.target.value)} /></label>
        <label className="field">نوع بازرسی<input value={inspectionType} onChange={(event) => setInspectionType(event.target.value)} /></label>
        <label className="field">مرجع بازرسی<input value={inspectionReference} onChange={(event) => setInspectionReference(event.target.value)} /></label>
        <label className="field full-width">نتیجه و علت عدم پذیرش<textarea rows={2} value={inspectionComment} onChange={(event) => setInspectionComment(event.target.value)} /></label>
        <button disabled={!props.isOnline || busyId?.startsWith("inspect-")}>ثبت نتیجه مستقل</button>
      </form>
    </div>
    <div className="supply-register">{state?.receipts.slice(0, 10).map((receipt) => <ReceiptRow key={receipt.id} receipt={receipt} busy={busyId === receipt.id} onSubmit={() => void run(receipt.id, () => transitionReceipt(props.apiBaseUrl, identity, props.projectId, receipt, "submit-inspection"), "دریافت برای بازرسی ارسال شد.")} onPost={() => void run(receipt.id, () => transitionReceipt(props.apiBaseUrl, identity, props.projectId, receipt, "post-stock"), "فقط مقدار پذیرفته‌شده در دفتر موجودی ثبت شد.")} />)}</div>
    </details>

    <details open><summary>موجودی و امانت کارگاه</summary>
      <div className="stock-table" role="table" aria-label="موجودی رسمی به تفکیک قلم و محل">{state?.stockPositions.length ? state.stockPositions.map((position) => <div className="stock-row" role="row" key={`${position.itemId}:${position.locationId}`}><strong>{labelItem(state, position.itemId)}</strong><span>{labelLocation(state, position.locationId)}</span><span>{position.onHandBaseQuantity.toLocaleString("fa-IR")} {position.baseUnit}</span><small>آخرین حرکت: {dateFa(position.lastMovementAt)}</small></div>) : <p className="empty-state">هنوز موجودی پذیرفته‌شده و ثبت‌شده‌ای وجود ندارد.</p>}</div>
      <div className="supply-form-grid">
        <form className="capture-form compact-form" onSubmit={(event) => void submitIssue(event)}><h3>تحویل به اکیپ یا محل مصرف</h3>
          <label className="field">قلم<select value={issueItemId} onChange={(event) => setIssueItemId(event.target.value)}><option value="">انتخاب کنید</option>{activeMaterialItems.map((item) => <option key={item.id} value={item.id}>{item.code} · {item.name}</option>)}</select></label>
          <label className="field">انبار<select value={issueLocationId} onChange={(event) => setIssueLocationId(event.target.value)}><option value="">انتخاب کنید</option>{activeStockLocations.map((location) => <option key={location.id} value={location.id}>{location.name}</option>)}</select></label>
          <label className="field">مقدار<input inputMode="decimal" value={issueQuantity} onChange={(event) => setIssueQuantity(event.target.value)} /></label><label className="field">تحویل‌گیرنده<input value={issuedTo} onChange={(event) => setIssuedTo(event.target.value)} /></label>
          <label className="field">مقصد<input value={issueDestination} onChange={(event) => setIssueDestination(event.target.value)} /></label><label className="field">مرجع کار (اختیاری)<input value={issueWorkReference} onChange={(event) => setIssueWorkReference(event.target.value)} /></label>
          <label className="field">ساختار شکست کار (WBS) اختیاری<input value={issueWbsReference} onChange={(event) => setIssueWbsReference(event.target.value)} /></label><label className="field">مدرک تحویل<input value={issueEvidence} onChange={(event) => setIssueEvidence(event.target.value)} /></label>
          <button disabled={!props.isOnline || busyId === "new-issue"}>ثبت تحویل؛ نه مصرف</button>
        </form>
        <form className="capture-form compact-form" onSubmit={(event) => void submitReconciliation(event)}><h3>تسویه امانی</h3>
          <label className="field full-width">تحویل باز<select value={reconcileIssueId} onChange={(event) => setReconcileIssueId(event.target.value)}><option value="">انتخاب کنید</option>{openIssues.map((issue) => <option key={issue.id} value={issue.id}>{issue.number} · مانده {issue.unreconciledBaseQuantity.toLocaleString("fa-IR")}</option>)}</select></label>
          <label className="field">مصرف‌شده<input inputMode="decimal" value={consumedQuantity} onChange={(event) => setConsumedQuantity(event.target.value)} /></label><label className="field">برگشتی<input inputMode="decimal" value={returnedQuantity} onChange={(event) => setReturnedQuantity(event.target.value)} /></label>
          <label className="field">پرت<input inputMode="decimal" value={wasteQuantity} onChange={(event) => setWasteQuantity(event.target.value)} /></label><label className="field">علت پرت<input value={wasteReason} onChange={(event) => setWasteReason(event.target.value)} /></label>
          <label className="field full-width">مدرک تسویه یا پرت<input value={reconcileEvidence} onChange={(event) => setReconcileEvidence(event.target.value)} /></label>
          <button disabled={!props.isOnline || busyId?.startsWith("reconcile-")}>ثبت تسویه افزایشی</button>
        </form>
      </div>
      <div className="supply-register">{state?.materialIssues.slice(0, 8).map((issue) => <IssueRow key={issue.id} issue={issue} busy={busyId === issue.id} onAcknowledge={() => { const reference = window.prompt("مرجع تأیید تحویل")?.trim(); if (reference) void run(issue.id, () => acknowledgeMaterialIssue(props.apiBaseUrl, identity, props.projectId, issue, reference), "دریافت امانت توسط تحویل‌گیرنده تأیید شد."); }} />)}</div>
    </details>

    <details><summary>انتقال، شمارش و اصلاح کنترل‌شده</summary><div className="supply-form-grid">
      <form className="capture-form compact-form" onSubmit={(event) => void submitTransfer(event)}><h3>انتقال بین محل‌ها</h3>
        <label className="field">قلم<select value={transferItemId} onChange={(event) => setTransferItemId(event.target.value)}><option value="">انتخاب کنید</option>{activeMaterialItems.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
        <label className="field">مبدأ<select value={transferSourceId} onChange={(event) => setTransferSourceId(event.target.value)}><option value="">انتخاب کنید</option>{activeStockLocations.map((location) => <option key={location.id} value={location.id}>{location.name}</option>)}</select></label>
        <label className="field">مقصد<select value={transferDestinationId} onChange={(event) => setTransferDestinationId(event.target.value)}><option value="">انتخاب کنید</option>{activeStockLocations.map((location) => <option key={location.id} value={location.id}>{location.name}</option>)}</select></label>
        <label className="field">مقدار<input inputMode="decimal" value={transferQuantity} onChange={(event) => setTransferQuantity(event.target.value)} /></label><label className="field full-width">علت<input value={transferReason} onChange={(event) => setTransferReason(event.target.value)} /></label>
        <button disabled={!props.isOnline || busyId === "new-transfer"}>ثبت انتقال متوازن</button>
      </form>
      <form className="capture-form compact-form" onSubmit={(event) => void submitCount(event)}><h3>شمارش و پیشنهاد اصلاح</h3>
        <label className="field">قلم<select value={countItemId} onChange={(event) => setCountItemId(event.target.value)}><option value="">انتخاب کنید</option>{activeMaterialItems.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
        <label className="field">محل<select value={countLocationId} onChange={(event) => setCountLocationId(event.target.value)}><option value="">انتخاب کنید</option>{activeStockLocations.map((location) => <option key={location.id} value={location.id}>{location.name}</option>)}</select></label>
        <label className="field">موجودی شمارش‌شده<input inputMode="decimal" value={countedQuantity} onChange={(event) => setCountedQuantity(event.target.value)} /></label><label className="field">علت اختلاف<input value={countReason} onChange={(event) => setCountReason(event.target.value)} /></label>
        <label className="field full-width">مدرک شمارش<input value={countEvidence} onChange={(event) => setCountEvidence(event.target.value)} /></label><button disabled={!props.isOnline || busyId === "new-adjustment"}>ساخت پیشنهاد؛ بدون بازنویسی مستقیم</button>
      </form>
    </div><div className="supply-register">{state?.adjustments.slice(0, 8).map((adjustment) => <AdjustmentRow key={adjustment.id} adjustment={adjustment} busy={busyId === adjustment.id} onReview={(approved) => void run(adjustment.id, () => reviewInventoryAdjustment(props.apiBaseUrl, identity, props.projectId, adjustment, approved), approved ? "اصلاح تأیید شد؛ هنوز باید در دفتر ثبت شود." : "پیشنهاد اصلاح رد شد.")} onPost={() => void run(adjustment.id, () => postInventoryAdjustment(props.apiBaseUrl, identity, props.projectId, adjustment), "اصلاح تأییدشده به‌صورت رخداد در دفتر ثبت شد.")} />)}</div></details>

    <details><summary>پذیرش خدمت و اجاره تجهیز</summary><form className="capture-form compact-form supply-single-form" onSubmit={(event) => void submitServiceAcceptance(event)}>
      <label className="field full-width">سفارش خدمت<select value={serviceOrderId} onChange={(event) => setServiceOrderId(event.target.value)}><option value="">انتخاب کنید</option>{issuedServiceOrders.map((order) => <option key={order.id} value={order.id}>{order.number} · {order.title}</option>)}</select></label>
      <label className="field">انجام‌شده<input inputMode="decimal" value={serviceDelivered} onChange={(event) => setServiceDelivered(event.target.value)} /></label><label className="field">پذیرفته<input inputMode="decimal" value={serviceAccepted} onChange={(event) => setServiceAccepted(event.target.value)} /></label><label className="field">ردشده<input inputMode="decimal" value={serviceRejected} onChange={(event) => setServiceRejected(event.target.value)} /></label>
      <label className="field full-width">معیار پذیرش<input value={serviceCriteria} onChange={(event) => setServiceCriteria(event.target.value)} /></label><label className="field full-width">مدرک انجام کار<input value={serviceEvidence} onChange={(event) => setServiceEvidence(event.target.value)} /></label>
      <label className="field full-width">توضیح و علت رد خدمت<input value={serviceComment} onChange={(event) => setServiceComment(event.target.value)} /></label>
      <button disabled={!props.isOnline || busyId === "new-service"}>ثبت پذیرش بدون ایجاد موجودی</button>
    </form></details>

    <details><summary>ردیابی دفتر تغییرناپذیر</summary><div className="supply-register">{state?.ledger.slice(0, 20).map((entry) => <div className="supply-row" key={entry.id}><div><strong>{inventoryEventLabel(entry.eventType)} · {labelItem(state, entry.itemId)}</strong><span>{signed(entry.baseQuantityDelta)} {entry.baseUnit} · {labelLocation(state, entry.locationId)}</span><small>{dateFa(entry.occurredAt)} · مرجع {entry.sourceType}</small></div></div>)}</div></details>
  </article>;
}

function Metric({ label, value, warning = false }: { readonly label: string; readonly value?: number; readonly warning?: boolean }) { return <div className={warning ? "metric-warning" : undefined}><strong>{value?.toLocaleString("fa-IR") ?? "—"}</strong><span>{label}</span></div>; }
function ReceiptRow({ receipt, busy, onSubmit, onPost }: { readonly receipt: GoodsReceiptModel; readonly busy: boolean; readonly onSubmit: () => void; readonly onPost: () => void }) { return <div className="supply-row"><div><strong>{receipt.number}</strong><span>{receipt.baseReceivedQuantity.toLocaleString("fa-IR")} {receipt.baseUnit} · {receiptStatusLabel(receipt.status)}</span><small>{receipt.stockPostedAt ? `ورود موجودی: ${dateFa(receipt.stockPostedAt)}` : "هنوز وارد موجودی قابل مصرف نشده"}</small></div><div className="review-actions">{receipt.status === "Received" && <button disabled={busy} onClick={onSubmit}>ارسال به بازرسی</button>}{["Accepted", "PartiallyAccepted"].includes(receipt.status) && !receipt.stockPostedAt && <button disabled={busy} onClick={onPost}>ثبت مقدار پذیرفته در موجودی</button>}</div></div>; }
function IssueRow({ issue, busy, onAcknowledge }: { readonly issue: MaterialIssueModel; readonly busy: boolean; readonly onAcknowledge: () => void }) { return <div className="supply-row"><div><strong>{issue.number} · {issue.issuedTo}</strong><span>تحویل {issue.issuedBaseQuantity.toLocaleString("fa-IR")} · مانده امانی {issue.unreconciledBaseQuantity.toLocaleString("fa-IR")} {issue.baseUnit}</span><small>{issueStatusLabel(issue.status)}{issue.wbsReference ? ` · ساختار شکست کار (WBS): ${issue.wbsReference}` : " · بدون الزام ساختار شکست کار (WBS)"}</small></div>{issue.status === "Issued" && <button disabled={busy} onClick={onAcknowledge}>تأیید دریافت</button>}</div>; }
function AdjustmentRow({ adjustment, busy, onReview, onPost }: { readonly adjustment: InventoryAdjustmentModel; readonly busy: boolean; readonly onReview: (approved: boolean) => void; readonly onPost: () => void }) { return <div className="supply-row"><div><strong>{adjustment.number}</strong><span>دفتر {adjustment.systemBaseQuantity.toLocaleString("fa-IR")} ← شمارش {adjustment.countedBaseQuantity.toLocaleString("fa-IR")} {adjustment.baseUnit}</span><small>{adjustmentStatusLabel(adjustment.status)} · اختلاف {signed(adjustment.deltaBaseQuantity)}</small></div><div className="review-actions">{adjustment.status === "Proposed" && <><button disabled={busy} onClick={() => onReview(true)}>تأیید</button><button className="secondary-button" disabled={busy} onClick={() => onReview(false)}>رد</button></>}{adjustment.status === "Approved" && <button disabled={busy} onClick={onPost}>ثبت رخداد اصلاح</button>}</div></div>; }
function positive(value: string) { const parsed = Number(value); return Number.isFinite(parsed) && parsed > 0 ? parsed : null; }
function nonNegative(value: string) { const parsed = Number(value); return Number.isFinite(parsed) && parsed >= 0 ? parsed : null; }
function evidenceList(value: string) { return value.split(/\r?\n|،|,/).map((entry) => entry.trim()).filter(Boolean); }
function readCache(key: string): SupplyStateModel | null { if (typeof window === "undefined") return null; try { return JSON.parse(window.localStorage.getItem(key) ?? "null") as SupplyStateModel | null; } catch { return null; } }
function labelItem(state: SupplyStateModel | null, id: string) { const item = state?.items.find((entry) => entry.id === id); return item ? `${item.code} · ${item.name}` : "قلم ناشناخته"; }
function labelLocation(state: SupplyStateModel | null, id: string) { const location = state?.locations.find((entry) => entry.id === id); return location ? `${location.code} · ${location.name}` : "محل ناشناخته"; }
function dateFa(value: string | null) { return formatPersianDateTime(value); }
function signed(value: number) { return `${value > 0 ? "+" : ""}${value.toLocaleString("fa-IR")}`; }
function supplyStateMessage(state: SupplyStateModel) { if (!state.items.length) return "ابتدا قلم‌ها و محل‌های واقعی پروژه را تعریف کنید؛ هیچ داده نمونه‌ای ایجاد نشده است."; if (state.pendingInspectionCount) return "دریافت فیزیکی ثبت شده، اما تا تصمیم بازرسی و ثبت صریح، موجودی قابل مصرف نیست."; return "موجودی از جمع رخدادهای تغییرناپذیر محاسبه شده است؛ دریافت، پذیرش، تحویل و مصرف یکی نیستند."; }
function receiptStatusLabel(value: GoodsReceiptModel["status"]) { return ({ Received: "دریافت فیزیکی", PendingInspection: "در انتظار بازرسی", Accepted: "پذیرفته", PartiallyAccepted: "پذیرش جزئی", Rejected: "مردود", Quarantined: "قرنطینه" })[value]; }
function issueStatusLabel(value: MaterialIssueModel["status"]) { return ({ Issued: "تحویل‌شده", PendingReconciliation: "در انتظار تسویه", PartiallyReconciled: "تسویه جزئی", Reconciled: "تسویه کامل" })[value]; }
function adjustmentStatusLabel(value: InventoryAdjustmentModel["status"]) { return ({ Proposed: "پیشنهادشده", Approved: "تأییدشده", Rejected: "ردشده", Posted: "ثبت‌شده در دفتر" })[value]; }
function inventoryEventLabel(value: SupplyStateModel["ledger"][number]["eventType"]) { return ({ AcceptedReceipt: "ورود پذیرش‌شده", MaterialIssue: "تحویل ماده", TransferOut: "خروج انتقال", TransferIn: "ورود انتقال", ReturnFromSite: "برگشت از کارگاه", Adjustment: "اصلاح مصوب", Reversal: "برگشت رخداد" })[value]; }
const locationTypeOptions: readonly [InventoryLocationType, string][] = [["CentralStore", "انبار مرکزی"], ["SiteStore", "انبار کارگاه"], ["FloorOrZoneStore", "انبار طبقه یا ناحیه"], ["LaydownArea", "محوطه دپو"], ["ContractorCustody", "امانت پیمانکار"], ["QuarantineArea", "قرنطینه"], ["RejectedArea", "محل مردودی"]];
