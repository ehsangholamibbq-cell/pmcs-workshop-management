import { notFound } from "next/navigation";
import { ProjectCollaboration } from "@/components/project-collaboration";

export const metadata = { title: "گفت‌وگوی پروژه | PMCS" };

interface ProjectCollaborationPageProps {
  readonly params: Promise<{ projectId: string }>;
}

export default async function ProjectCollaborationPage({ params }: ProjectCollaborationPageProps) {
  const { projectId } = await params;
  if (!/^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/iu.test(projectId)) notFound();
  return <ProjectCollaboration projectId={projectId} />;
}
