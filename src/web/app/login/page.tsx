import { LoginPanel } from "@/components/login-panel";

export const metadata = {
  title: "ورود امن | سامانه کنترل مدیریت پروژه",
};

interface LoginPageProps {
  readonly searchParams: Promise<{ error?: string | string[] }>;
}

export default async function LoginPage({ searchParams }: LoginPageProps) {
  const { error } = await searchParams;
  return <LoginPanel initialError={Boolean(error)} />;
}
