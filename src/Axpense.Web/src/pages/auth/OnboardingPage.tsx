import { useEffect, useState, type FormEvent, type ReactNode } from "react";
import { Link, Navigate, useNavigate } from "react-router-dom";
import { ArrowLeft, ArrowRight, Car, ChevronRight, PartyPopper, Users, Wallet } from "lucide-react";
import { useAuth } from "../../contexts/AuthContext";
import { ApiError, companyApi } from "../../lib/api";
import type { CompanyOptions } from "../../lib/types";
import { useAuthLang } from "../../lib/authI18n";
import { AuthAlert, AuthLayout, SubmitButton } from "../../components/auth/AuthLayout";
import { PageSpinner } from "../../components/ui/Spinner";
import { SignupSteps } from "./SignupSteps";

type Values = Record<string, string>;
const STEP1 = ["companyName", "legalName", "industry", "companySize"];

/**
 * First-run wizard for a new workspace: company details → location & preferences → done.
 * Saves through POST /api/company/onboarding (same validation as Administration → Company); "Skip for now" marks it done.
 */
export default function OnboardingPage() {
  const { user, refresh } = useAuth();
  const { t } = useAuthLang();
  const navigate = useNavigate();
  const [options, setOptions] = useState<CompanyOptions | null>(null);
  const [v, setV] = useState<Values>({});
  const [step, setStep] = useState(0);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [loadError, setLoadError] = useState("");

  useEffect(() => {
    Promise.all([companyApi.options(), companyApi.profile()])
      .then(([o, p]) => {
        setOptions(o);
        setV({
          companyName: p.companyName ?? "",
          legalName: p.legalName ?? "",
          industry: p.industry ?? "",
          companySize: p.companySize ?? "",
          country: p.country ?? "",
          city: p.city ?? "",
          phone: p.phone ?? "",
          timeZone: p.timeZone,
          currency: p.currency,
          fiscalYearStartMonth: String(p.fiscalYearStartMonth),
          dateFormat: p.dateFormat,
          distanceUnit: p.distanceUnit,
        });
      })
      .catch((e) => setLoadError(e instanceof Error ? e.message : t("genericError")));
  }, [t]);

  if (!user) return <Navigate to="/login" replace />;
  if (!user.onboardingRequired && step < 2) return <Navigate to="/" replace />;

  const set = (k: string) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    setV((p) => ({ ...p, [k]: e.target.value }));
    if (errors[k]) setErrors((p) => ({ ...p, [k]: "" }));
  };

  const next = (e: FormEvent) => {
    e.preventDefault();
    if (!v.companyName?.trim()) {
      setErrors({ companyName: t("required") });
      return;
    }
    setErrors({});
    setStep(1);
  };

  const finish = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError("");
    try {
      const body: Record<string, unknown> = {};
      for (const [k, val] of Object.entries(v)) body[k] = val?.trim() ? val.trim() : null;
      body.fiscalYearStartMonth = Number(v.fiscalYearStartMonth);
      await companyApi.completeOnboarding(body);
      setStep(2);
      await refresh();
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.details ?? {}).length) {
        setErrors(err.details);
        setError(t("fixFields"));
        if (Object.keys(err.details).some((k) => STEP1.includes(k))) setStep(0);
      } else setError(err instanceof ApiError && err.status < 500 ? err.message : t("genericError"));
    } finally {
      setBusy(false);
    }
  };

  const skip = async () => {
    setBusy(true);
    try {
      await companyApi.skipOnboarding();
      await refresh();
      navigate("/", { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : t("genericError"));
      setBusy(false);
    }
  };

  if (loadError)
    return (
      <AuthLayout wide>
        <AuthAlert>{loadError}</AuthAlert>
      </AuthLayout>
    );
  if (!options) return <PageSpinner />;

  const sel = (name: string, label: string, opts: { value: string; label: string }[], blank = false, full = false) => (
    <Field name={name} label={label} error={errors[name]} full={full}>
      <select id={`ob-${name}`} value={v[name] ?? ""} onChange={set(name)} aria-invalid={!!errors[name]}>
        {blank && <option value="">{t("select")}</option>}
        {opts.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
    </Field>
  );
  const txt = (name: string, label: string, props: React.InputHTMLAttributes<HTMLInputElement> = {}, full = false) => (
    <Field name={name} label={label} error={errors[name]} full={full}>
      <div className="af-in">
        <input id={`ob-${name}`} value={v[name] ?? ""} onChange={set(name)} aria-invalid={!!errors[name]} {...props} />
      </div>
    </Field>
  );
  const plain = (xs: string[]) => xs.map((x) => ({ value: x, label: x }));
  const firstName = user.firstName || user.userName;

  return (
    <AuthLayout wide>
      <SignupSteps t={t} active={step === 2 ? 2 : 1} />
      <div className="auth-card">
        {step < 2 && (
          <div className="auth-h">
            <div className="ob-count">
              {t("stepOf", { n: step + 1, total: 2 })} · {step === 0 ? t("obStep1") : t("obStep2")}
            </div>
            <h1>{t("obWelcome", { name: firstName })}</h1>
            <p>{t("obSub")}</p>
          </div>
        )}

        {error && step < 2 && (
          <div style={{ marginBottom: 16 }}>
            <AuthAlert>{error}</AuthAlert>
          </div>
        )}

        {step === 0 && (
          <form onSubmit={next} noValidate>
            <div className="ob-grid">
              {txt("companyName", `${t("company")} *`, { autoFocus: true, autoComplete: "organization", maxLength: 120 }, true)}
              {txt("legalName", t("legalName"), { maxLength: 160 }, true)}
              {sel("industry", t("industry"), plain(options.industries), true)}
              {sel("companySize", t("size"), plain(options.sizes), true)}
            </div>
            <div className="ob-actions">
              <button type="button" className="auth-link" onClick={skip} disabled={busy}>
                {t("skip")}
              </button>
              <span className="grow" />
              <button type="submit" className="auth-btn">
                {t("next")} <ArrowRight className="dir" />
              </button>
            </div>
          </form>
        )}

        {step === 1 && (
          <form onSubmit={finish} noValidate>
            <div className="ob-grid">
              {txt("country", t("country"), { autoComplete: "country-name", maxLength: 80, autoFocus: true })}
              {txt("city", t("city"), { autoComplete: "address-level2", maxLength: 80 })}
              {txt("phone", t("phone"), { type: "tel", autoComplete: "tel", dir: "ltr" }, true)}
              {sel("timeZone", t("timeZone"), options.timeZones)}
              {sel("currency", t("currency"), options.currencies)}
              {sel("fiscalYearStartMonth", t("fiscal"), options.months)}
              {sel("dateFormat", t("dateFormat"), plain(options.dateFormats))}
              {sel("distanceUnit", t("distance"), options.distanceUnits, false, true)}
            </div>
            <div className="ob-actions">
              <button type="button" className="auth-btn sec" onClick={() => setStep(0)} disabled={busy}>
                <ArrowLeft className="dir" /> {t("back")}
              </button>
              <span className="grow" />
              <button type="button" className="auth-link" onClick={skip} disabled={busy}>
                {t("skip")}
              </button>
              <div>
                <SubmitButton busy={busy} t={t}>
                  {t("finish")}
                </SubmitButton>
              </div>
            </div>
          </form>
        )}

        {step === 2 && (
          <>
            <div className="auth-h">
              <span className="ic-badge ok">
                <PartyPopper />
              </span>
              <h1>{t("doneTitle")}</h1>
              <p>{t("doneSub")}</p>
            </div>
            <div className="ob-next">
              <Next to="/vehicles" icon={<Car />} title={t("nextVehicle")} desc={t("nextVehicleD")} />
              <Next to="/administration/users" icon={<Users />} title={t("nextTeam")} desc={t("nextTeamD")} />
              <Next to="/administration/settings?tab=budget" icon={<Wallet />} title={t("nextBudget")} desc={t("nextBudgetD")} />
            </div>
            <div className="ob-actions">
              <span className="grow" />
              <button type="button" className="auth-btn" onClick={() => navigate("/", { replace: true })}>
                {t("goDashboard")} <ArrowRight className="dir" />
              </button>
            </div>
          </>
        )}
      </div>
    </AuthLayout>
  );
}

function Field({ name, label, error, full, children }: { name: string; label: string; error?: string; full?: boolean; children: ReactNode }) {
  return (
    <div className={`af ${error ? "err" : ""} ${full ? "full" : ""}`}>
      <div className="af-lbl">
        <label htmlFor={`ob-${name}`}>{label}</label>
      </div>
      {children}
      {error && (
        <span className="af-msg" role="alert">
          {error}
        </span>
      )}
    </div>
  );
}

function Next({ to, icon, title, desc }: { to: string; icon: ReactNode; title: string; desc: string }) {
  return (
    <Link to={to}>
      <span className="ic">{icon}</span>
      <div>
        <b>{title}</b>
        <small>{desc}</small>
      </div>
      <ChevronRight className="chev" />
    </Link>
  );
}
