import { Check } from "lucide-react";
import type { T } from "../../lib/authI18n";

/** Account → Company → Ready progress shown across registration and onboarding. */
export function SignupSteps({ t, active }: { t: T; active: 0 | 1 | 2 }) {
  const steps = [
    [t("stepAccount"), t("regTitle")],
    [t("stepCompany"), t("obStep1")],
    [t("stepReady"), t("obStep3D")],
  ];
  return (
    <ol className="ob-steps" aria-label={t("stepOf", { n: active + 1, total: 3 })}>
      {steps.map(([title, desc], i) => (
        <li key={title} className={i === active ? "on" : i < active ? "done" : ""} aria-current={i === active ? "step" : undefined}>
          <span className="n">{i < active ? <Check /> : i + 1}</span>
          <div style={{ minWidth: 0 }}>
            <b>{title}</b>
            <small>{desc}</small>
          </div>
        </li>
      ))}
    </ol>
  );
}
