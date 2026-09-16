export interface ApiProblem {
  readonly code?: string;
  readonly title?: string;
  readonly detail?: string;
  readonly message?: string;
}

export class ApiRequestError extends Error {
  readonly status: number;
  readonly code?: string;
  readonly retryAfterSeconds?: number;

  constructor(message: string, status: number, code?: string, retryAfterSeconds?: number) {
    super(message);
    this.name = "ApiRequestError";
    this.status = status;
    this.code = code;
    this.retryAfterSeconds = retryAfterSeconds;
  }
}

const statusMessages: Readonly<Record<number, string>> = {
  400: "درخواست ارسال‌شده معتبر نیست.",
  401: "برای ادامه باید وارد حساب کاربری شوید.",
  403: "شما اجازه انجام این عملیات را ندارید.",
  404: "اطلاعات درخواستی پیدا نشد.",
  409: "اطلاعات هم‌زمان تغییر کرده است؛ صفحه را تازه کنید و دوباره تلاش کنید.",
  410: "مهلت این درخواست پایان یافته است؛ دوباره تلاش کنید.",
  413: "حجم فایل بیشتر از حد مجاز است.",
  422: "اطلاعات واردشده با قواعد پروژه سازگار نیست.",
  429: "تعداد درخواست‌ها زیاد است؛ کمی بعد دوباره تلاش کنید.",
  503: "سرویس موردنیاز هنوز آماده نیست؛ مدیر سامانه باید پیکربندی را بررسی کند.",
};

const exactCodeMessages: Readonly<Record<string, string>> = {
  "idempotency.key.required": "شناسه یکتای درخواست ارسال نشده است؛ دوباره تلاش کنید.",
  "idempotency.key.invalid": "شناسه یکتای درخواست معتبر نیست؛ صفحه را تازه کنید و دوباره تلاش کنید.",
  "idempotency.key.reused": "این درخواست قبلاً با اطلاعات متفاوت ثبت شده است.",
  "idempotency.operation.in_progress": "درخواست مشابه در حال ثبت است؛ یک لحظه بعد دوباره تلاش کنید.",
  "rate_limit.exceeded": "تعداد درخواست‌ها زیاد است؛ کمی بعد دوباره تلاش کنید.",
  "sync.concurrency.conflict": "اطلاعات هم‌زمان تغییر کرده است؛ موارد تعارض را بررسی کنید.",
  "sync.client.update_required": "نسخه برنامه برای همگام‌سازی قدیمی است؛ ابتدا برنامه را به‌روزرسانی کنید.",
  "sync.protocol.unsupported": "نسخه قرارداد همگام‌سازی پشتیبانی نمی‌شود؛ برنامه را به‌روزرسانی کنید.",
  "sync.schema.update_required": "ساختار داده محلی نیازمند ارتقا است؛ برنامه را دوباره باز کنید.",
  "sync.device.revoked": "دسترسی این دستگاه لغو شده است؛ داده‌های ارسال‌نشده را برای بازیابی امن بررسی کنید.",
  "sync.device_or_lease.revoked": "نشست یا مجوز آفلاین این دستگاه دیگر معتبر نیست.",
  "sync.session.required": "پیش از همگام‌سازی باید نشست امن دستگاه ایجاد شود.",
  "sync.session.invalid_or_expired": "نشست همگام‌سازی منقضی شده است؛ دوباره همگام‌سازی را آغاز کنید.",
  "sync.project.access_denied": "دسترسی آفلاین به این پروژه در حال حاضر مجاز نیست.",
  "sync.project.not_active": "پروژه برای ثبت عملیات فعال نیست؛ وضعیت پروژه را بررسی کنید.",
  "sync.pull.access_denied": "دریافت تغییرهای این پروژه با دسترسی فعلی مجاز نیست.",
  "sync.lease.operation_not_covered": "این عملیات بیرون از محدوده یا زمان مجوز آفلاین ثبت شده و خودکار رسمی نشد.",
  "sync.checkpoint.mismatch": "نقطه کنترل دستگاه با سرور یکسان نیست؛ دریافت کنترل‌شده باید از نو انجام شود.",
  "sync.checkpoint.offer.invalid": "پیشنهاد نقطه کنترل منقضی یا قبلاً استفاده شده است؛ دریافت تغییرها را تکرار کنید.",
  "sync.checkpoint.regression": "نقطه کنترل قدیمی‌تر از وضعیت ثبت‌شده سرور است و پذیرفته نشد.",
  "sync.conflict.already_resolved": "این تعارض قبلاً تعیین تکلیف شده است.",
  "sync.conflict.replacement.required": "برای بازاعمال باید یک عملیات جدید با شناسه مستقل ساخته شود.",
  "sync.operation.dependency.blocked": "این عملیات تا تعیین تکلیف عملیات وابسته در صف باقی می‌ماند.",
  "sync.operation.dependency.out_of_order": "ترتیب وابستگی عملیات محلی معتبر نیست و نیازمند بررسی است.",
  "action.assignee.not_assignable": "فرد انتخاب‌شده امکان پذیرش این اقدام را ندارد.",
  "action.due_date.in_past": "تاریخ سررسید نمی‌تواند در گذشته باشد.",
  "attention.already_triaged": "این مورد قبلاً تعیین تکلیف شده است.",
  "attention.source.not_found_or_not_approved": "مشاهده تأییدشده موردنظر پیدا نشد.",
  "evidence.content_type.unsupported": "نوع فایل انتخاب‌شده پشتیبانی نمی‌شود.",
  "evidence.file.too_large": "حجم فایل بیشتر از حد مجاز است.",
  "evidence.upload.expired": "مهلت بارگذاری پایان یافته است؛ دوباره تلاش کنید.",
  "ai.provider.not_configured": "اتصال سرویس هوش مصنوعی هنوز توسط مدیر سامانه پیکربندی نشده است.",
  "ai.provider.unavailable": "سرویس تحلیل فعلاً در دسترس نیست؛ وضعیت رسمی پروژه بدون تغییر باقی مانده است.",
  "ai.provider.timeout": "زمان پاسخ‌گویی سرویس تحلیل پایان یافت؛ کمی بعد دوباره تلاش کنید.",
  "ai.provider.incomplete": "پاسخ سرویس تحلیل کامل نبود و منتشر نشد.",
  "ai.provider.rejected": "پیکربندی یا مجوز سرویس تحلیل معتبر نیست؛ مدیر سامانه باید آن را بررسی کند.",
  "ai.provider.refusal": "سرویس تحلیل برای این درخواست خروجی قابل انتشار تولید نکرد.",
  "ai.provider.invalid_response": "خروجی تحلیل با قرارداد ساختاری سامانه سازگار نبود و منتشر نشد.",
  "ai.context.no_snapshot": "برای تحلیل، ابتدا تصویر رسمی وضعیت پروژه را محاسبه کنید.",
  "ai.context.stale_snapshot": "تصویر رسمی وضعیت قدیمی است؛ ابتدا وضعیت پروژه را دوباره محاسبه کنید.",
  "ai.request.already_active": "یک درخواست تحلیل برای شما در حال پردازش است.",
  "ai.permission.revoked": "دسترسی تولید تحلیل پیش از پردازش تغییر کرده است؛ درخواست اجرا نشد.",
  "ai.processing.failed": "پردازش تحلیل کامل نشد؛ وضعیت رسمی پروژه بدون تغییر باقی ماند.",
  "insight.citation.unsupported": "یکی از ارجاعات تحلیل قابل اثبات نبود؛ خروجی منتشر نشد.",
  "insight.language.invalid": "خروجی فارسی معتبر تولید نشد و به رابط کاربر راه نیافت.",
  "insight.expired": "اعتبار زمانی این تحلیل پایان یافته است؛ تحلیل جدیدی تولید کنید.",
  "insight.snapshot.stale": "این تحلیل مربوط به تصویر قبلی پروژه است و دیگر قابل پذیرش نیست.",
  "insight.already_reviewed": "این تحلیل قبلاً بازبینی شده است.",
  "identity_provider.not_configured": "اتصال مدیریت حساب‌ها هنوز توسط مدیر سامانه پیکربندی نشده است.",
  "identity_provider.email.already_owned": "این نشانی ایمیل پیش‌تر در سامانه ورود ثبت شده و قابل اتصال خودکار نیست.",
  "identity_provider.invitation.delivery_failed": "ارسال ایمیل دعوت انجام نشد؛ سامانه به‌صورت خودکار دوباره تلاش می‌کند.",
  "user.email.duplicate": "برای این نشانی ایمیل قبلاً حساب ساخته شده است.",
  "invitation.email.pending": "برای این نشانی ایمیل یک دعوت در حال پردازش وجود دارد.",
  "invitation.cleanup.pending": "پاک‌سازی حساب ورود قبلی هنوز در حال انجام است؛ پس از تکمیل دوباره تلاش کنید.",
  "tenant.last_administrator.required": "آخرین مدیر فعال سازمان را نمی‌توان غیرفعال یا تنزل نقش داد.",
  "user.self_lockout.denied": "برای جلوگیری از قطع دسترسی، نمی‌توانید حساب خودتان را غیرفعال کنید.",
  "membership.project.not_found": "پروژه انتخاب‌شده در سازمان شما پیدا نشد.",
  "project.not_operational": "پروژه برای ثبت عملیات فعال نیست؛ ابتدا وضعیت پروژه را بررسی کنید.",
  "project.activate.contract_model.required": "پیش از فعال‌سازی باید مدل قراردادی پایه پروژه مشخص باشد.",
  "project.activate.location_root.required": "پیش از فعال‌سازی باید محل اصلی پروژه موجود و فعال باشد.",
  "project.location.required": "انتخاب یک محل معتبر از ساختار مکانی پروژه الزامی است.",
  "project.location.not_active": "محل انتخاب‌شده در این پروژه فعال نیست؛ فهرست محل‌ها را تازه کنید.",
  "project.location.parent.not_active": "محل بالادست انتخاب‌شده فعال نیست.",
  "project.location.active_children": "این محل زیرمجموعهٔ فعال دارد و فعلاً قابل غیرفعال‌کردن نیست.",
  "project.location.root.retire.denied": "محل اصلی پروژه قابل غیرفعال‌کردن نیست.",
  "invitation.project.not_found": "یکی از پروژه‌های انتخاب‌شده در سازمان شما پیدا نشد.",
  "measurement_item.inactive": "قلم اندازه‌گیری غیرفعال است و برای ثبت جدید قابل استفاده نیست.",
  "measurement_item.unit.mismatch": "واحد واقعیت با واحد قلم اندازه‌گیری یکسان نیست.",
  "planning.baseline.mode_mismatch": "نوع مبنا با حالت فعلی برنامه‌ریزی پروژه سازگار نیست.",
  "planning.baseline.weights.invalid": "وزن همه ردیف‌های پیشرفت باید مشخص و جمع آن دقیقاً ۱۰۰٪ باشد.",
  "planning.baseline.measurement_mapping.invalid": "یکی از قلم‌های اندازه‌گیری به این پروژه تعلق ندارد.",
  "planning.baseline.measurement_basis.not_ready": "همه قلم‌های مقدارمحور باید فعال و دارای مقدار هدف باشند.",
  "planning.baseline.external_source.required": "نام سامانه منبع و مرجع نسخه برنامه بیرونی الزامی است.",
  "planning.milestone_update.status_date.regression": "تاریخ این وضعیت از آخرین وضعیت تأییدشده قدیمی‌تر است.",
  "planning.milestone_update.baseline_not_approved": "ثبت رسمی نقطه عطف فقط روی مبنای مصوب مجاز است.",
  "supply.receipt.excess.requires_approval": "مقدار دریافتی از سفارش بیشتر است؛ دلیل و مجوز اضافه‌تحویل لازم است.",
  "supply.receipt.order.not_open": "سفارش انتخاب‌شده باز و قابل دریافت نیست.",
  "supply.receipt.item.order_mismatch": "قلم یا مبنای مقداری دریافت با سفارش سازگار نیست.",
  "supply.receipt.stock_basis.invalid": "قلم موجودی‌پذیر به محل نگهداری نیاز دارد و قلم غیرانبارشونده نباید وارد موجودی شود.",
  "supply.item.tracking.unsupported": "رهگیری بچ یا سریال در این نسخه فعال نیست؛ فعلاً رهگیری مقداری را انتخاب کنید.",
  "supply.inspection.balance.invalid": "جمع مقدار پذیرفته، مردود و قرنطینه باید دقیقاً برابر مقدار دریافت‌شده باشد.",
  "supply.inventory.insufficient": "موجودی قابل مصرف این قلم در محل انتخاب‌شده کافی نیست.",
  "supply.issue.reconcile.balance.invalid": "مقدار تسویه از مانده امانی بیشتر است.",
  "supply.issue.waste.evidence.required": "ثبت پرت به علت و مدرک نیاز دارد.",
  "supply.count.variance.required": "مقدار شمارش‌شده با دفتر موجودی برابر است و اصلاحی لازم نیست.",
  "supply.adjustment.would_make_negative": "ثبت این اصلاح موجودی را منفی می‌کند و مجاز نیست.",
  "supply.service.quantity.exceeds_order": "کارکرد ثبت‌شده از مقدار سفارش خدمت بیشتر می‌شود.",
  "quality.not_enabled": "کنترل کیفیت برای این پروژه فعال نشده است.",
  "hse.not_enabled": "کنترل ایمنی، بهداشت و محیط‌زیست برای این پروژه فعال نشده است.",
  "quality.suspended": "کنترل کیفیت این پروژه تعلیق شده است.",
  "hse.suspended": "کنترل ایمنی، بهداشت و محیط‌زیست این پروژه تعلیق شده است.",
  "quality_safety.setup_required": "پیش از ثبت رسمی، مسئول، ماتریس و قواعد بستن پرونده را پیکربندی کنید.",
  "quality_safety.configuration.project_capability_mismatch": "حالت انتخاب‌شده با قابلیت‌های فعال پروژه سازگار نیست.",
  "quality_safety.configuration.matrix.invalid": "ماتریس انتخاب‌شده نسخه معتبر همین پروژه و حوزه نیست.",
  "quality_safety.intake.convert.area_mismatch": "نوع تبدیل با حوزه ثبت اولیه سازگار نیست.",
  "quality_safety.inspection.pass.not_ready": "بازرسی آماده‌نشده نمی‌تواند نتیجه قبول دریافت کند.",
  "quality_safety.ncr.concession.approver.required": "پذیرش مشروط عدم انطباق به تأییدکننده مجاز نیاز دارد.",
  "quality_safety.ncr.closure_evidence.required": "بستن عدم انطباق به مدرک راستی‌آزمایی یا صرف‌نظر رسمی و ممیزی‌شده نیاز دارد.",
  "quality_safety.incident.final_severity.required": "بازبینی نهایی حادثه به شدت نهاییِ جداگانه و تأییدشده نیاز دارد.",
  "quality_safety.action.source.invalid": "منبع رسمی اقدام اصلاحی در همین پروژه و حوزه پیدا نشد.",
  "quality_safety.permit.activation.outside_window": "مجوز کار فقط داخل بازه اعتبار مصوب قابل فعال‌سازی است.",
  "governance.owner.not_assignable": "مالک انتخاب‌شده در این پروژه فعال یا قابل تخصیص نیست.",
  "governance.sla.recipient.not_assignable": "گیرنده اعلام هشدار در این پروژه فعال یا قابل تخصیص نیست.",
  "governance.decision_request.authority.not_assignable": "مرجع تصمیم در این پروژه فعال یا قابل تخصیص نیست.",
  "governance.decision_request.authority.not_authorized": "مرجع انتخاب‌شده مجوز ثبت تصمیم رسمی در این پروژه را ندارد.",
  "governance.risk_matrix.not_found": "نسخه ماتریس ریسک در همین پروژه پیدا نشد.",
  "governance.risk.review_date.in_past": "تاریخ بازبینی ریسک نمی‌تواند در گذشته باشد.",
  "governance.issue.target.in_past": "تاریخ هدف رفع مسئله نمی‌تواند در گذشته باشد.",
  "governance.issue.verify_before_close": "مسئله باید ابتدا رفع و سپس با مدرک مستقل راستی‌آزمایی و بسته شود.",
  "governance.issue.independent_verifier.required": "فردی که رفع مسئله را ثبت کرده است نمی‌تواند همان مسئله را راستی‌آزمایی و ببندد.",
  "governance.issue.resolution.required": "شرح نتیجه رفع مسئله الزامی است.",
  "governance.decision_request.required_by.in_past": "موعد درخواست تصمیم نمی‌تواند در گذشته باشد.",
  "governance.decision_request.options.insufficient": "برای ارسال درخواست تصمیم، دست‌کم دو گزینه صریح لازم است.",
  "governance.decision.option.not_offered": "تصمیم فقط می‌تواند یکی از گزینه‌های رسمی درخواست باشد.",
  "governance.decision.not_current": "این تصمیم با نسخه جدیدتری جایگزین شده و دیگر رکورد جاری نیست.",
};

const codeSuffixMessages: readonly [suffix: string, message: string][] = [
  ["revision.conflict", "اطلاعات هم‌زمان تغییر کرده است؛ صفحه را تازه کنید و دوباره تلاش کنید."],
  ["not_found_or_not_approved", "اطلاعات تأییدشده موردنظر پیدا نشد."],
  ["not_found", "اطلاعات موردنظر پیدا نشد."],
  ["not_approved", "این مورد هنوز تأیید نشده است."],
  ["not_active", "این مورد در حال حاضر فعال نیست."],
  ["invalid_state", "این عملیات در وضعیت فعلی مجاز نیست."],
  ["duplicate", "یک رکورد با همین مشخصات قبلاً ثبت شده است."],
  ["required", "یکی از اطلاعات الزامی وارد نشده است."],
  ["too_long", "طول متن واردشده بیشتر از حد مجاز است."],
  ["unsupported", "این عملیات یا نوع داده پشتیبانی نمی‌شود."],
  ["mismatch", "اطلاعات انتخاب‌شده با یکدیگر سازگار نیستند."],
  ["invalid", "یکی از مقادیر واردشده معتبر نیست."],
];

export async function ensureApiSuccess(response: Response): Promise<void> {
  if (response.ok) return;

  let problem: ApiProblem | null = null;
  try {
    problem = await response.json() as ApiProblem;
  } catch {
    // پاسخ غیرساختاریافته نیز به یک پیام امن و فارسی تبدیل می‌شود.
  }

  throw new ApiRequestError(
    apiProblemMessage(problem, response.status),
    response.status,
    problem?.code,
    parseRetryAfterSeconds(response.headers.get("Retry-After")),
  );
}

export function apiProblemMessage(problem: ApiProblem | null, status?: number): string {
  const code = problem?.code?.trim().toLowerCase();
  if (code) {
    const exact = exactCodeMessages[code];
    if (exact) return exact;

    const suffix = codeSuffixMessages.find(([candidate]) => code.endsWith(candidate));
    if (suffix) return suffix[1];
  }

  const suppliedMessage = problem?.detail ?? problem?.message ?? problem?.title;
  if (suppliedMessage && containsPersian(suppliedMessage)) {
    return suppliedMessage;
  }

  if (status !== undefined) {
    if (status >= 500) return "در حال حاضر مشکلی در سرور رخ داده است؛ کمی بعد دوباره تلاش کنید.";
    const knownStatus = statusMessages[status];
    if (knownStatus) return knownStatus;
  }

  return "ارتباط با سرور ناموفق بود؛ دوباره تلاش کنید.";
}

export function toUserMessage(error: unknown, fallback: string): string {
  if (!(error instanceof Error)) return fallback;
  const message = error.message.trim();
  if (!message) return fallback;
  if (containsPersian(message)) return message;
  if (/^[a-z][a-z0-9_.-]+$/i.test(message)) {
    return apiProblemMessage({ code: message });
  }
  return fallback;
}

export function currencyLabel(currencyCode?: string | null): string {
  const normalized = currencyCode?.trim().toUpperCase();
  if (!normalized) return "ارز پروژه";

  const known: Readonly<Record<string, string>> = {
    IRR: "ریال",
    USD: "دلار آمریکا",
    EUR: "یورو",
    AED: "درهم امارات",
    AZN: "منات آذربایجان",
  };
  if (known[normalized]) return known[normalized];

  try {
    return new Intl.DisplayNames(["fa"], { type: "currency" }).of(normalized) ?? "ارز پروژه";
  } catch {
    return "ارز پروژه";
  }
}

export function formatAmountFa(value?: number | null, currencyCode?: string | null): string {
  if (value === null || value === undefined) return "—";
  return `${value.toLocaleString("fa-IR", { maximumFractionDigits: 2 })} ${currencyLabel(currencyCode)}`;
}

function containsPersian(value: string): boolean {
  return /[\u0600-\u06ff]/u.test(value);
}

function parseRetryAfterSeconds(value: string | null): number | undefined {
  if (!value) return undefined;
  const seconds = Number.parseInt(value, 10);
  if (Number.isInteger(seconds) && seconds >= 0) return seconds;
  const timestamp = Date.parse(value);
  if (!Number.isFinite(timestamp)) return undefined;
  return Math.max(0, Math.ceil((timestamp - Date.now()) / 1000));
}
