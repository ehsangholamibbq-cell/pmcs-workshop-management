import type { Metadata, Viewport } from "next";
import type { ReactNode } from "react";
import { ServiceWorkerRegistration } from "@/components/service-worker-registration";
import "./globals.css";

export const metadata: Metadata = {
  title: "سامانه کنترل مدیریت پروژه | مرکز فرمان پروژه",
  description: "سامانه کنترل مدیریتی پروژه‌های عمرانی",
  applicationName: "سامانه کنترل مدیریت پروژه",
};

export const viewport: Viewport = {
  themeColor: "#112a2a",
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
