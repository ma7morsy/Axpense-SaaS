import { useCallback, useEffect, useMemo, useState } from "react";
import { BarChart3, ChevronDown, Coins, Eye, Pencil, Plus, Trash2 } from "lucide-react";
import { PageHeader } from "../components/ui/PageHeader";
import { Button } from "../components/ui/Button";
import { Modal } from "../components/ui/Modal";
import { RowMenu } from "../components/ui/RowMenu";
import { ConfirmDialog } from "../components/ui/ConfirmDialog";
import { PageSpinner } from "../components/ui/Spinner";
import { useToast } from "../components/ui/Toast";
import { RecordFormModal, type FormValues } from "../components/vehicles/RecordFormModal";
import { budgetsApi } from "../lib/api";
import type { BudgetDetail, BudgetList, BudgetOptions, BudgetRow } from "../lib/types";
import { useIsAdmin } from "./admin/useIsAdmin";
import "./budgets.css";

const MONTHS = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
const nf = (n: number) => n.toLocaleString("en-US", { maximumFractionDigits: 0 });
const money = (n: number) => `EGP ${nf(n)}`;
const tone = (s: string) => (s === "Over" ? "bad" : s === "At risk" ? "warn" : "ok");

/**
 * Budgets: monthly spending limits (overall or per expense type) with actual spend, status and a per-budget summary.
 * Actual spend and every rule come from /api/budgets (BudgetLimitService).
 */
export default function BudgetsPage() {
  const { isAdmin } = useIsAdmin();
  const toast = useToast();
  const now = new Date();
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState<number | null>(now.getMonth() + 1);
  const [data, setData] = useState<BudgetList | null>(null);
  const [options, setOptions] = useState<BudgetOptions | null>(null);
  const [error, setError] = useState("");
  const [form, setForm] = useState<{ budget: BudgetRow | null } | null>(null);
  const [removing, setRemoving] = useState<BudgetRow | null>(null);
  const [summaryId, setSummaryId] = useState<string | null>(null);

  const load = useCallback(() => {
    setError("");
    return budgetsApi
      .list(year, month)
      .then(setData)
      .catch((e) => setError(e instanceof Error ? e.message : "Could not load budgets"));
  }, [year, month]);
  useEffect(() => {
    setData(null);
    load();
  }, [load]);
  useEffect(() => {
    budgetsApi.options().then(setOptions).catch(() => undefined);
  }, []);

  const years = useMemo(() => [now.getFullYear() - 2, now.getFullYear() - 1, now.getFullYear(), now.getFullYear() + 1], [now]);
  const t = data?.totals;

  return (
    <div>
      <PageHeader
        eyebrow="Cost control"
        title="Budgets"
        subtitle="Monthly spending limits and how actual spend tracks against them."
        action={
          isAdmin ? (
            <Button onClick={() => setForm({ budget: null })}>
              <Plus /> Add budget
            </Button>
          ) : undefined
        }
      />

      <div className="bd-filters">
        <label className="pillsel">
          <select aria-label="Year" value={year} onChange={(e) => setYear(Number(e.target.value))}>
            {years.map((y) => (
              <option key={y} value={y}>
                {y}
              </option>
            ))}
          </select>
          <ChevronDown className="cv" />
        </label>
        <label className="pillsel">
          <select aria-label="Month" value={month ?? ""} onChange={(e) => setMonth(e.target.value ? Number(e.target.value) : null)}>
            <option value="">All months</option>
            {MONTHS.map((m, i) => (
              <option key={m} value={i + 1}>
                {m}
              </option>
            ))}
          </select>
          <ChevronDown className="cv" />
        </label>
        <span className="t-sub">{month ? `${MONTHS[month - 1]} ${year}` : `All of ${year}`}</span>
      </div>

      {error ? (
        <div className="card">
          <div className="empty">
            <h3>Unable to load budgets</h3>
            <p>{error}</p>
            <Button variant="secondary" onClick={load}>
              Try again
            </Button>
          </div>
        </div>
      ) : !data || !t ? (
        <PageSpinner />
      ) : (
        <>
          <div className="grid g-4" style={{ marginBottom: 18 }}>
            <div className="stat">
              <div className="k">Budgeted</div>
              <div className="v">{money(t.budgeted)}</div>
              <div className="m">{t.basis === "overall" ? "overall limits" : t.basis === "categories" ? "sum of category limits" : t.basis === "mixed" ? "overall where set, else categories" : "no limits set"}</div>
            </div>
            <div className={`stat ${t.utilizationPct != null && t.utilizationPct > 100 ? "bad" : ""}`}>
              <div className="k">Spent</div>
              <div className="v">{money(t.actual)}</div>
              <div className="m">{t.utilizationPct == null ? "—" : `${t.utilizationPct}% of budget`}</div>
            </div>
            <div className={`stat ${t.remaining < 0 ? "bad" : "neutral"}`}>
              <div className="k">Remaining</div>
              <div className="v">{money(t.remaining)}</div>
              <div className="m">{t.remaining < 0 ? "over the limit" : "left to spend"}</div>
            </div>
            <div className={`stat ${t.over ? "bad" : t.atRisk ? "warn" : ""}`}>
              <div className="k">Over / at risk</div>
              <div className="v">
                {t.over} / {t.atRisk}
              </div>
              <div className="m">of {t.count} budget{t.count === 1 ? "" : "s"}</div>
            </div>
          </div>

          <div className="card">
            <div className="card-h">
              <div>
                <h2>Budget lines</h2>
                <div className="t-sub">Actual spend = expenses + fuel + completed work orders in the budget's month.</div>
              </div>
            </div>
            {data.rows.length === 0 ? (
              <div className="empty">
                <Coins />
                <h3>No budgets for this period</h3>
                <p>Add a monthly limit to start tracking spend against it.</p>
                {isAdmin && (
                  <div style={{ marginTop: 14 }}>
                    <Button onClick={() => setForm({ budget: null })}>
                      <Plus /> Add budget
                    </Button>
                  </div>
                )}
              </div>
            ) : (
              <div className="tbl-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Budget</th>
                      <th>Category</th>
                      <th>Period</th>
                      <th className="num">Limit</th>
                      <th className="num">Spent</th>
                      <th style={{ width: 200 }}>Utilization</th>
                      <th className="num">Remaining</th>
                      <th>Status</th>
                      <th />
                    </tr>
                  </thead>
                  <tbody>
                    {data.rows.map((r) => (
                      <tr key={r.id} className="clickable" onClick={() => setSummaryId(r.id)}>
                        <td className="t-main">{r.name}</td>
                        <td>
                          <span className="bd-cat">
                            <i style={{ background: r.color ?? "#B8C4D0" }} />
                            {r.category}
                          </span>
                        </td>
                        <td>{r.period}</td>
                        <td className="num">{money(r.limitAmount)}</td>
                        <td className="num">{money(r.actual)}</td>
                        <td>
                          <div className="bd-util">
                            <div className={`bar-mini ${tone(r.status) === "ok" ? "" : tone(r.status)}`}>
                              <i style={{ width: `${Math.min(100, r.utilizationPct)}%` }} />
                            </div>
                            <span className="mono">{r.utilizationPct}%</span>
                          </div>
                        </td>
                        <td className="num" style={{ color: r.remaining < 0 ? "var(--red)" : undefined }}>
                          {money(r.remaining)}
                        </td>
                        <td>
                          <span className={`badge ${tone(r.status)}`}>{r.status}</span>
                        </td>
                        <td>
                          <RowMenu
                            items={[
                              { label: "View summary", icon: Eye, onSelect: () => setSummaryId(r.id) },
                              ...(isAdmin
                                ? [
                                    { label: "Edit budget", icon: Pencil, onSelect: () => setForm({ budget: r }) },
                                    { separator: true as const },
                                    { label: "Delete budget", icon: Trash2, danger: true, onSelect: () => setRemoving(r) },
                                  ]
                                : []),
                            ]}
                          />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </>
      )}

      <RecordFormModal
        open={!!form}
        title={form?.budget ? "Edit Budget" : "Add Budget"}
        submitLabel="Save Budget"
        size="slim"
        fields={[
          { name: "name", label: "Budget name", req: true, full: true, placeholder: "e.g. Fleet operations" },
          { name: "category", label: "Category", type: "select", req: true, options: (options?.categories ?? [{ value: "Overall", label: "Overall", color: null }]).map((c) => ({ value: c.value, label: c.value })) },
          { name: "limitAmount", label: "Limit amount", type: "number", req: true, step: "0.01", placeholder: "EGP" },
          { name: "year", label: "Year", type: "number", req: true },
          { name: "month", label: "Month", type: "select", req: true, options: MONTHS.map((m, i) => ({ value: String(i + 1), label: m })) },
        ]}
        initial={
          {
            name: form?.budget?.name ?? "",
            category: form?.budget?.category ?? "Overall",
            limitAmount: form?.budget?.limitAmount?.toString() ?? "",
            year: String(form?.budget?.year ?? year),
            month: String(form?.budget?.month ?? month ?? now.getMonth() + 1),
          } as FormValues
        }
        onClose={() => setForm(null)}
        onSubmit={async (body) => {
          const payload = { ...body, month: Number(body.month) };
          if (form?.budget) await budgetsApi.update(form.budget.id, payload);
          else await budgetsApi.create(payload);
          toast(form?.budget ? "Budget updated" : "Budget added");
          setForm(null);
          await load();
        }}
      />
      <ConfirmDialog
        open={!!removing}
        title="Delete budget"
        message={`Delete "${removing?.name ?? ""}" (${removing?.category ?? ""}, ${removing?.period ?? ""})? Spend records are not affected.`}
        onClose={() => setRemoving(null)}
        onConfirm={async () => {
          try {
            await budgetsApi.remove(removing!.id);
            toast("Budget deleted");
            await load();
          } catch (e) {
            toast(e instanceof Error ? e.message : "Could not delete the budget", "bad");
          } finally {
            setRemoving(null);
          }
        }}
      />
      <BudgetSummaryModal id={summaryId} onClose={() => setSummaryId(null)} />
    </div>
  );
}

/** Per-budget summary: headline figures, spend vs limit over the month, breakdown and largest entries. */
function BudgetSummaryModal({ id, onClose }: { id: string | null; onClose: () => void }) {
  const [d, setD] = useState<BudgetDetail | null>(null);
  const [error, setError] = useState("");
  useEffect(() => {
    if (!id) return;
    setD(null);
    setError("");
    budgetsApi
      .get(id)
      .then(setD)
      .catch((e) => setError(e instanceof Error ? e.message : "Could not load the summary"));
  }, [id]);

  const b = d?.budget;
  return (
    <Modal open={!!id} onClose={onClose} title={b ? b.name : "Budget summary"} description={b ? `${b.category} · ${b.period}` : undefined} size="wide"
      footer={<Button variant="secondary" onClick={onClose}>Close</Button>}>
      {error ? (
        <div className="empty">
          <h3>Unable to load</h3>
          <p>{error}</p>
        </div>
      ) : !d || !b ? (
        <PageSpinner />
      ) : (
        <div className="bd-sum">
          <div className="grid g-4">
            <div className="stat">
              <div className="k">Limit</div>
              <div className="v">{money(b.limitAmount)}</div>
              <div className="m">{money(d.dailyAllowance)} per day</div>
            </div>
            <div className={`stat ${b.status === "Over" ? "bad" : ""}`}>
              <div className="k">Spent</div>
              <div className="v">{money(b.actual)}</div>
              <div className="m">{b.utilizationPct}% used</div>
            </div>
            <div className={`stat ${b.remaining < 0 ? "bad" : "neutral"}`}>
              <div className="k">Remaining</div>
              <div className="v">{money(b.remaining)}</div>
              <div className="m">
                {d.daysElapsed >= d.daysInMonth ? "month closed" : d.daysElapsed === 0 ? "month not started" : `${d.daysInMonth - d.daysElapsed} days left`}
              </div>
            </div>
            <div className={`stat ${b.status === "Over" ? "bad" : b.status === "At risk" ? "warn" : ""}`}>
              <div className="k">Status</div>
              <div className="v" style={{ fontSize: 22 }}>{b.status}</div>
              <div className="m">{b.projected != null ? `projected ${money(b.projected)} by month end` : "—"}</div>
            </div>
          </div>

          <div className="bd-sec">
            <div className="bd-sec-h">
              <BarChart3 /> Spend against the limit
            </div>
            <CumulativeChart d={d} />
          </div>

          <div className="bd-two">
            <div className="bd-sec">
              <div className="bd-sec-h">{b.expenseTypeId ? "Where it came from" : "By category"}</div>
              {d.breakdown.length === 0 ? (
                <p className="t-sub">No spend recorded yet.</p>
              ) : (
                d.breakdown.map((x) => (
                  <div className="bd-brk" key={x.name}>
                    <span>
                      <i style={{ background: x.color }} />
                      {x.name}
                    </span>
                    <div className="bar-mini">
                      <i style={{ width: `${x.percent}%`, background: x.color }} />
                    </div>
                    <b>{money(x.amount)}</b>
                    <small>{x.percent}%</small>
                  </div>
                ))
              )}
            </div>
            <div className="bd-sec">
              <div className="bd-sec-h">Largest entries</div>
              {d.topEntries.length === 0 ? (
                <p className="t-sub">No entries.</p>
              ) : (
                <table className="bd-top">
                  <tbody>
                    {d.topEntries.map((e, i) => (
                      <tr key={i}>
                        <td className="t-sub mono">{e.date.slice(8, 10)}/{e.date.slice(5, 7)}</td>
                        <td>
                          <div className="t-main" style={{ fontSize: 13 }}>{e.description}</div>
                          <div className="t-sub">
                            {e.kind}
                            {e.plate ? ` · ${e.plate}` : ""}
                          </div>
                        </td>
                        <td className="num">{money(e.amount)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        </div>
      )}
    </Modal>
  );
}

/** Cumulative spend by day (area) against the limit (dashed line) and the even-pace line. */
function CumulativeChart({ d }: { d: BudgetDetail }) {
  const W = 640, H = 180, P = 8;
  const limit = d.budget.limitAmount;
  const shown = d.daysElapsed > 0 ? d.daily.slice(0, Math.max(1, d.daysElapsed)) : [];
  const max = Math.max(limit * 1.1, ...shown.map((x) => x.cumulative), 1);
  const x = (i: number) => P + (i / Math.max(1, d.daysInMonth - 1)) * (W - 2 * P);
  const y = (v: number) => H - P - (v / max) * (H - 2 * P);
  const line = shown.map((p, i) => `${i === 0 ? "M" : "L"}${x(i).toFixed(1)},${y(p.cumulative).toFixed(1)}`).join(" ");
  const over = shown.length && shown[shown.length - 1].cumulative > limit;
  return (
    <div className="bd-chart">
      <svg viewBox={`0 0 ${W} ${H}`} preserveAspectRatio="none" aria-label="Cumulative spend against the limit">
        <line x1={P} x2={W - P} y1={y(limit)} y2={y(limit)} stroke="#C0392B" strokeDasharray="6 5" strokeWidth="1.5" vectorEffect="non-scaling-stroke" />
        <line x1={x(0)} y1={y(0)} x2={x(d.daysInMonth - 1)} y2={y(limit)} stroke="#B8C4D0" strokeDasharray="3 4" strokeWidth="1" vectorEffect="non-scaling-stroke" />
        {shown.length > 0 && (
          <>
            <path d={`${line} L${x(shown.length - 1).toFixed(1)},${y(0)} L${x(0)},${y(0)} Z`} fill={over ? "rgba(192,57,43,.12)" : "rgba(15,162,154,.14)"} />
            <path d={line} fill="none" stroke={over ? "#C0392B" : "#0FA29A"} strokeWidth="2.4" vectorEffect="non-scaling-stroke" />
          </>
        )}
      </svg>
      <div className="bd-legend">
        <span><i style={{ background: over ? "#C0392B" : "#0FA29A" }} />Cumulative spend</span>
        <span><i className="dash red" />Limit {money(limit)}</span>
        <span><i className="dash" />Even pace</span>
        <span className="t-sub" style={{ marginLeft: "auto" }}>Day 1 – {d.daysInMonth}</span>
      </div>
    </div>
  );
}
