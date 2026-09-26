import { MemberProfileEditor } from "@/components/member-profile";
import { PmcsSessionBoundary } from "@/components/pmcs-session";

export const metadata = {
  title: "پروفایل من | سامانه کنترل مدیریت پروژه",
};

export default function ProfilePage() {
  return (
    <PmcsSessionBoundary>
      <MemberProfileEditor />
    </PmcsSessionBoundary>
  );
}
