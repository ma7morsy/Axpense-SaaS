import { useEffect, useState, type InputHTMLAttributes, type ReactNode } from "react";
import { AlertCircle, CheckCircle2, Coins, Eye, EyeOff, Gauge, Globe, Leaf, ShieldCheck } from "lucide-react";
import axpenseLogo from "../../assets/axpense-logo.png";
import { useAuthLang, type T } from "../../lib/authI18n";
import "./auth.css";

/**
 * Split-screen shell for sign-in, register, password reset and onboarding:
 * brand panel (skyline art, headline, value points) on one side, the form on the other.
 * Sets dir="rtl" when Arabic is selected.
 */
export function AuthLayout({ children, wide = false }: { children: ReactNode; wide?: boolean }) {
  const { t, dir, lang, toggle } = useAuthLang();
  useEffect(() => {
    document.title = "Axpense";
  }, []);
  return (
    <div className={`auth ${lang === "ar" ? "ar" : ""}`} dir={dir} lang={lang}>
      <aside className="auth-brand" aria-hidden={false}>
        <BrandArt />
        <div className="ab-top">
          <span className="ab-logo">
            <img src={axpenseLogo} alt="Axpense" />
          </span>
        </div>
        <div className="ab-mid">
          <h2>{t("brandHeadline")}</h2>
          <p>{t("brandSub")}</p>
          <ul className="ab-vp">
            <Vp icon={<ShieldCheck />} title={t("vpSafety")} desc={t("vpSafetyD")} />
            <Vp icon={<Coins />} title={t("vpCosts")} desc={t("vpCostsD")} />
            <Vp icon={<Gauge />} title={t("vpEfficiency")} desc={t("vpEfficiencyD")} />
            <Vp icon={<Leaf />} title={t("vpGreen")} desc={t("vpGreenD")} />
          </ul>
        </div>
        <div className="ab-foot">
          <b>{t("tagline")}</b>
          <span>
            © {new Date().getFullYear()} Axpense. {t("rights")}
          </span>
        </div>
      </aside>

      <main className="auth-main">
        <header className="am-top">
          <img className="am-logo" src={axpenseLogo} alt="Axpense" />
          <button type="button" className="lang-btn" onClick={toggle} aria-label="Change language">
            <Globe />
            {t("language")}
          </button>
        </header>
        <div className={`am-body ${wide ? "wide" : ""}`}>{children}</div>
      </main>
    </div>
  );
}

function Vp({ icon, title, desc }: { icon: ReactNode; title: string; desc: string }) {
  return (
    <li>
      <span className="vp-ic">{icon}</span>
      <div>
        <b>{title}</b>
        <small>{desc}</small>
      </div>
    </li>
  );
}

/** Text input with a leading icon, label and inline error. */
export function AuthField({
  id,
  label,
  icon,
  error,
  hint,
  aside,
  ...input
}: InputHTMLAttributes<HTMLInputElement> & { id: string; label: string; icon?: ReactNode; error?: string; hint?: ReactNode; aside?: ReactNode }) {
  return (
    <div className={`af ${error ? "err" : ""}`}>
      <div className="af-lbl">
        <label htmlFor={id}>{label}</label>
        {aside}
      </div>
      <div className="af-in">
        {icon && <span className="af-ic">{icon}</span>}
        <input id={id} aria-invalid={!!error} aria-describedby={error || hint ? `${id}-msg` : undefined} {...input} />
      </div>
      {error ? (
        <span className="af-msg" id={`${id}-msg`} role="alert">
          {error}
        </span>
      ) : hint ? (
        <span className="af-hint" id={`${id}-msg`}>
          {hint}
        </span>
      ) : null}
    </div>
  );
}

/** Password input with show/hide toggle. */
export function PasswordField(props: Omit<Parameters<typeof AuthField>[0], "type"> & { t: T }) {
  const { t, ...rest } = props;
  const [show, setShow] = useState(false);
  return (
    <div className="pw-wrap">
      <AuthField {...rest} type={show ? "text" : "password"} />
      <button type="button" className="pw-eye" onClick={() => setShow((s) => !s)} aria-label={show ? t("hide") : t("show")} title={show ? t("hide") : t("show")}>
        {show ? <EyeOff /> : <Eye />}
      </button>
    </div>
  );
}

export function StrengthMeter({ score, t }: { score: number; t: T }) {
  const labels = [t("weak"), t("weak"), t("fair"), t("good"), t("strong")];
  return (
    <div className={`pw-meter s${score}`} aria-live="polite">
      <div className="bars">
        {[1, 2, 3, 4].map((i) => (
          <i key={i} className={score >= i ? "on" : ""} />
        ))}
      </div>
      <span>
        {t("strength")}: <b>{labels[score]}</b>
      </span>
    </div>
  );
}

export function AuthAlert({ tone = "bad", children }: { tone?: "bad" | "ok" | "info"; children: ReactNode }) {
  return (
    <div className={`auth-alert aa-${tone}`} role={tone === "bad" ? "alert" : "status"}>
      {tone === "ok" ? <CheckCircle2 /> : <AlertCircle />}
      <div>{children}</div>
    </div>
  );
}

export function SubmitButton({ busy, children, t }: { busy: boolean; children: ReactNode; t: T }) {
  return (
    <button type="submit" className="auth-btn" disabled={busy} aria-busy={busy}>
      {busy ? (
        <>
          <span className="spin" aria-hidden /> {t("pleaseWait")}
        </>
      ) : (
        children
      )}
    </button>
  );
}

/** City skyline and road — the same art direction as the app sidebar. */
function BrandArt() {
  return (
    <svg className="ab-art" viewBox="-200 150 664 490" preserveAspectRatio="xMidYMax slice" aria-hidden="true">
      <defs>
        <radialGradient id="a-sk" cx="150" cy="330" r="300" gradientUnits="userSpaceOnUse">
          <stop offset="0" stopColor="#4FB0E6" stopOpacity=".5" />
          <stop offset=".55" stopColor="#2A7DB8" stopOpacity=".16" />
          <stop offset="1" stopColor="#0B4A78" stopOpacity="0" />
        </radialGradient>
        <linearGradient id="a-mist" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#5DB7E6" stopOpacity="0" />
          <stop offset=".55" stopColor="#5DB7E6" stopOpacity=".30" />
          <stop offset="1" stopColor="#5DB7E6" stopOpacity="0" />
        </linearGradient>
        <linearGradient id="a-gnd" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#0D3B60" />
          <stop offset="1" stopColor="#051B32" />
        </linearGradient>
        <linearGradient id="a-rd" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#2E6F9C" />
          <stop offset=".22" stopColor="#173F62" />
          <stop offset="1" stopColor="#081E36" />
        </linearGradient>
        <linearGradient id="a-fade" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#061C33" stopOpacity="0" />
          <stop offset="1" stopColor="#061C33" stopOpacity=".8" />
        </linearGradient>
        <linearGradient id="a-top" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#0A2C4A" stopOpacity="1" />
          <stop offset="1" stopColor="#0A2C4A" stopOpacity="0" />
        </linearGradient>
        <g id="a-far">
          <rect x="-6" y="221" width="18" height="125" /><rect x="17" y="227" width="18" height="119" /><rect x="36" y="227" width="13" height="119" /><rect x="54" y="214" width="21" height="132" /><rect x="75" y="235" width="12" height="111" /><rect x="89" y="281" width="13" height="65" /><rect x="100" y="242" width="20" height="104" /><rect x="125" y="214" width="21" height="132" /><rect x="146" y="291" width="20" height="55" /><rect x="165" y="288" width="11" height="58" /><rect x="177" y="216" width="14" height="130" /><rect x="189" y="233" width="23" height="113" /><rect x="215" y="217" width="18" height="129" /><rect x="234" y="263" width="19" height="83" /><rect x="253" y="240" width="13" height="106" />
        </g>
        <g id="a-near">
          <rect x="-4" y="314" width="21" height="48" /><rect x="17" y="318" width="27" height="44" /><rect x="44" y="312" width="24" height="50" /><rect x="67" y="286" width="17" height="76" /><rect x="84" y="293" width="22" height="69" /><rect x="108" y="314" width="22" height="48" /><rect x="129" y="295" width="15" height="67" /><rect x="147" y="308" width="22" height="54" /><rect x="168" y="315" width="13" height="47" /><rect x="185" y="308" width="24" height="54" /><rect x="212" y="315" width="13" height="47" /><rect x="227" y="314" width="20" height="48" /><rect x="247" y="299" width="19" height="63" />
        </g>
      </defs>
      <rect x="-200" y="150" width="664" height="490" fill="url(#a-sk)" />
      <g fill="#1F6791" opacity=".5">
        <use href="#a-far" x="-264" /><use href="#a-far" /><use href="#a-far" x="264" />
      </g>
      <g fill="#0F4670" opacity=".92">
        <use href="#a-near" x="-264" /><use href="#a-near" /><use href="#a-near" x="264" />
      </g>
      <rect x="-200" y="300" width="664" height="60" fill="url(#a-mist)" />
      <rect x="-200" y="346" width="664" height="294" fill="url(#a-gnd)" />
      <path d="M-200 356 C -60 344 60 358 126 392 C 90 470 40 560 -40 640 L -200 640 Z" fill="#092F50" />
      <path d="M464 352 C 330 346 210 366 168 400 C 200 480 250 560 330 640 L 464 640 Z" fill="#092F50" />
      <path d="M146,338 L160,338 C 175,400 235,520 300,640 L -50,640 C 60,530 125,410 146,338 Z" fill="url(#a-rd)" />
      <path d="M146,338 C 125,410 60,530 -50,640" fill="none" stroke="#54D2BB" strokeOpacity=".38" strokeWidth="1.3" />
      <path d="M160,338 C 175,400 235,520 300,640" fill="none" stroke="#54D2BB" strokeOpacity=".38" strokeWidth="1.3" />
      <path d="M153,345 C 156,420 170,520 190,640" fill="none" stroke="#FFFFFF" strokeOpacity=".22" strokeWidth="1.4" strokeDasharray="7 9" />
      <rect x="-200" y="340" width="664" height="300" fill="url(#a-fade)" opacity=".85" />
      <rect x="-200" y="150" width="664" height="90" fill="url(#a-top)" />
    </svg>
  );
}
