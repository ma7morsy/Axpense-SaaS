import { useEffect, useState } from "react";
import { Outlet } from "react-router-dom";
import { Sidebar } from "./Sidebar";
import { Topbar } from "./Topbar";
import { notificationsApi } from "../../lib/api";
import { cn } from "../../lib/utils";

export function AppLayout() {
  const [unreadCount, setUnreadCount] = useState(0);
  const [rail, setRail] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);

  useEffect(() => {
    let cancelled = false;
    async function load() {
      try {
        const items = await notificationsApi.list(true);
        if (!cancelled) setUnreadCount(items.length);
      } catch {
        /* ignore */
      }
    }
    load();
    const interval = setInterval(load, 60_000);
    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, []);

  return (
    <div className={cn("app", rail && "rail")}>
      <Sidebar
        unreadCount={unreadCount}
        rail={rail}
        onToggleRail={() => setRail((v) => !v)}
        mobileOpen={mobileOpen}
        onCloseMobile={() => setMobileOpen(false)}
      />
      <div className="main">
        <Topbar unreadCount={unreadCount} onOpenMobile={() => setMobileOpen(true)} />
        <main className="page">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
