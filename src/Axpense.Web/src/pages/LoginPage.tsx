import { useState, type FormEvent } from "react";
import { Link, Navigate, useLocation, useNavigate } from "react-router-dom";
import { ArrowRight, Lock, User } from "lucide-react";
import { useAuth } from "../contexts/AuthContext";
import { ApiError } from "../lib/api";
import { codeMessage, useAuthLang } from "../lib/authI18n";
import { AuthAlert, AuthField, AuthLayout, PasswordField, SubmitButton } from "../components/auth/AuthLayout";

/** Sign in. Validation messages are local; credential errors come from the API as codes and are translated. */
export default function LoginPage() {
  const { user, login } = useAuth();
  const { t } = useAuthLang();
  const navigate = useNavigate();
  const location = useLocation();
  const notice = (location.state as { notice?: string } | null)?.notice;
  const [id, setId] = useState("");
  const [password, setPassword] = useState("");
  const [remember, setRemember] = useState(true);
  const [errors, setErrors] = useState<{ id?: string; password?: string }>({});
  const [error, setError] = useState<{ code?: string; text: string } | null>(null);
  const [busy, setBusy] = useState(false);

  if (user) return <Navigate to={user.onboardingRequired ? "/onboarding" : "/"} replace />;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const errs: typeof errors = {};
    if (!id.trim()) errs.id = t("required");
    if (!password) errs.password = t("required");
    setErrors(errs);
    setError(null);
    if (Object.keys(errs).length) return;
    setBusy(true);
    try {
      await login(id.trim(), password, remember);
      navigate("/", { replace: true });
    } catch (err) {
      if (err instanceof ApiError) setError({ code: err.code, text: err.status >= 500 ? t("genericError") : err.message });
      else setError({ text: t("network") });
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthLayout>
      <div className="auth-card">
        <div className="auth-h">
          <h1>{t("loginTitle")}</h1>
          <p>{t("loginSub")}</p>
        </div>
        <form className="auth-form" onSubmit={submit} noValidate>
          {notice === "reset" && <AuthAlert tone="ok">{t("resetDoneSub")}</AuthAlert>}
          {error && <AuthAlert>{codeMessage(t, error.code, error.text)}</AuthAlert>}
          <AuthField
            id="login-id"
            label={t("loginId")}
            icon={<User />}
            autoComplete="username"
            autoFocus
            placeholder={t("loginIdPh")}
            value={id}
            onChange={(e) => setId(e.target.value)}
            error={errors.id}
          />
          <PasswordField
            t={t}
            id="login-pw"
            label={t("password")}
            icon={<Lock />}
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            error={errors.password}
            aside={
              <Link to="/forgot-password" state={{ email: id.includes("@") ? id.trim() : "" }}>
                {t("forgotLink")}
              </Link>
            }
          />
          <label className="auth-check">
            <input type="checkbox" checked={remember} onChange={(e) => setRemember(e.target.checked)} />
            {t("remember")}
          </label>
          <SubmitButton busy={busy} t={t}>
            {t("signIn")} <ArrowRight className="dir" />
          </SubmitButton>
        </form>
        {import.meta.env.DEV && (
          <div className="auth-demo">
            <span>
              {t("demo")}: <code>admin@axpense.local</code> / <code>Axpense123!</code>
            </span>
            <button
              type="button"
              className="auth-link"
              onClick={() => {
                setId("admin@axpense.local");
                setPassword("Axpense123!");
              }}
            >
              {t("useDemo")}
            </button>
          </div>
        )}
      </div>
      <p className="auth-foot">
        {t("noAccount")}
        <Link className="auth-link" to="/register">
          {t("createAccount")}
        </Link>
      </p>
    </AuthLayout>
  );
}
