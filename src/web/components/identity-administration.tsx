"use client";

import Link from "next/link";
import { type FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import {
  changeTenantRole,
  changeUserStatus,
  getIdentityDirectory,
  inviteUser,
  resendInvitation,
  revokeInvitation,
  revokeMembership,
  upsertMembership,
  type IdentityDirectoryModel,
  type InvitationStatus,
  type MembershipStatus,
  type TenantRole,
  type UserDirectoryModel,
  type UserStatus,
} from "@/lib/identity-administration";
import { toUserMessage } from "@/lib/localization";
import { formatPersianDateTime } from "@/lib/persian-date";
import { listProjects, type ProjectModel } from "@/lib/projects";
import { PmcsSessionBoundary, SessionBadge, usePmcsSession } from "@/components/pmcs-session";

const apiBaseUrl = "/api/pmcs";

export function IdentityAdministration() {
  return (
    <PmcsSessionBoundary>
      <IdentityAdministrationContent />
    </PmcsSessionBoundary>
  );
}

function IdentityAdministrationContent() {
  const session = usePmcsSession();
  const [directory, setDirectory] = useState<IdentityDirectoryModel | null>(null);
  const [projects, setProjects] = useState<readonly ProjectModel[]>([]);
  const [message, setMessage] = useState("در حال دریافت فهرست کاربران و دعوت‌ها…");
  const [reloadToken, setReloadToken] = useState(0);
  const [busyKey, setBusyKey] = useState("");

  const reload = useCallback(() => setReloadToken((value) => value + 1), []);

  useEffect(() => {
    if (session.tenantRole !== "TenantAdministrator") return;
    let active = true;
    void Promise.all([
      getIdentityDirectory(apiBaseUrl),
      listProjects(apiBaseUrl, { tenantId: session.tenantId, userId: session.userId }),
    ]).then(([loadedDirectory, loadedProjects]) => {
      if (!active) return;
      setDirectory(loadedDirectory);
      setProjects(loadedProjects);
      setMessage("فهرست کاربران و دامنه دسترسی‌ها از مرجع رسمی دریافت شد.");
    }).catch((error: unknown) => {
      if (!active) return;
      setMessage(toUserMessage(error, "دریافت اطلاعات مدیریت کاربران انجام نشد."));
    });
    return () => {
      active = false;
    };
  }, [reloadToken, session.tenantId, session.tenantRole, session.userId]);

  async function runAction(key: string, action: () => Promise<void>, success: string) {
    setBusyKey(key);
    setMessage("در حال ثبت و ممیزی تغییر…");
    try {
      await action();
      setMessage(success);
      reload();
    } catch (error) {
      setMessage(toUserMessage(error, "ثبت تغییر انجام نشد."));
    } finally {
      setBusyKey("");
    }
  }

  if (session.tenantRole !== "TenantAdministrator") {
    return (
      <main className="auth-state">
        <section className="auth-state-card">
          <span className="auth-state-mark">پ</span>
          <h1>دسترسی مدیریت کاربران</h1>
          <p>این بخش فقط برای مدیر فعال سازمان در دسترس است.</p>
          <Link className="secondary-button" href="/">بازگشت به مرکز فرمان</Link>
        </section>
      </main>
    );
  }

  return (
    <main className="app-shell identity-shell">
      <aside className="sidebar" aria-label="ناوبری اصلی">
        <div className="brand-mark" aria-label="سامانه کنترل مدیریت پروژه"><span>پ</span></div>
        <nav>
          <Link className="nav-item" href="/portfolio">سبد پروژه‌ها</Link>
          <Link className="nav-item" href="/">مرکز فرمان پروژه</Link>
          <Link className="nav-item active" href="/admin/users">کاربران و دسترسی‌ها</Link>
        </nav>
        <SessionBadge />
      </aside>

      <section className="workspace identity-workspace">
        <header className="topbar">
          <div>
            <p className="eyebrow">مدیریت هویت سازمان</p>
            <h1>کاربران، دعوت‌ها و عضویت پروژه</h1>
            <p className="identity-lead">حساب ورود و مجوز داخلی دو مرز مستقل‌اند؛ دعوت موفق بدون عضویت، دسترسی پروژه ایجاد نمی‌کند.</p>
          </div>
          <button className="secondary-button" type="button" disabled={Boolean(busyKey)} onClick={reload}>تازه‌سازی</button>
        </header>

        <p className="portfolio-system-message" aria-live="polite">{message}</p>

        {directory && !directory.provisioningEnabled && (
          <div className="identity-warning" role="alert">
            اتصال مدیریتی سامانه ورود فعال نیست؛ مشاهده کاربران ممکن است اما دعوت جدید پردازش نمی‌شود.
          </div>
        )}

        {directory && (
          <>
            <InviteUserForm
              disabled={!directory.provisioningEnabled || Boolean(busyKey)}
              projects={projects}
              projectRoles={directory.projectRoles}
              onSubmit={(input) => runAction(
                "invite",
                async () => { await inviteUser(apiBaseUrl, input); },
                "دعوت در صف پایدار ثبت شد؛ وضعیت ارسال به‌صورت خودکار به‌روزرسانی می‌شود.",
              )}
            />

            <section className="identity-section" aria-labelledby="pending-invitations-title">
              <div className="section-title">
                <div>
                  <p className="eyebrow">چرخه دعوت</p>
                  <h2 id="pending-invitations-title">دعوت‌های اخیر</h2>
                </div>
                <span className="section-note">{directory.invitations.length.toLocaleString("fa-IR")} مورد</span>
              </div>
              {directory.invitations.length === 0 ? (
                <p className="empty-state">هنوز دعوتی ثبت نشده است.</p>
              ) : (
                <div className="identity-card-grid">
                  {directory.invitations.map((invitation) => (
                    <article className="identity-card" key={invitation.id}>
                      <div className="identity-card-heading">
                        <div><h3>{invitation.displayName}</h3><span dir="ltr">{invitation.email}</span></div>
                        <span className={`identity-status status-${invitation.status.toLowerCase()}`}>{invitationStatusLabel(invitation.status)}</span>
                      </div>
                      <dl className="identity-details">
                        <div><dt>نقش سازمانی</dt><dd>{tenantRoleLabel(invitation.tenantRole)}</dd></div>
                        <div><dt>تعداد تلاش</dt><dd>{invitation.attempts.toLocaleString("fa-IR")}</dd></div>
                        <div><dt>اعتبار تا</dt><dd>{formatDateTime(invitation.expiresAt)}</dd></div>
                        <div><dt>پروژه‌ها</dt><dd>{invitation.projects.length.toLocaleString("fa-IR")}</dd></div>
                      </dl>
                      {invitation.lastErrorCode && <p className="identity-error">آخرین تلاش کامل نشد؛ ارسال مجدد یا تلاش خودکار در دسترس است.</p>}
                      <div className="identity-actions">
                        {invitation.status !== "Revoked" && (
                          <button type="button" disabled={Boolean(busyKey)} onClick={() => void runAction(
                            `resend-${invitation.id}`,
                            () => resendInvitation(apiBaseUrl, invitation.id),
                            "دعوت برای ارسال مجدد در صف قرار گرفت.",
                          )}>ارسال دوباره</button>
                        )}
                        {!(["Sent", "Revoked"] as InvitationStatus[]).includes(invitation.status) && (
                          <button className="danger-button" type="button" disabled={Boolean(busyKey)} onClick={() => void runAction(
                            `revoke-${invitation.id}`,
                            () => revokeInvitation(apiBaseUrl, invitation.id),
                            "دعوت لغو شد.",
                          )}>لغو دعوت</button>
                        )}
                      </div>
                    </article>
                  ))}
                </div>
              )}
            </section>

            <section className="identity-section" aria-labelledby="users-title">
              <div className="section-title">
                <div>
                  <p className="eyebrow">مرجع مجوز داخلی</p>
                  <h2 id="users-title">حساب‌های سازمان</h2>
                </div>
                <span className="section-note">{directory.users.length.toLocaleString("fa-IR")} حساب</span>
              </div>
              <div className="identity-user-list">
                {directory.users.map((user) => (
                  <UserCard
                    key={user.id}
                    user={user}
                    currentUserId={session.userId}
                    projects={projects}
                    projectRoles={directory.projectRoles}
                    disabled={Boolean(busyKey)}
                    onAction={runAction}
                  />
                ))}
              </div>
            </section>
          </>
        )}
      </section>
    </main>
  );
}

function InviteUserForm({ disabled, projects, projectRoles, onSubmit }: {
  readonly disabled: boolean;
  readonly projects: readonly ProjectModel[];
  readonly projectRoles: readonly string[];
  readonly onSubmit: (input: {
    displayName: string;
    email: string;
    tenantRole: TenantRole;
    projects: readonly { projectId: string; roleCode: string }[];
  }) => Promise<void>;
}) {
  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [tenantRole, setTenantRole] = useState<TenantRole>("Member");
  const [projectId, setProjectId] = useState("");
  const [projectRole, setProjectRole] = useState("Observer");

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await onSubmit({
      displayName,
      email,
      tenantRole,
      projects: projectId ? [{ projectId, roleCode: projectRole }] : [],
    });
    setDisplayName("");
    setEmail("");
    setProjectId("");
  }

  return (
    <section className="identity-section invite-panel" aria-labelledby="invite-title">
      <div className="section-title">
        <div><p className="eyebrow">دعوت کنترل‌شده</p><h2 id="invite-title">افزودن کاربر</h2></div>
        <span className="section-note">بدون ثبت‌نام عمومی</span>
      </div>
      <form className="identity-form" onSubmit={(event) => void submit(event)}>
        <label><span>نام و نام خانوادگی</span><input required maxLength={200} value={displayName} onChange={(event) => setDisplayName(event.target.value)} /></label>
        <label><span>نشانی ایمیل</span><input dir="ltr" type="email" required maxLength={320} value={email} onChange={(event) => setEmail(event.target.value)} /></label>
        <label><span>نقش سازمانی</span><select value={tenantRole} onChange={(event) => setTenantRole(event.target.value as TenantRole)}>
          <option value="Member">عضو سازمان</option><option value="PortfolioViewer">مشاهده‌گر سبد پروژه‌ها</option><option value="TenantAdministrator">مدیر سازمان</option>
        </select></label>
        <label><span>پروژه اولیه، اختیاری</span><select value={projectId} onChange={(event) => setProjectId(event.target.value)}>
          <option value="">بدون عضویت اولیه</option>{projects.map((project) => <option key={project.id} value={project.id}>{project.name} · {project.code}</option>)}
        </select></label>
        <label><span>نقش در پروژه</span><select disabled={!projectId} value={projectRole} onChange={(event) => setProjectRole(event.target.value)}>
          {projectRoles.map((role) => <option key={role} value={role}>{projectRoleLabel(role)}</option>)}
        </select></label>
        <button className="primary-button" type="submit" disabled={disabled}>ثبت و ارسال دعوت امن</button>
      </form>
    </section>
  );
}

function UserCard({ user, currentUserId, projects, projectRoles, disabled, onAction }: {
  readonly user: UserDirectoryModel;
  readonly currentUserId: string;
  readonly projects: readonly ProjectModel[];
  readonly projectRoles: readonly string[];
  readonly disabled: boolean;
  readonly onAction: (key: string, action: () => Promise<void>, success: string) => Promise<void>;
}) {
  const [projectId, setProjectId] = useState("");
  const [projectRole, setProjectRole] = useState("Observer");
  const projectNames = useMemo(() => new Map(projects.map((project) => [project.id, project.name])), [projects]);

  return (
    <article className="identity-user-card">
      <div className="identity-card-heading">
        <div><h3>{user.displayName}{user.id === currentUserId ? " · حساب شما" : ""}</h3><span dir="ltr">{user.email}</span></div>
        <div className="identity-user-state"><span className={`identity-status status-${user.status.toLowerCase()}`}>{userStatusLabel(user.status)}</span>{user.providerSyncPending && <small>همگام‌سازی ورود در صف است</small>}</div>
      </div>
      <div className="identity-account-controls">
        <label><span>نقش سازمانی</span><select value={user.tenantRole} disabled={disabled} onChange={(event) => void onAction(
          `tenant-role-${user.id}`,
          () => changeTenantRole(apiBaseUrl, user.id, event.target.value as TenantRole),
          "نقش سازمانی با ثبت ممیزی تغییر کرد.",
        )}><option value="Member">عضو سازمان</option><option value="PortfolioViewer">مشاهده‌گر سبد پروژه‌ها</option><option value="TenantAdministrator">مدیر سازمان</option></select></label>
        <div className="identity-actions">
          {user.status !== "Active" && user.status !== "Deactivated" && <button type="button" disabled={disabled} onClick={() => void onAction(`activate-${user.id}`, () => changeUserStatus(apiBaseUrl, user.id, "Active"), "حساب داخلی فعال و همگام‌سازی سامانه ورود در صف قرار گرفت.")}>فعال‌سازی</button>}
          {user.status === "Active" && user.id !== currentUserId && <button type="button" disabled={disabled} onClick={() => void onAction(`suspend-${user.id}`, () => changeUserStatus(apiBaseUrl, user.id, "Suspended"), "دسترسی داخلی فوراً تعلیق و خروج نشست‌ها در صف قرار گرفت.")}>تعلیق و خروج نشست‌ها</button>}
          {user.status !== "Deactivated" && user.id !== currentUserId && <button className="danger-button" type="button" disabled={disabled} onClick={() => void onAction(`deactivate-${user.id}`, () => changeUserStatus(apiBaseUrl, user.id, "Deactivated"), "حساب غیرفعال شد و خروج نشست‌ها در صف قرار گرفت.")}>غیرفعال‌سازی نهایی</button>}
        </div>
      </div>

      <div className="membership-list">
        <h4>عضویت‌های پروژه</h4>
        {user.memberships.filter((item) => item.status === "Active").length === 0 ? <p>عضویت فعالی ثبت نشده است.</p> : user.memberships.filter((item) => item.status === "Active").map((membership) => (
          <div className="membership-row" key={membership.id}>
            <span>{projectNames.get(membership.projectId) ?? "پروژه ثبت‌شده"}</span>
            <strong>{projectRoleLabel(membership.roleCode)}</strong>
            <button type="button" disabled={disabled} onClick={() => void onAction(`membership-revoke-${membership.id}`, () => revokeMembership(apiBaseUrl, user.id, membership.projectId), "عضویت پروژه لغو شد.")}>لغو عضویت</button>
          </div>
        ))}
      </div>

      {user.status !== "Deactivated" && (
        <div className="membership-editor">
          <label><span>پروژه</span><select value={projectId} onChange={(event) => setProjectId(event.target.value)}><option value="">انتخاب پروژه</option>{projects.map((project) => <option key={project.id} value={project.id}>{project.name}</option>)}</select></label>
          <label><span>نقش پروژه</span><select value={projectRole} onChange={(event) => setProjectRole(event.target.value)}>{projectRoles.map((role) => <option key={role} value={role}>{projectRoleLabel(role)}</option>)}</select></label>
          <button type="button" disabled={disabled || !projectId} onClick={() => void onAction(`membership-${user.id}`, () => upsertMembership(apiBaseUrl, user.id, projectId, projectRole), "عضویت و نقش پروژه ثبت شد.")}>ثبت عضویت</button>
        </div>
      )}
    </article>
  );
}

function tenantRoleLabel(role: TenantRole): string {
  return ({ Member: "عضو سازمان", PortfolioViewer: "مشاهده‌گر سبد پروژه‌ها", TenantAdministrator: "مدیر سازمان" })[role];
}

function projectRoleLabel(role: string): string {
  return ({ ProjectManager: "مدیر پروژه", ProjectController: "کارشناس کنترل پروژه", SiteSupervisor: "سرپرست کارگاه", Observer: "مشاهده‌گر", TechnicalOffice: "دفتر فنی", FinanceOperator: "کارشناس مالی", FinanceManager: "مدیر مالی", ContractAdministrator: "مدیر قرارداد", ProcurementOperator: "کارشناس خرید", ProcurementManager: "مدیر خرید", QualityController: "مسئول کنترل کیفیت", HseOfficer: "مسئول ایمنی، بهداشت و محیط‌زیست" } as Record<string, string>)[role] ?? "نقش پروژه";
}

function invitationStatusLabel(status: InvitationStatus): string {
  return ({ Queued: "در صف", Processing: "در حال پردازش", RetryScheduled: "تلاش مجدد زمان‌بندی‌شده", Sent: "ارسال‌شده", Failed: "ناموفق", Revoked: "لغوشده", Expired: "منقضی‌شده" })[status];
}

function userStatusLabel(status: UserStatus): string {
  return ({ Invited: "دعوت‌شده", Active: "فعال", Suspended: "تعلیق‌شده", Deactivated: "غیرفعال" })[status];
}

export function membershipStatusLabel(status: MembershipStatus): string {
  return ({ Proposed: "پیشنهادی", Active: "فعال", Suspended: "تعلیق‌شده", Expired: "منقضی‌شده", Revoked: "لغوشده" })[status];
}

function formatDateTime(value: string): string {
  return formatPersianDateTime(value);
}
