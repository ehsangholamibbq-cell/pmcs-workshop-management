import type { MembershipStatus, UserStatus } from "./identity-administration.ts";

export function projectRoleLabel(role: string): string {
  return ({ ProjectManager: "مدیر پروژه", ProjectController: "کارشناس کنترل پروژه", SiteSupervisor: "سرپرست کارگاه", Observer: "مشاهده‌گر", TechnicalOffice: "دفتر فنی", FinanceOperator: "کارشناس مالی", FinanceManager: "مدیر مالی", ContractAdministrator: "مدیر قرارداد", ProcurementOperator: "کارشناس خرید", ProcurementManager: "مدیر خرید", QualityController: "مسئول کنترل کیفیت", HseOfficer: "مسئول ایمنی، بهداشت و محیط‌زیست" } as Record<string, string>)[role] ?? "نقش پروژه";
}

export function userStatusLabel(status: UserStatus): string {
  return ({ Invited: "دعوت‌شده", Active: "فعال", Suspended: "تعلیق‌شده", Deactivated: "غیرفعال" })[status];
}

export function membershipStatusLabel(status: MembershipStatus): string {
  return ({ Proposed: "پیشنهادی", Active: "فعال", Suspended: "تعلیق‌شده", Expired: "منقضی‌شده", Revoked: "لغوشده" })[status];
}
