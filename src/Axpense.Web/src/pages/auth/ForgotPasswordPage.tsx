import { useEffect, useState, type FormEvent } from "react";
import { Link, useLocation } from "react-router-dom";
import { ArrowLeft, KeyRound, Mail, MailCheck } from "lucide-react";
import { authApi, ApiError } from "../../lib/api";
import { EMAIL_RX, useAuthLang } from "../../lib/authI18n";
import { AuthAlert, AuthField, AuthLayout, SubmitButton } from "../../components/auth/AuthLayout";

const COOLDOWN = 30;

/**
 * Request a reset link. The API answers the same way whether or not the email exists (no account enumeration).
 * Email delivery is not configured yet: in Development only, the API returns the token and the link is shown here.
 */
export default function ForgotPasswordPage() {
  const { t } = useAuthLang();
  const location = useLocation();
  const [email, setEmail] = useState((location.state as { email?: string } | null)?.email ?? "");
  const [error, setError] = useState("");
  const [fieldError, setFieldError] = useState("");
  const [busy, setBusy] = useState(false);
  const [sent, setSent] = useState(false);
  const [devLink, setDevLink] = useState<string | null>(null);
  const [wait, setWait] = useState(0);

  useEffect(() => {
    if (wait <= 0) return;
    const id = setTimeout(() => setWait((w) => w - 1), 1000);
    return () => clearTimeout(id);
  }, [wait]);

  const send = async (e?: FormEvent) => {
    e?.preventDefault();
    const value = email.trim();
    if (!value) return setFieldError(t("required"));
    if (!EMAIL_RX.test(value)) return setFieldError(t("invalidEmail"));
    setFieldError("");
    setError("");
    setBusy(true);
    try {
      const res = await authApi.forgotPassword(value);
      setDevLink(res.resetToken ? `/reset-password?email=${encodeURIComponent(value)}&token=${encodeURIComponent(res.resetToken)}` : null);
      setSent(true);
      setWait(COOLDOWN);
    } catch (err) {
      setError(err instanceof ApiError && err.status < 500 ? err.message : t("network"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthLayout>
      <Link to="/login" className="auth-back">
        <ArrowLeft /> {t("backToSignIn")}
      </Link>
      <div className="auth-card">
        {!sent ? (
          <>
            <div className="auth-h">
              <span className="ic-badge">
                <KeyRound />
              </span>
              <h1>{t("forgotTitle")}</h1>
              <p>{t("forgotSub")}</p>
            </div>
            <form className="auth-form" onSubmit={send} noValidate>
              {error && <AuthAlert>{error}</AuthAlert>}
              <AuthField
                id="fp-email"
                type="email"
                label={t("email")}
                icon={<Mail />}
                autoComplete="email"
                autoFocus
                placeholder={t("emailPh")}
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                error={fieldError}
              />
              <SubmitButton busy={busy} t={t}>
                {t("sendLink")}
              </SubmitButton>
            </form>
          </>
        ) : (
          <>
            <div className="auth-h">
              <span className="ic-badge ok">
                <MailCheck />
              </span>
              <h1>{t("checkTitle")}</h1>
              <p>
                {t("checkSub", { email: "\u0000" })
                  .split("\u0000")
                  .flatMap((part, i) => (i === 0 ? [part] : [<b key={i}>{email.trim()}</b>, part]))}
              </p>
            </div>
            <div className="auth-form">
              {devLink && (
                <AuthAlert tone="info">
                  <b>{t("devLinkTitle")}</b> — {t("devLinkBody")}{" "}
                  <Link to={devLink} className="auth-link">
                    {t("openLink")}
                  </Link>
                </AuthAlert>
              )}
              {error && <AuthAlert>{error}</AuthAlert>}
              <button type="button" className="auth-btn sec" disabled={busy || wait > 0} onClick={() => send()}>
                {t("resend")}
                {wait > 0 ? ` (${wait})` : ""}
              </button>
            </div>
          </>
        )}
      </div>
    </AuthLayout>
  );
}
