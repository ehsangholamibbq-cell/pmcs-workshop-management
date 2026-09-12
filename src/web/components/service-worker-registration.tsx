"use client";

import { useEffect } from "react";

export function ServiceWorkerRegistration() {
  useEffect(() => {
    if (!("serviceWorker" in navigator)) {
      return;
    }

    const register = async () => {
      try {
        await navigator.serviceWorker.register("/sw.js", { scope: "/" });
      } catch (error) {
        console.warn("ثبت سرویس پس‌زمینه مرورگر ناموفق بود.", error);
      }
    };

    void register();
  }, []);

  return null;
}
