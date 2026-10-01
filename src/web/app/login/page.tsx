import { LoginPanel } from "@/components/login-panel";
import { loadLoginExperience } from "@/lib/login-experience-server";

export const metadata = {
  title: "ورود امن | سامانه کنترل مدیریت پروژه",
};

interface LoginPageProps {
  readonly searchParams: Promise<{ error?: string | string[] }>;
}

export default async function LoginPage({ searchParams }: LoginPageProps) {
  const [{ error }, experience] = await Promise.all([searchParams, loadLoginExperience()]);
  return <LoginPanel experience={experience} initialError={Boolean(error)} />;
}
