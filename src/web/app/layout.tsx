import type { Metadata, Viewport } from "next";
import type { ReactNode } from "react";
import { ServiceWorkerRegistration } from "@/components/service-worker-registration";
import "./globals.css";
import "./typography.generated.css";

export const metadata: Metadata = {
  title: "سامانه کنترل مدیریت پروژه | مرکز فرمان پروژه",
  description: "سامانه کنترل مدیریتی پروژه‌های عمرانی",
  applicationName: "سامانه کنترل مدیریت پروژه",
};

export const viewport: Viewport = {
  themeColor: "#0c2036",
  colorScheme: "light",
};

export default function RootLayout({ children }: Readonly<{ children: ReactNode }>) {
  return (
    <html lang="fa" dir="rtl">
      <body>
        <ServiceWorkerRegistration />
        {children}
      </body>
    </html>
  );
}
