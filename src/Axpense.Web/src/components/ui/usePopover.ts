import { useCallback, useEffect, useRef, useState } from "react";

/**
 * Open/close state + fixed positioning for a popover anchored to a trigger.
 * The console body is zoomed (--z), so coordinates are converted the same way the
 * reference UI does. Closes on outside click, scroll, resize and Escape.
 */
export function usePopover(align: "start" | "end" = "end") {
  const [open, setOpen] = useState(false);
  const [pos, setPos] = useState<{ top: number; left: number }>({ top: 0, left: 0 });
  const wrapRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const popRef = useRef<HTMLDivElement>(null);

  const place = useCallback(() => {
    const t = triggerRef.current;
    const p = popRef.current;
    if (!t || !p) return;
    const r = t.getBoundingClientRect();
    const z = parseFloat(getComputedStyle(document.body).zoom) || 1;
    const vh = window.innerHeight / z;
    const vw = window.innerWidth / z;
    let top = r.bottom + 6;
    let left = align === "end" ? Math.min(r.right - p.offsetWidth, vw - p.offsetWidth - 12) : Math.min(r.left, vw - p.offsetWidth - 12);
    if (top + p.offsetHeight > vh - 12) top = Math.max(12, r.top - p.offsetHeight - 6);
    if (left < 12) left = 12;
    setPos({ top, left });
  }, [align]);

  useEffect(() => {
    if (!open) return;
    place();
    const close = () => setOpen(false);
    const onDown = (e: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) close();
    };
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && close();
    document.addEventListener("mousedown", onDown);
    document.addEventListener("keydown", onKey);
    window.addEventListener("scroll", close, true);
    window.addEventListener("resize", close);
    return () => {
      document.removeEventListener("mousedown", onDown);
      document.removeEventListener("keydown", onKey);
      window.removeEventListener("scroll", close, true);
      window.removeEventListener("resize", close);
    };
  }, [open, place]);

  return { open, setOpen, pos, wrapRef, triggerRef, popRef };
}
