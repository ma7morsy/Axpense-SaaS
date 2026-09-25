import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Bell, ChevronDown, LogOut, Menu, Search, User as UserIcon } from "lucide-react";
import { useAuth } from "../../contexts/AuthContext";
import { cn } from "../../lib/utils";

export function Topbar({ unreadCount, onOpenMobile }: { unreadCount: number; onOpenMobile: () => void }) {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function onClick(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) setMenuOpen(false);
    }
    document.addEventListener("mousedown", onClick);
    return () => document.removeEventListener("mousedown", onClick);
  }, []);

  return (
    <header className="topbar">
      <button className="icon-btn burger" onClick={onOpenMobile} aria-label="Open menu">
        <Menu />
      </button>

      <div className="search">
        <Search />
        <input placeholder="Search vehicles, drivers, maintenance…" aria-label="Search" />
      </div>

      <div className="topbar-r">
        <button className="icon-btn" onClick={() => navigate("/notifications")} aria-label="Alerts">
          <Bell />
          {unreadCount > 0 && <span className="badge-n">{unreadCount}</span>}
        </button>
        <span className="vr" />
        <div className="user-wrap" ref={menuRef}>
          <button className="user" onClick={() => setMenuOpen((v) => !v)} aria-haspopup="menu">
            <span className="avatar">
              <UserIcon size={22} />
            </span>
            <span className="who">
              <span className="nm">
                {user?.firstName} {user?.lastName}
              </span>
              <span className="rl">{user?.role}</span>
            </span>
            <ChevronDown className="cv" />
          </button>

          <div className={cn("umenu", menuOpen && "open")} role="menu">
            <div className="dm-head">
              <span className="avatar">
                <UserIcon size={22} />
              </span>
              <div>
                <b>
                  {user?.firstName} {user?.lastName}
                </b>
                <small>{user?.email}</small>
              </div>
            </div>
            <button
              className="dm-i"
              onClick={() => {
                setMenuOpen(false);
                navigate("/administration");
              }}
            >
              <UserIcon /> <span>Account</span>
            </button>
            <div className="dm-sep" />
            <button
              className="dm-i danger"
              onClick={() => {
                setMenuOpen(false);
                logout();
              }}
            >
              <LogOut /> <span>Sign out</span>
            </button>
          </div>
        </div>
      </div>
    </header>
  );
}
