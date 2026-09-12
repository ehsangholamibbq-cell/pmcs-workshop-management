import { notFound } from "next/navigation";
import { FoundationDashboard } from "@/components/foundation-dashboard";

interface ProjectPageProps {
  readonly params: Promise<{ projectId: string }>;
}

const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/iu;

export default async function ProjectPage({ params }: ProjectPageProps) {
  const { projectId } = await params;
  if (!uuidPattern.test(projectId)) notFound();
  return <FoundationDashboard projectId={projectId} />;
}
