import type { MetadataRoute } from "next";

export default function manifest(): MetadataRoute.Manifest {
  return {
    name: "سامانه کنترل مدیریت پروژه‌های عمرانی",
    short_name: "کنترل پروژه",
    description: "ثبت واقعیت کارگاه و تبدیل آن به اطلاعات مدیریتی",
    start_url: "/",
    display: "standalone",
    background_color: "#f2f4ef",
    theme_color: "#112a2a",
    lang: "fa",
    dir: "rtl",
    icons: [
      {
        src: "/icon.svg",
        sizes: "any",
        type: "image/svg+xml",
        purpose: "any",
      },
      {
        src: "/icon.svg",
        sizes: "any",
        type: "image/svg+xml",
        purpose: "maskable",
      },
    ],
  };
}
