"use client";

import { useId, useRef, useState, type KeyboardEvent, type MouseEvent, type ReactNode } from "react";

interface SidebarNavigationProps {
  readonly children: ReactNode;
  readonly label: string;
}

export function SidebarNavigation({ children, label }: SidebarNavigationProps) {
  const [open, setOpen] = useState(false);
  const id = useId();
  const toggle = useRef<HTMLButtonElement>(null);

  function closeOnLink(event: MouseEvent<HTMLElement>) {
    if (event.target instanceof Element && event.target.closest("a")) setOpen(false);
  }

  function closeOnEscape(event: KeyboardEvent<HTMLElement>) {
    if (event.key !== "Escape" || !open) return;
    event.preventDefault();
    setOpen(false);
    toggle.current?.focus();
  }

  return <>
    <nav className="sidebar-desktop-navigation" aria-label={label}>{children}</nav>
    <div className="sidebar-mobile-navigation" onKeyDown={closeOnEscape}>
      <button ref={toggle} type="button" aria-expanded={open} aria-controls={id}
        onClick={() => setOpen(current => !current)}>
        {open ? "بستن فهرست بخش‌ها" : "باز کردن فهرست بخش‌ها"}
      </button>
      <nav id={id} aria-label={label} hidden={!open} onClick={closeOnLink}>{children}</nav>
    </div>
  </>;
}
