import { useState, type FormEvent } from "react";
import { Link, Navigate, useNavigate } from "react-router-dom";
import { ArrowRight, Building2, Lock, Mail, User } from "lucide-react";
import { useAuth } from "../../contexts/AuthContext";
import { ApiError } from "../../lib/api";
import { EMAIL_RX, codeMessage, passwordScore, useAuthLang } from "../../lib/authI18n";
import { AuthAlert, AuthField, AuthLayout, PasswordField, StrengthMeter, SubmitButton } from "../../components/auth/AuthLayout";
import { SignupSteps } from "./SignupSteps";

type Field = "organizationName" | "firstName" | "lastName" | "email" | "password";

/** Create a workspace (organization + owner). The username is derived from the email; the company is set up next in onboarding. */
export default function RegisterPage() {
  const { user, register } = useAuth();
  const { t } = useAuthLang();
  const navigate = useNavigate();
  const [v, setV] = useState<Record<Field, string>>({ organizationName: "", firstName: "", lastName: "", email: "", password: "" });
  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({});
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  if (user) return <Navigate to={user.onboardingRequired ? "/onboarding" : "/"} replace />;

  const set = (k: Field) => (e: React.ChangeEvent<HTMLInputElement>) => {
    setV((p) => ({ ...p, [k]: e.target.value }));
    if (errors[k]) setErrors((p) => ({ ...p, [k]: undefined }));
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const errs: Partial<Record<Field, string>> = {};
    (["organizationName", "firstName", "lastName", "email", "password"] as Field[]).forEach((k) => {
      if (!v[k].trim()) errs[k] = t("required");
    });
    if (!errs.email && !EMAIL_RX.test(v.email.trim())) errs.email = t("invalidEmail");
    if (!errs.password && v.password.length < 6) errs.password = t("pwTooShort");
    setErrors(errs);
    setError("");
    if (Object.keys(errs).length) return;
    setBusy(true);
    try {
      await register({
        organizationName: v.organizationName.trim(),
        firstName: v.firstName.trim(),
        lastName: v.lastName.trim(),
        email: v.email.trim(),
        password: v.password,
        confirmPassword: v.password,
      });
      navigate("/onboarding", { replace: true });
    } catch (err) {
      if (err instanceof ApiError) {
        const fieldMap: Record<string, Field> = { email: "email", password: "password", userName: "email" };
        const fe: Partial<Record<Field, string>> = {};
        for (const [k, msg] of Object.entries(err.details ?? {})) if (fieldMap[k]) fe[fieldMap[k]] = codeMessage(t, err.code, msg);
        if (Object.keys(fe).length) setErrors(fe);
        else setError(codeMessage(t, err.code, err.status >= 500 ? t("genericError") : err.message));
      } else setError(t("network"));
    } finally {
      setBusy(false);
    }
  };

  const score = passwordScore(v.password);

  return (
    <AuthLayout>
      <SignupSteps t={t} active={0} />
      <div className="auth-card">
        <div className="auth-h">
          <h1>{t("regTitle")}</h1>
          <p>{t("regSub")}</p>
        </div>
        <form className="auth-form" onSubmit={submit} noValidate>
          {error && <AuthAlert>{error}</AuthAlert>}
          <AuthField
            id="reg-org"
            label={t("company")}
            icon={<Building2 />}
            autoComplete="organization"
            autoFocus
            placeholder={t("companyPh")}
            value={v.organizationName}
            onChange={set("organizationName")}
            error={errors.organizationName}
            maxLength={100}
          />
          <div className="auth-row">
            <AuthField id="reg-first" label={t("firstName")} icon={<User />} autoComplete="given-name" value={v.firstName} onChange={set("firstName")} error={errors.firstName} maxLength={50} />
            <AuthField id="reg-last" label={t("lastName")} autoComplete="family-name" value={v.lastName} onChange={set("lastName")} error={errors.lastName} maxLength={50} />
          </div>
          <AuthField
            id="reg-email"
            type="email"
            label={t("workEmail")}
            icon={<Mail />}
            autoComplete="email"
            placeholder={t("emailPh")}
            value={v.email}
            onChange={set("email")}
            error={errors.email}
          />
          <PasswordField
            t={t}
            id="reg-pw"
            label={t("createPassword")}
            icon={<Lock />}
            autoComplete="new-password"
            value={v.password}
            onChange={set("password")}
            error={errors.password}
            hint={t("pwHint")}
          />
          {v.password && <StrengthMeter score={score} t={t} />}
          <SubmitButton busy={busy} t={t}>
            {t("createBtn")} <ArrowRight className="dir" />
          </SubmitButton>
        </form>
      </div>
      <p className="auth-foot">
        {t("haveAccount")}
        <Link className="auth-link" to="/login">
          {t("signIn")}
        </Link>
      </p>
    </AuthLayout>
  );
}
