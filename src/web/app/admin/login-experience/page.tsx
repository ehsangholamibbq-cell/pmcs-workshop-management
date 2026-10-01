import { LoginExperienceAdministration } from "@/components/login-experience-administration";
import { PmcsSessionBoundary } from "@/components/pmcs-session";

export const metadata = {
  title: "ظاهر صفحه ورود | سامانه کنترل مدیریت پروژه",
};

export default function LoginExperienceAdministrationPage() {
  return (
    <PmcsSessionBoundary>
      <LoginExperienceAdministration />
    </PmcsSessionBoundary>
  );
}
