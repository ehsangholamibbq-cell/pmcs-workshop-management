import { ProjectBootstrapWizard } from "@/components/project-bootstrap-wizard";
import { PmcsSessionBoundary } from "@/components/pmcs-session";

export const metadata = {
  title: "ساخت از روی پروژهٔ موجود | سامانه کنترل مدیریت پروژه",
};

export default function ProjectBootstrapPage() {
  return <PmcsSessionBoundary><ProjectBootstrapWizard /></PmcsSessionBoundary>;
}
