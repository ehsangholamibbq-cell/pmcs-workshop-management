import type { Metadata } from "next";
import { ProjectReportingCenter } from "@/components/project-reporting-center";
import "./reporting-layout.css";

export const metadata: Metadata = { title: "مرکز گزارش‌های پروژه | پی‌ام‌سی‌اس" };

export default async function ProjectReportsPage({ params }: {
  readonly params: Promise<{ projectId: string }>;
}) {
  const { projectId } = await params;
  return <ProjectReportingCenter projectId={projectId} />;
}
