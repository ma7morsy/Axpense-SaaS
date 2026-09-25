import { useCallback, useEffect, useRef, useState, type FormEvent, type ReactNode } from "react";
import { useSearchParams } from "react-router-dom";
import { BarChart3, Building2, Check, CreditCard, Download, Globe, MapPin, Plus } from "lucide-react";
import { PageHeader } from "../../components/ui/PageHeader";
import { Button } from "../../components/ui/Button";
import { Modal } from "../../components/ui/Modal";
import { ConfirmDialog } from "../../components/ui/ConfirmDialog";
import { PageSpinner } from "../../components/ui/Spinner";
import { useToast } from "../../components/ui/Toast";
import { ApiError, companyApi } from "../../lib/api";
import type { BillingInfo, CompanyOptions, CompanyProfile, PlanInfo, SubscriptionInfo } from "../../lib/types";
import { formatDay, initials } from "../../lib/utils";
import { useIsAdmin } from "./useIsAdmin";

/**
 * Administration → Company: company info, subscription and billing.
 * All rules (validation, plan limits, renewals, invoices) live in the API; this page only renders and maps errors.
 */
type Tab = "info" | "sub" | "bill";
const TABS: [Tab, string][] = [
  ["info", "Company info"],
  ["sub", "Subscription"],
  ["bill", "Billing"],
];

const nf = (n: number) => n.toLocaleString("en-US", { maximumFractionDigits: 0 });

/** Loading / error state shared by the three tabs. */
function useLoad<T>(load: () => Promise<T>) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState("");
  const run = useCallback(() => {
    setError("");
    load()
      .then(setData)
      .catch((e) => setError(e instanceof Error ? e.message : "Could not load"));
  }, [load]);
  useEffect(run, [run]);
  return { data, setData, error, reload: run };
}

function LoadError({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div className="card">
      <div className="empty">
        <h3>Unable to load</h3>
        <p>{message}</p>
        <div style={{ marginTop: 16 }}>
          <Button onClick={onRetry}>Try again</Button>
        </div>
      </div>
    </div>
  );
}

export default function CompanyPage() {
  const { isAdmin } = useIsAdmin();
  const [params, setParams] = useSearchParams();
  const raw = params.get("tab") ?? "";
  const tab: Tab = raw === "sub" || raw === "bill" ? raw : "info";
  const [companyName, setCompanyName] = useState("");
  const [saveSignal, setSaveSignal] = useState(0);
  const [saving, setSaving] = useState(false);
  useEffect(() => {
    companyApi
      .profile()
      .then((p) => setCompanyName(p.companyName))
      .catch(() => undefined);
  }, []);

  return (
    <div>
      <PageHeader
        eyebrow="Administration"
        title="Company"
        subtitle={`Manage ${companyName || "your company"} — details, plan and billing.`}
        action={
          tab === "info" && isAdmin ? (
            <Button onClick={() => setSaveSignal((n) => n + 1)} disabled={saving}>
              <Check /> {saving ? "Saving…" : "Save changes"}
            </Button>
          ) : undefined
        }
      />
      <div className="tabs" role="tablist">
        {TABS.map(([k, label]) => (
          <div
            key={k}
            role="tab"
            tabIndex={0}
            aria-selected={tab === k}
            className={`tab ${tab === k ? "on" : ""}`}
            onClick={() => setParams(k === "info" ? {} : { tab: k }, { replace: true })}
          >
            {label}
          </div>
        ))}
      </div>
      {tab === "info" && <CompanyInfo isAdmin={isAdmin} saveSignal={saveSignal} onSaving={setSaving} onName={setCompanyName} />}
      {tab === "sub" && <SubscriptionTab isAdmin={isAdmin} />}
      {tab === "bill" && (isAdmin ? <BillingTab /> : <NoAccess />)}
    </div>
  );
}

function NoAccess() {
  return (
    <div className="card">
      <div className="empty">
        <h3>Billing is limited to owners and admins</h3>
        <p>Ask an administrator for invoices or payment details.</p>
      </div>
    </div>
  );
}

// =====================================================================
// Company info
// =====================================================================
type Values = Record<string, string>;
const PROFILE_FIELDS = [
  "companyName", "legalName", "industry", "companySize", "commercialRegistrationNo", "taxId", "foundedYear",
  "address", "city", "country", "phone", "email", "website", "timeZone", "currency", "fiscalYearStartMonth", "dateFormat", "distanceUnit",
] as const;

function toValues(p: CompanyProfile): Values {
  const v: Values = {};
  for (const k of PROFILE_FIELDS) v[k] = p[k] == null ? "" : String(p[k]);
  return v;
}

function CompanyInfo({
  isAdmin,
  saveSignal,
  onSaving,
  onName,
}: {
  isAdmin: boolean;
  saveSignal: number;
  onSaving: (b: boolean) => void;
  onName: (n: string) => void;
}) {
  const toast = useToast();
  const load = useCallback(() => Promise.all([companyApi.profile(), companyApi.options()]), []);
  const { data, error, reload } = useLoad(load);
  const [profile, setProfile] = useState<CompanyProfile | null>(null);
  const [options, setOptions] = useState<CompanyOptions | null>(null);
  const [values, setValues] = useState<Values>({});
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [logo, setLogo] = useState<string | null>(null);
  const [logoBusy, setLogoBusy] = useState(false);
  const fileRef = useRef<HTMLInputElement>(null);
  const lastSignal = useRef(saveSignal);

  useEffect(() => {
    if (!data) return;
    setProfile(data[0]);
    setOptions(data[1]);
    setValues(toValues(data[0]));
    onName(data[0].companyName);
  }, [data, onName]);

  // The logo is served with the auth header → object URL.
  useEffect(() => {
    if (!profile?.hasLogo) {
      setLogo(null);
      return;
    }
    let url: string | null = null;
    let cancelled = false;
    companyApi
      .logoUrl()
      .then((u) => {
        if (cancelled) URL.revokeObjectURL(u);
        else setLogo((url = u));
      })
      .catch(() => setLogo(null));
    return () => {
      cancelled = true;
      if (url) URL.revokeObjectURL(url);
    };
  }, [profile?.hasLogo, profile?.logoVersion]);

  const save = useCallback(async () => {
    onSaving(true);
    setErrors({});
    try {
      const body: Record<string, unknown> = {};
      for (const k of PROFILE_FIELDS) body[k] = values[k]?.trim() ? values[k].trim() : null;
      body.foundedYear = values.foundedYear?.trim() ? Number(values.foundedYear) : null;
      body.fiscalYearStartMonth = Number(values.fiscalYearStartMonth);
      const res = await companyApi.saveProfile(body);
      setProfile(res);
      setValues(toValues(res));
      onName(res.companyName);
      toast("Company details saved");
    } catch (e) {
      if (e instanceof ApiError && Object.keys(e.details).length) {
        setErrors(e.details);
        toast("Fill in the highlighted fields to continue", "bad");
      } else toast(e instanceof Error ? e.message : "Could not save", "bad");
    } finally {
      onSaving(false);
    }
  }, [values, onSaving, onName, toast]);

  useEffect(() => {
    if (saveSignal !== lastSignal.current) {
      lastSignal.current = saveSignal;
      save();
    }
  }, [saveSignal, save]);

  const upload = async (file: File | undefined) => {
    if (!file) return;
    setLogoBusy(true);
    try {
      setProfile(await companyApi.uploadLogo(file));
      toast("Logo updated");
    } catch (e) {
      toast(e instanceof ApiError ? (e.details.logo ?? e.message) : "Could not upload the logo", "bad");
    } finally {
      setLogoBusy(false);
      if (fileRef.current) fileRef.current.value = "";
    }
  };
  const removeLogo = async () => {
    setLogoBusy(true);
    try {
      setProfile(await companyApi.removeLogo());
      toast("Logo removed");
    } catch (e) {
      toast(e instanceof Error ? e.message : "Could not remove the logo", "bad");
    } finally {
      setLogoBusy(false);
    }
  };

  if (error) return <LoadError message={error} onRetry={reload} />;
  if (!profile || !options) return <PageSpinner />;

  const set = (k: string, v: string) => setValues((p) => ({ ...p, [k]: v }));
  const field = (
    name: string,
    label: string,
    opts: { req?: boolean; full?: boolean; type?: string; placeholder?: string; select?: { value: string; label: string }[]; blank?: string } = {}
  ) => {
    const id = `co-${name}`;
    const style = errors[name] ? { borderColor: "var(--red)" } : undefined;
    return (
      <div className={`f ${opts.full ? "full" : ""}`} key={name}>
        <label htmlFor={id}>
          {label}
          {opts.req && <span className="req"> *</span>}
        </label>
        {opts.select ? (
          <select id={id} value={values[name] ?? ""} disabled={!isAdmin} onChange={(e) => set(name, e.target.value)} style={style}>
            {opts.blank !== undefined && <option value="">{opts.blank}</option>}
            {opts.select.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </select>
        ) : (
          <input
            id={id}
            type={opts.type ?? "text"}
            value={values[name] ?? ""}
            placeholder={opts.placeholder}
            disabled={!isAdmin}
            onChange={(e) => set(name, e.target.value)}
            style={style}
          />
        )}
        {errors[name] && (
          <span className="hint" style={{ color: "var(--red)" }}>
            {errors[name]}
          </span>
        )}
      </div>
    );
  };
  const plain = (xs: string[]) => xs.map((x) => ({ value: x, label: x }));

  return (
    <form
      id="coForm"
      className="co-info"
      noValidate
      onSubmit={(e: FormEvent) => {
        e.preventDefault();
        if (isAdmin) save();
      }}
    >
      {!isAdmin && (
        <div className="banner" style={{ marginBottom: 16 }}>
          Only owners and admins can change company details.
        </div>
      )}
      <section className="dc">
        <div className="dc-h">
          <Building2 />
          <h2>Identity</h2>
        </div>
        <div className="logo-row">
          <span className="logo-box">{logo ? <img src={logo} alt="" /> : initials(profile.companyName)}</span>
          <div>
            <b>Company logo</b>
            <span className="t-sub" style={{ display: "block", margin: "2px 0 10px" }}>
              PNG or SVG, shown on reports and invoices.
            </span>
            {isAdmin && (
              <>
                <label className="btn sm" style={{ cursor: logoBusy ? "wait" : "pointer" }}>
                  <Plus /> {logoBusy ? "Uploading…" : "Upload logo"}
                  <input
                    ref={fileRef}
                    type="file"
                    accept=".png,.jpg,.jpeg,.svg,image/png,image/jpeg,image/svg+xml"
                    hidden
                    disabled={logoBusy}
                    onChange={(e) => upload(e.target.files?.[0])}
                  />
                </label>{" "}
                {profile.hasLogo && (
                  <Button type="button" variant="ghost" size="sm" onClick={removeLogo} disabled={logoBusy}>
                    Remove
                  </Button>
                )}
              </>
            )}
          </div>
        </div>
        <div className="form-grid">
          {field("companyName", "Company name", { req: true })}
          {field("legalName", "Legal name")}
          {field("industry", "Industry", { select: plain(options.industries), blank: "Select industry" })}
          {field("companySize", "Company size", { select: plain(options.sizes), blank: "Select size" })}
          {field("commercialRegistrationNo", "Commercial registration no.")}
          {field("taxId", "Tax ID")}
          {field("foundedYear", "Founded", { type: "number", placeholder: "e.g. 2016" })}
        </div>
      </section>

      <section className="dc">
        <div className="dc-h">
          <MapPin />
          <h2>Contact &amp; address</h2>
        </div>
        <div className="form-grid">
          {field("address", "Address", { full: true })}
          {field("city", "City")}
          {field("country", "Country")}
          {field("phone", "Phone", { type: "tel" })}
          {field("email", "Email", { type: "email" })}
          {field("website", "Website", { placeholder: "www.example.com" })}
        </div>
      </section>

      <section className="dc">
        <div className="dc-h">
          <Globe />
          <h2>Regional settings</h2>
        </div>
        <div className="form-grid">
          {field("timeZone", "Time zone", { select: options.timeZones })}
          {field("currency", "Currency", { select: options.currencies })}
          {field("fiscalYearStartMonth", "Fiscal year starts", { select: options.months })}
          {field("dateFormat", "Date format", { select: plain(options.dateFormats) })}
          {field("distanceUnit", "Distance unit", { select: options.distanceUnits })}
        </div>
      </section>
      <button type="submit" hidden />
    </form>
  );
}

// =====================================================================
// Subscription
// =====================================================================
const lim = (n: number | null) => (n == null ? "Unlimited" : nf(n));
const gb = (bytes: number) => {
  const g = bytes / 1024 ** 3;
  return g >= 10 ? g.toFixed(0) : g >= 0.1 ? g.toFixed(1) : g > 0 ? "<0.1" : "0";
};

function Meter({ label, used, limit, unit = "", display }: { label: string; used: number; limit: number | null; unit?: string; display?: ReactNode }) {
  const pct = limit == null ? Math.min(100, used ? 12 : 0) : Math.min(100, limit ? (used / limit) * 100 : 0);
  const tone = pct >= 100 ? "bad" : pct >= 85 ? "warn" : "";
  return (
    <div className="meter">
      <div className="between">
        <span>{label}</span>
        <b>
          {display ?? nf(used)}
          {unit}{" "}
          <small>
            / {lim(limit)}
            {limit == null ? "" : unit}
          </small>
        </b>
      </div>
      <div className={`bar-mini ${tone}`}>
        <i style={{ width: `${pct}%` }} />
      </div>
    </div>
  );
}

function SubscriptionTab({ isAdmin }: { isAdmin: boolean }) {
  const toast = useToast();
  const load = useCallback(() => companyApi.subscription(), []);
  const { data: sub, setData, error, reload } = useLoad(load);
  const [cycle, setCycle] = useState<"monthly" | "annual" | null>(null);
  const [pick, setPick] = useState<PlanInfo | null>(null);
  const [confirmCancel, setConfirmCancel] = useState(false);
  const [busy, setBusy] = useState(false);
  const plansRef = useRef<HTMLHeadingElement>(null);

  if (error) return <LoadError message={error} onRetry={reload} />;
  if (!sub) return <PageSpinner />;

  const cyc = cycle ?? sub.billingCycle;
  const price = (p: PlanInfo, c: string) => (c === "annual" ? p.annual : p.monthly);
  const current = sub.plans.find((p) => p.key === sub.planKey);
  const rank = (k: string) => sub.plans.findIndex((p) => p.key === k);
  const ended = sub.status === "Cancelled";

  const act = async (fn: () => Promise<SubscriptionInfo>, msg: string) => {
    setBusy(true);
    try {
      setData(await fn());
      toast(msg);
    } catch (e) {
      toast(e instanceof Error ? e.message : "Something went wrong", "bad");
    } finally {
      setBusy(false);
    }
  };

  const statusBadge =
    sub.status === "Active" ? (
      <span className="badge ok">Active</span>
    ) : sub.status === "Cancelling" ? (
      <span className="badge warn">Cancels {formatDay(sub.currentPeriodEnd)}</span>
    ) : (
      <span className="badge bad">Cancelled</span>
    );

  return (
    <>
      <section className="plan-hero">
        <div>
          <div className="ph-k">Current plan {statusBadge}</div>
          <h2>{sub.planName}</h2>
          <div className="ph-p">
            EGP {nf(sub.price)} <small>/ {sub.billingCycle === "annual" ? "year" : "month"}</small>
          </div>
          <div className="ph-m">
            {ended ? (
              <>
                Ended <b>{formatDay(sub.currentPeriodEnd)}</b>
              </>
            ) : (
              <>
                {sub.status === "Cancelling" ? "Ends" : "Renews"} <b>{formatDay(sub.currentPeriodEnd)}</b> ({sub.daysLeft} days)
              </>
            )}{" "}
            · {sub.autoRenew ? "auto-renew on" : "auto-renew off"} · Customer since {formatDay(sub.customerSince)}
          </div>
        </div>
        {isAdmin && (
          <div className="ph-a">
            <Button variant="secondary" onClick={() => plansRef.current?.scrollIntoView({ behavior: "smooth", block: "start" })}>
              {ended ? "Choose a plan" : "Change plan"}
            </Button>
            {sub.status === "Active" && (
              <Button
                variant="ghost"
                disabled={busy}
                onClick={() => act(() => companyApi.setAutoRenew(!sub.autoRenew), `Auto-renew ${sub.autoRenew ? "off" : "on"}`)}
              >
                {sub.autoRenew ? "Turn off auto-renew" : "Turn on auto-renew"}
              </Button>
            )}
            {sub.status === "Active" && (
              <Button variant="ghost" className="danger-t" onClick={() => setConfirmCancel(true)}>
                Cancel subscription
              </Button>
            )}
            {sub.status === "Cancelling" && (
              <Button variant="ghost" disabled={busy} onClick={() => act(() => companyApi.resume(), "Subscription resumed")}>
                Keep subscription
              </Button>
            )}
          </div>
        )}
      </section>

      <section className="dc" style={{ margin: "18px 0" }}>
        <div className="dc-h">
          <BarChart3 />
          <h2>Usage</h2>
        </div>
        <div className="meters">
          <Meter label="Vehicles in use" used={sub.usage.vehicles} limit={sub.usage.vehicleLimit} />
          <Meter label="Seats in use" used={sub.usage.seats} limit={sub.usage.seatLimit} />
          <Meter
            label="Storage"
            used={sub.usage.storageBytes / 1024 ** 3}
            limit={sub.usage.storageLimitBytes / 1024 ** 3}
            unit=" GB"
            display={gb(sub.usage.storageBytes)}
          />
        </div>
      </section>

      <div className="between" style={{ margin: "26px 0 14px" }}>
        <h2 ref={plansRef} style={{ fontSize: 18 }}>
          Plans
        </h2>
        <div className="tog">
          <button type="button" className={cyc === "monthly" ? "on" : ""} onClick={() => setCycle("monthly")}>
            Monthly
          </button>
          <button type="button" className={cyc === "annual" ? "on" : ""} onClick={() => setCycle("annual")}>
            Annual <em className="save">−20%</em>
          </button>
        </div>
      </div>
      <div className="plans">
        {sub.plans.map((p) => {
          const isCurrent = !ended && p.key === sub.planKey && cyc === sub.billingCycle;
          const samePlan = p.key === sub.planKey;
          const up = rank(p.key) > rank(sub.planKey);
          const label = ended ? "Choose plan" : samePlan ? `Switch to ${cyc}` : up ? "Upgrade" : "Downgrade";
          return (
            <div key={p.key} className={`plan ${!ended && samePlan ? "cur" : ""} ${p.popular ? "pop" : ""}`}>
              {p.popular && <span className="pop-t">Most popular</span>}
              <h3>{p.name}</h3>
              <div className="pl-p">
                EGP {nf(price(p, cyc))}
                <small> / {cyc === "annual" ? "year" : "month"}</small>
              </div>
              <ul>
                {p.features.map((f) => (
                  <li key={f}>
                    <Check />
                    {f}
                  </li>
                ))}
              </ul>
              {isCurrent ? (
                <button className="btn" disabled>
                  Current plan
                </button>
              ) : isAdmin ? (
                <Button variant={up || ended ? "primary" : "secondary"} onClick={() => setPick(p)}>
                  {label}
                </Button>
              ) : null}
            </div>
          );
        })}
      </div>

      <ConfirmDialog
        open={!!pick}
        title={pick ? `Switch to ${pick.name}` : ""}
        message={
          pick
            ? `Your plan changes to ${pick.name} at EGP ${nf(price(pick, cyc))} per ${cyc === "annual" ? "year" : "month"}, effective immediately. A new billing period starts today and an invoice for the full amount is issued${
                current && !ended ? ` (the unused part of ${current.name} is not credited)` : ""
              }.`
            : ""
        }
        confirmLabel="Confirm change"
        tone="primary"
        onClose={() => setPick(null)}
        onConfirm={async () => {
          const p = pick!;
          setPick(null);
          await act(() => companyApi.changePlan(p.key, cyc), `You are now on ${p.name}`);
          setCycle(null);
        }}
      />
      <ConfirmDialog
        open={confirmCancel}
        title="Cancel subscription"
        message={`Your plan stays active until ${formatDay(sub.currentPeriodEnd)}, then the subscription ends. You can keep it any time before then.`}
        confirmLabel="Cancel subscription"
        onClose={() => setConfirmCancel(false)}
        onConfirm={async () => {
          setConfirmCancel(false);
          await act(() => companyApi.cancel(), "Subscription set to cancel at period end");
        }}
      />
    </>
  );
}

// =====================================================================
// Billing
// =====================================================================
function cardBrand(digits: string) {
  if (/^4/.test(digits)) return "Visa";
  if (/^(5[1-5]|2(2[2-9]|[3-6]\d|7[01]|720))/.test(digits)) return "Mastercard";
  if (/^3[47]/.test(digits)) return "Amex";
  if (/^507803/.test(digits)) return "Meeza";
  return "Card";
}
function luhn(digits: string) {
  let sum = 0;
  for (let i = 0; i < digits.length; i++) {
    let d = Number(digits[digits.length - 1 - i]);
    if (i % 2 === 1) {
      d *= 2;
      if (d > 9) d -= 9;
    }
    sum += d;
  }
  return sum % 10 === 0;
}

function BillingTab() {
  const toast = useToast();
  const load = useCallback(() => companyApi.billing(), []);
  const { data: bill, setData, error, reload } = useLoad(load);
  const [cardOpen, setCardOpen] = useState(false);
  const [mailOpen, setMailOpen] = useState(false);
  const [opening, setOpening] = useState<string | null>(null);

  if (error) return <LoadError message={error} onRetry={reload} />;
  if (!bill) return <PageSpinner />;

  const download = async (id: string) => {
    setOpening(id);
    try {
      await companyApi.openInvoice(id);
    } catch (e) {
      toast(e instanceof Error ? e.message : "Could not open the invoice", "bad");
    } finally {
      setOpening(null);
    }
  };

  return (
    <div className="bill-grid">
      <section className="dc">
        <div className="dc-h">
          <CreditCard />
          <h2>Payment method</h2>
        </div>
        {bill.cardLast4 ? (
          <div className="cardvis">
            <div className="cv-t">{bill.cardBrand}</div>
            <div className="cv-n">•••• •••• •••• {bill.cardLast4}</div>
            <div className="cv-b">
              <span>{bill.cardHolder}</span>
              <span>{bill.cardExpiry}</span>
            </div>
          </div>
        ) : (
          <p className="t-sub">No card on file.</p>
        )}
        <Button variant="secondary" size="sm" style={{ marginTop: 14 }} onClick={() => setCardOpen(true)}>
          {bill.cardLast4 ? "Update card" : "Add card"}
        </Button>
        <div className="divider" />
        <div className="fs-t">Billing contact</div>
        <div className="between">
          <span>{bill.billingEmail ?? "Not set"}</span>
          <Button variant="ghost" size="sm" onClick={() => setMailOpen(true)}>
            Edit
          </Button>
        </div>
      </section>

      <section className="card">
        <div className="card-h">
          <h2>Invoices</h2>
        </div>
        <div className="tbl-wrap">
          <table>
            <thead>
              <tr>
                <th>Invoice</th>
                <th>Date</th>
                <th>Description</th>
                <th className="num">Amount</th>
                <th>Status</th>
                <th style={{ width: 140 }} />
              </tr>
            </thead>
            <tbody>
              {bill.invoices.length === 0 && (
                <tr>
                  <td colSpan={6}>
                    <div className="empty" style={{ padding: "24px 0" }}>
                      <h3>No invoices yet</h3>
                    </div>
                  </td>
                </tr>
              )}
              {bill.invoices.map((i) => (
                <tr key={i.id}>
                  <td className="t-main">{i.code}</td>
                  <td>{formatDay(i.issueDate)}</td>
                  <td>{i.description}</td>
                  <td className="num">
                    {i.currency} {nf(i.amount)}
                  </td>
                  <td>
                    <span className={`badge ${i.status === "Paid" ? "ok" : i.status === "Void" ? "plain" : "warn"}`}>{i.status}</span>
                  </td>
                  <td style={{ textAlign: "right" }}>
                    <Button variant="ghost" size="sm" disabled={opening === i.id} onClick={() => download(i.id)}>
                      <Download /> Download
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <CardModal
        open={cardOpen}
        bill={bill}
        onClose={() => setCardOpen(false)}
        onSaved={(b) => {
          setData(b);
          setCardOpen(false);
        }}
      />
      <EmailModal
        open={mailOpen}
        bill={bill}
        onClose={() => setMailOpen(false)}
        onSaved={(b) => {
          setData(b);
          setMailOpen(false);
        }}
      />
    </div>
  );
}

/** The full card number is only used in the browser to derive brand and last 4 digits; it is never sent to the API. */
function CardModal({ open, bill, onClose, onSaved }: { open: boolean; bill: BillingInfo; onClose: () => void; onSaved: (b: BillingInfo) => void }) {
  const toast = useToast();
  const [holder, setHolder] = useState("");
  const [num, setNum] = useState("");
  const [exp, setExp] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    if (open) {
      setHolder(bill.cardHolder ?? "");
      setNum("");
      setExp(bill.cardExpiry ?? "");
      setErrors({});
    }
  }, [open, bill]);

  const submit = async (e?: FormEvent) => {
    e?.preventDefault();
    const digits = num.replace(/\D/g, "");
    const errs: Record<string, string> = {};
    if (!holder.trim()) errs.holder = "Cardholder name is required.";
    if (digits.length < 12 || digits.length > 19 || !luhn(digits)) errs.cardNumber = "Enter a valid card number.";
    if (!/^(0[1-9]|1[0-2])\/\d{2}$/.test(exp.trim())) errs.expiry = "Enter the expiry as MM/YY.";
    setErrors(errs);
    if (Object.keys(errs).length) return;
    setBusy(true);
    try {
      const res = await companyApi.setPaymentMethod({ brand: cardBrand(digits), last4: digits.slice(-4), expiry: exp.trim(), holder: holder.trim() });
      setNum("");
      toast("Payment card updated");
      onSaved(res);
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.details).length) setErrors(err.details);
      toast(err instanceof Error ? err.message : "Could not save the card", "bad");
    } finally {
      setBusy(false);
    }
  };
  const errText = (k: string) =>
    errors[k] ? (
      <span className="hint" style={{ color: "var(--red)" }}>
        {errors[k]}
      </span>
    ) : null;
  const red = (k: string) => (errors[k] ? { borderColor: "var(--red)" } : undefined);

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Update payment card"
      description="Only the card brand, last 4 digits and expiry are saved."
      size="slim"
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={busy}>
            Cancel
          </Button>
          <Button onClick={() => submit()} disabled={busy}>
            {busy ? "Saving…" : "Save card"}
          </Button>
        </>
      }
    >
      <form className="form-grid" style={{ gridTemplateColumns: "1fr" }} onSubmit={submit} noValidate autoComplete="off">
        <div className="f">
          <label htmlFor="cc-holder">
            Name on card<span className="req"> *</span>
          </label>
          <input id="cc-holder" value={holder} onChange={(e) => setHolder(e.target.value)} style={red("holder")} />
          {errText("holder")}
        </div>
        <div className="f">
          <label htmlFor="cc-num">
            Card number<span className="req"> *</span>
          </label>
          <input
            id="cc-num"
            inputMode="numeric"
            autoComplete="off"
            placeholder="1234 5678 9012 3456"
            value={num}
            onChange={(e) => setNum(e.target.value.replace(/[^\d ]/g, "").slice(0, 23))}
            style={red("cardNumber")}
          />
          {errText("cardNumber")}
        </div>
        <div className="f">
          <label htmlFor="cc-exp">
            Expiry<span className="req"> *</span>
          </label>
          <input id="cc-exp" placeholder="MM/YY" value={exp} onChange={(e) => setExp(e.target.value.slice(0, 5))} style={red("expiry")} />
          {errText("expiry")}
        </div>
        <button type="submit" hidden />
      </form>
    </Modal>
  );
}

function EmailModal({ open, bill, onClose, onSaved }: { open: boolean; bill: BillingInfo; onClose: () => void; onSaved: (b: BillingInfo) => void }) {
  const toast = useToast();
  const [email, setEmail] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    if (open) {
      setEmail(bill.billingEmail ?? "");
      setError("");
    }
  }, [open, bill]);
  const submit = async (e?: FormEvent) => {
    e?.preventDefault();
    setBusy(true);
    try {
      const res = await companyApi.setBillingEmail(email);
      toast("Billing contact updated");
      onSaved(res);
    } catch (err) {
      setError(err instanceof ApiError ? (err.details.email ?? err.message) : "Could not save");
    } finally {
      setBusy(false);
    }
  };
  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Billing contact"
      size="slim"
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={busy}>
            Cancel
          </Button>
          <Button onClick={() => submit()} disabled={busy}>
            {busy ? "Saving…" : "Save"}
          </Button>
        </>
      }
    >
      <form className="form-grid" style={{ gridTemplateColumns: "1fr" }} onSubmit={submit} noValidate>
        <div className="f">
          <label htmlFor="bill-mail">
            Invoices are sent to<span className="req"> *</span>
          </label>
          <input
            id="bill-mail"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            style={error ? { borderColor: "var(--red)" } : undefined}
          />
          {error && (
            <span className="hint" style={{ color: "var(--red)" }}>
              {error}
            </span>
          )}
        </div>
        <button type="submit" hidden />
      </form>
    </Modal>
  );
}
