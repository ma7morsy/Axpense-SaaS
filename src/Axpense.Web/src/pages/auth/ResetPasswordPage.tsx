import { useState, type FormEvent } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { ArrowLeft, Lock, ShieldCheck } from "lucide-react";
import { authApi, ApiError } from "../../lib/api";
import { codeMessage, passwordScore, useAuthLang } from "../../lib/authI18n";
import { AuthAlert, AuthLayout, PasswordField, StrengthMeter, SubmitButton } from "../../components/auth/AuthLayout";

/** Set a new password from a reset link (/reset-password?email=…&token=…). Success returns to sign-in with a notice. */
export default function ResetPasswordPage() {
  const { t } = useAuthLang();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const email = params.get("email") ?? "";
  const token = params.get("token") ?? "";
  const [pw, setPw] = useState("");
  const [confirm, setConfirm] = useState("");
  const [errors, setErrors] = useState<{ pw?: string; confirm?: string }>({});
  const [error, setError] = useState("");
  const [expired, setExpired] = useState(false);
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const errs: typeof errors = {};
    if (!pw) errs.pw = t("required");
    else if (pw.length < 6) errs.pw = t("pwTooShort");
    if (!confirm) errs.confirm = t("required");
    else if (pw && confirm !== pw) errs.confirm = t("mismatch");
    setErrors(errs);
    setError("");
    if (Object.keys(errs).length) return;
    setBusy(true);
    try {
      await authApi.resetPassword({ email, token, newPassword: pw, confirmNewPassword: confirm });
      navigate("/login", { replace: true, state: { notice: "reset" } });
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.code === "INVALID_RESET_TOKEN") setExpired(true);
        else if (err.details?.newPassword) setErrors({ pw: codeMessage(t, err.code, err.details.newPassword) });
        else setError(err.status >= 500 ? t("genericError") : err.message);
      } else setError(t("network"));
    } finally {
      setBusy(false);
    }
  };

  const broken = !email || !token;

  return (
    <AuthLayout>
      <Link to="/login" className="auth-back">
        <ArrowLeft /> {t("backToSignIn")}
      </Link>
      <div className="auth-card">
        <div className="auth-h">
          <span className="ic-badge">
            <ShieldCheck />
          </span>
          <h1>{t("resetTitle")}</h1>
          {!broken && (
            <p>
              {t("resetSub", { email: "\u0000" })
                .split("\u0000")
                .flatMap((part, i) => (i === 0 ? [part] : [<b key={i}>{email}</b>, part]))}
            </p>
          )}
        </div>
        {broken || expired ? (
          <div className="auth-form">
            <AuthAlert>{broken ? t("badLink") : t("INVALID_RESET_TOKEN")}</AuthAlert>
            <Link to="/forgot-password" state={{ email }} className="auth-btn" style={{ textDecoration: "none" }}>
              {t("requestNew")}
            </Link>
          </div>
        ) : (
          <form className="auth-form" onSubmit={submit} noValidate>
            {error && <AuthAlert>{error}</AuthAlert>}
            <PasswordField
              t={t}
              id="rp-pw"
              label={t("newPassword")}
              icon={<Lock />}
              autoComplete="new-password"
              autoFocus
              value={pw}
              onChange={(e) => setPw(e.target.value)}
              error={errors.pw}
              hint={t("pwHint")}
            />
            {pw && <StrengthMeter score={passwordScore(pw)} t={t} />}
            <PasswordField
              t={t}
              id="rp-confirm"
              label={t("confirmPassword")}
              icon={<Lock />}
              autoComplete="new-password"
              value={confirm}
              onChange={(e) => setConfirm(e.target.value)}
              error={errors.confirm}
            />
            <SubmitButton busy={busy} t={t}>
              {t("resetBtn")}
            </SubmitButton>
          </form>
        )}
      </div>
    </AuthLayout>
  );
}
