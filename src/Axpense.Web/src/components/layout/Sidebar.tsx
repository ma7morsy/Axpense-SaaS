import { useEffect, useState } from "react";
import { NavLink, useLocation, useNavigate } from "react-router-dom";
import {
  LayoutDashboard,
  Car,
  Map as MapIcon,
  Wrench,
  ShieldCheck,
  Wallet,
  Coins,
  Users,
  BarChart3,
  Settings,
  Building2,
  UserCog,
  SlidersHorizontal,
  ChevronDown,
  PanelLeftClose,
  PanelLeftOpen,
} from "lucide-react";
import { cn } from "../../lib/utils";
import axpenseLogo from "../../assets/axpense-logo.png";
import "./sidebar-group.css";

const NAV = [
  { to: "/", label: "Dashboard", icon: LayoutDashboard, end: true },
  { to: "/vehicles", label: "Vehicles", icon: Car },
  { to: "/aerial", label: "Aerial view", icon: MapIcon },
  { to: "/maintenance", label: "Maintenance", icon: Wrench },
  { to: "/inspections", label: "Inspections", icon: ShieldCheck },
  { to: "/expenses", label: "Expenses", icon: Wallet },
  { to: "/budgets", label: "Budgets", icon: Coins },
  { to: "/drivers", label: "Drivers", icon: Users },
  { to: "/reports", label: "Reports", icon: BarChart3 },
];

const ADMIN = {
  base: "/administration",
  label: "Administration",
  icon: Settings,
  children: [
    { to: "/administration/company", label: "Company", icon: Building2 },
    { to: "/administration/users", label: "Roles & Users", icon: UserCog },
    { to: "/administration/settings", label: "Settings", icon: SlidersHorizontal },
  ],
};

export function Sidebar({
  rail,
  onToggleRail,
  mobileOpen,
  onCloseMobile,
}: {
  /** Unread notifications are shown on the top-bar bell; kept for API compatibility. */
  unreadCount?: number;
  rail: boolean;
  onToggleRail: () => void;
  mobileOpen: boolean;
  onCloseMobile: () => void;
}) {
  return (
    <>
      {mobileOpen && (
        <div
          className="fixed inset-0 z-[89] bg-black/40 lg:hidden"
          onClick={onCloseMobile}
          aria-hidden
        />
      )}
      <aside className={cn("side", mobileOpen && "open")} id="side">
        <svg className="side-art" viewBox="0 0 264 640" preserveAspectRatio="xMidYMax slice" aria-hidden="true">
          <defs>
            <radialGradient id="sk" cx="150" cy="330" r="230" gradientUnits="userSpaceOnUse">
              <stop offset="0" stopColor="#4FB0E6" stopOpacity=".55" />
              <stop offset=".55" stopColor="#2A7DB8" stopOpacity=".18" />
              <stop offset="1" stopColor="#0B4A78" stopOpacity="0" />
            </radialGradient>
            <linearGradient id="mist" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0" stopColor="#5DB7E6" stopOpacity="0" />
              <stop offset=".55" stopColor="#5DB7E6" stopOpacity=".30" />
              <stop offset="1" stopColor="#5DB7E6" stopOpacity="0" />
            </linearGradient>
            <linearGradient id="gnd" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0" stopColor="#0D3B60" />
              <stop offset="1" stopColor="#051B32" />
            </linearGradient>
            <linearGradient id="rd" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0" stopColor="#2E6F9C" />
              <stop offset=".22" stopColor="#173F62" />
              <stop offset="1" stopColor="#081E36" />
            </linearGradient>
            <linearGradient id="fade" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0" stopColor="#061C33" stopOpacity="0" />
              <stop offset="1" stopColor="#061C33" stopOpacity=".78" />
            </linearGradient>
            <linearGradient id="fadeTop" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0" stopColor="#0A3B60" stopOpacity="1" />
              <stop offset="1" stopColor="#0A3B60" stopOpacity="0" />
            </linearGradient>
            <radialGradient id="hl" cx="0.5" cy="0.5" r="0.5">
              <stop offset="0" stopColor="#FFE9B8" stopOpacity=".9" />
              <stop offset="1" stopColor="#FFE9B8" stopOpacity="0" />
            </radialGradient>
          </defs>
          <rect width="264" height="640" fill="url(#sk)" />
          <g fill="#1F6791" opacity=".55">
            <rect x="-6" y="221" width="18" height="125" /><rect x="17" y="227" width="18" height="119" /><rect x="36" y="227" width="13" height="119" /><rect x="54" y="214" width="21" height="132" /><rect x="75" y="235" width="12" height="111" /><rect x="89" y="281" width="13" height="65" /><rect x="100" y="242" width="20" height="104" /><rect x="125" y="214" width="21" height="132" /><rect x="146" y="291" width="20" height="55" /><rect x="165" y="288" width="11" height="58" /><rect x="177" y="216" width="14" height="130" /><rect x="189" y="233" width="23" height="113" /><rect x="215" y="217" width="18" height="129" /><rect x="234" y="263" width="19" height="83" /><rect x="255" y="292" width="18" height="54" /><rect x="272" y="257" width="18" height="89" />
          </g>
          <g fill="#0F4670" opacity=".92">
            <rect x="-4" y="314" width="21" height="48" /><rect x="17" y="318" width="27" height="44" /><rect x="44" y="312" width="24" height="50" /><rect x="67" y="286" width="17" height="76" /><rect x="84" y="293" width="22" height="69" /><rect x="108" y="314" width="22" height="48" /><rect x="129" y="295" width="15" height="67" /><rect x="147" y="308" width="22" height="54" /><rect x="168" y="315" width="13" height="47" /><rect x="185" y="308" width="24" height="54" /><rect x="212" y="315" width="13" height="47" /><rect x="227" y="314" width="20" height="48" /><rect x="247" y="299" width="23" height="63" /><rect x="270" y="307" width="15" height="55" />
          </g>
          <rect x="0" y="300" width="264" height="60" fill="url(#mist)" />
          <rect x="0" y="346" width="264" height="294" fill="url(#gnd)" />
          <path d="M0 352 C 38 344 84 358 126 392 C 90 470 40 560 0 640 Z" fill="#092F50" />
          <path d="M264 350 C 226 348 190 372 168 400 C 200 480 250 560 264 640 Z" fill="#092F50" />
          <path d="M146,338 L160,338 C 175,400 235,520 300,640 L -50,640 C 60,530 125,410 146,338 Z" fill="url(#rd)" />
          <path d="M146,338 C 125,410 60,530 -50,640" fill="none" stroke="#54D2BB" strokeOpacity=".38" strokeWidth="1.3" />
          <path d="M160,338 C 175,400 235,520 300,640" fill="none" stroke="#54D2BB" strokeOpacity=".38" strokeWidth="1.3" />
          <rect x="0" y="340" width="264" height="300" fill="url(#fade)" opacity=".85" />
          <rect x="0" y="0" width="264" height="140" fill="url(#fadeTop)" />
        </svg>

        <div className="brand">
          <div className="brand-logo">
            <img src={axpenseLogo} alt="Axpense" />
          </div>
          <button className="side-toggle" onClick={onToggleRail} aria-label="Toggle menu" title="Collapse / expand menu">
            {rail ? <PanelLeftOpen className="ic-expand" /> : <PanelLeftClose className="ic-collapse" />}
          </button>
        </div>

        <nav className="side-scroll">
          {NAV.map(({ to, label, icon: Icon, end }) => (
            <NavLink
              key={to}
              to={to}
              end={end}
              onClick={onCloseMobile}
              className={({ isActive }) => cn("nav-a", isActive && "on")}
            >
              <Icon size={24} strokeWidth={1.55} />
              <span>{label}</span>
            </NavLink>
          ))}
          <AdminGroup rail={rail} onNavigate={onCloseMobile} />
        </nav>

        <div className="side-tag">
          Smarter Fleet
          <br />
          Lower Costs
          <br />
          Greater Control
          <svg width="70" height="11" viewBox="0 0 70 11" aria-hidden="true">
            <defs>
              <linearGradient id="sw" x1="0" x2="1">
                <stop offset="0" stopColor="#34D6B0" />
                <stop offset="1" stopColor="#34D6B0" stopOpacity="0" />
              </linearGradient>
            </defs>
            <path d="M2 8.5C16 2.5 42 2 68 6" fill="none" stroke="url(#sw)" strokeWidth="3" strokeLinecap="round" />
          </svg>
        </div>
      </aside>
    </>
  );
}

/** Administration with its sub-modules. Expands automatically while inside /administration. */
function AdminGroup({ rail, onNavigate }: { rail: boolean; onNavigate: () => void }) {
  const { pathname } = useLocation();
  const navigate = useNavigate();
  const inside = pathname.startsWith(ADMIN.base);
  const [open, setOpen] = useState(inside);
  useEffect(() => {
    if (inside) setOpen(true);
  }, [inside]);
  const Icon = ADMIN.icon;

  return (
    <div className={cn("nav-group", open && "open")}>
      <button
        type="button"
        className={cn("nav-a nav-parent", inside && !open && "on", inside && "within")}
        aria-expanded={open}
        onClick={() => (rail ? navigate(ADMIN.children[0].to) : setOpen((o) => !o))}
        title={ADMIN.label}
      >
        <Icon size={24} strokeWidth={1.55} />
        <span>{ADMIN.label}</span>
        <ChevronDown className="nav-chev" />
      </button>
      {open && !rail && (
        <div className="nav-children">
          {ADMIN.children.map(({ to, label, icon: ChildIcon }) => (
            <NavLink key={to} to={to} onClick={onNavigate} className={({ isActive }) => cn("nav-a nav-sub", isActive && "on")}>
              <ChildIcon size={19} strokeWidth={1.6} />
              <span>{label}</span>
            </NavLink>
          ))}
        </div>
      )}
    </div>
  );
}
