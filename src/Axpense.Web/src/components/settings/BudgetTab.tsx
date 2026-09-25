import { useCallback, useEffect, useState } from "react";
import { AlertTriangle, BarChart3, ChevronLeft, ChevronRight, Wallet } from "lucide-react";
import { Button } from "../ui/Button";
import { PageSpinner } from "../ui/Spinner";
import { useToast } from "../ui/Toast";
import { budgetApi } from "../../lib/api";
import type { AnnualBudget } from "../../lib/types";
import { formatMoney, formatMoneyCompact } from "../../lib/utils";
import "./settings.css";

const MONTHS = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

function Util({ pct }: { pct?: number | null }) {
  if (pct == null) return <span className="t-sub">no plan</span>;
  const tone = pct > 100 ? "bad" : pct > 90 ? "warn" : "";
  return (
    <div className="bg-util">
      <div className={`bar-mini ${tone}`}>
        <i style={{ width: `${Math.min(100, pct)}%` }} />
      </div>
      <span>{Math.round(pct)}%</span>
    </div>
  );
}

/** Number input that commits on blur / Enter, only when the value changed. */
function CommitInput({
  value,
  step,
  readOnly,
  label,
  onCommit,
  className = "bg-in",
}: {
  value: number;
  step?: number;
  readOnly?: boolean;
  label: string;
  onCommit: (v: number) => void;
  className?: string;
}) {
  const [text, setText] = useState(String(value));
  useEffect(() => setText(String(value)), [value]);
  const commit = () => {
    const n = Number(text);
    if (text.trim() === "" || Number.isNaN(n)) return setText(String(value));
    if (n !== value) onCommit(n);
  };
  return (
    <input
      className={className}
      type="number"
      min={0}
      step={step}
      aria-label={label}
      readOnly={readOnly}
      value={text}
      onChange={(e) => setText(e.target.value)}
      onBlur={commit}
      onKeyDown={(e) => e.key === "Enter" && (e.target as HTMLInputElement).blur()}
    />
  );
}

/** Settings → Budget: annual amount, monthly plan and category allocation for a year. */
export function BudgetTab({ isAdmin, onChanged }: { isAdmin: boolean; onChanged?: (b: AnnualBudget) => void }) {
  const toast = useToast();
  const [year, setYear] = useState(new Date().getFullYear());
  const [b, setB] = useState<AnnualBudget | null>(null);
  const [newAmount, setNewAmount] = useState("");
  const [busy, setBusy] = useState(false);

  const load = useCallback(() => {
    setB(null);
    budgetApi
      .get(year)
      .then((x) => {
        setB(x);
        setNewAmount(String(x.suggestedAmount || ""));
        onChanged?.(x);
      })
      .catch(() => toast("The budget could not be loaded", "bad"));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [year]);
  useEffect(load, [load]);

  const run = async (fn: () => Promise<AnnualBudget>, message?: string) => {
    setBusy(true);
    try {
      const x = await fn();
      setB(x);
      onChanged?.(x);
      if (message) toast(message);
    } catch (err) {
      toast(err instanceof Error ? err.message : "Could not update the budget", "bad");
    } finally {
      setBusy(false);
    }
  };

  const yearNav = (
    <div className="bg-year">
      <button className="act" aria-label="Previous year" onClick={() => setYear((y) => y - 1)}>
        <ChevronLeft />
      </button>
      <b>{year}</b>
      <button className="act" aria-label="Next year" onClick={() => setYear((y) => y + 1)}>
        <ChevronRight />
      </button>
      {busy && <span className="t-sub">Saving…</span>}
    </div>
  );

  if (!b) return (
    <>
      {yearNav}
      <PageSpinner />
    </>
  );

  if (!b.exists) {
    return (
      <>
        {yearNav}
        <div className="card">
          <div className="empty">
            <Wallet />
            <h3>No budget set for {year}</h3>
            <p>
              {b.suggestedAmount
                ? `Suggested: ${formatMoney(b.suggestedAmount)} — last year's spend plus 5%.`
                : "Set the annual figure; months split evenly and categories follow last year's mix."}
            </p>
            {isAdmin && (
              <div className="flex" style={{ justifyContent: "center", marginTop: 14 }}>
                <input
                  className="bg-in"
                  style={{ width: 200 }}
                  type="number"
                  min={0}
                  step={10000}
                  aria-label="Annual budget"
                  value={newAmount}
                  onChange={(e) => setNewAmount(e.target.value)}
                />
                <Button
                  disabled={busy || !newAmount}
                  onClick={() => run(() => budgetApi.setAnnual(year, Number(newAmount)), `Budget for ${year} created`)}
                >
                  Set budget
                </Button>
              </div>
            )}
          </div>
        </div>
      </>
    );
  }

  const drift = b.allocatedTotal - b.amount;
  const used = b.amount ? (b.actualTotal / b.amount) * 100 : 0;
  const shareOff = Math.abs(b.sharePercentTotal - 100) > 0.5;

  return (
    <>
      {yearNav}
      <div className="bg-grid">
        <div className="card">
          <div className="card-h">
            <h2>Annual budget</h2>
          </div>
          <div className="card-b">
            <div className="f" style={{ marginBottom: 16 }}>
              <label>Annual budget (EGP)</label>
              <div className="flex bg-annual" style={{ gap: 10 }}>
                <CommitInput
                  className="bg-in"
                  value={b.amount}
                  step={10000}
                  readOnly={!isAdmin}
                  label="Annual budget"
                  onCommit={(v) => run(() => budgetApi.setAnnual(year, v), `Annual budget set to ${formatMoney(v)} — months re-split evenly`)}
                />
              </div>
              <span className="hint">Changing it re-splits the months evenly.</span>
            </div>
            <dl className="kv">
              <dt>Monthly average</dt>
              <dd className="mono">{formatMoney(b.amount / 12)}</dd>
              <dt>Allocated across months</dt>
              <dd className="mono" style={{ color: Math.abs(drift) > 1 ? "var(--red)" : undefined }}>
                {formatMoney(b.allocatedTotal)}
              </dd>
              <dt>Unallocated</dt>
              <dd className="mono">{formatMoney(b.unallocated)}</dd>
              <dt>Spent to date</dt>
              <dd className="mono">{formatMoney(b.actualTotal)}</dd>
              <dt>Remaining</dt>
              <dd className="mono" style={{ color: b.amount - b.actualTotal < 0 ? "var(--red)" : "#067A52" }}>
                {formatMoney(b.amount - b.actualTotal)}
              </dd>
            </dl>
            <div className="divider" />
            <div className="fs-t">Utilization</div>
            <div className={`bar-mini ${used > 100 ? "bad" : used > 90 ? "warn" : ""}`} style={{ height: 10 }}>
              <i style={{ width: `${Math.min(100, used)}%` }} />
            </div>
            <div className="t-sub" style={{ marginTop: 7 }}>
              {used.toFixed(1)}% of the annual budget consumed
            </div>
          </div>
        </div>

        <div className="card">
          <div className="card-h">
            <div>
              <h2>Monthly breakdown</h2>
              <p className="t-sub">Even split by default — override any month and the rest stay as they are</p>
            </div>
            {isAdmin && (
              <div style={{ marginLeft: "auto", display: "flex", gap: 8 }}>
                <Button variant="secondary" size="sm" style={{ marginLeft: 0 }} onClick={() => run(() => budgetApi.splitEvenly(year), "Monthly plan split evenly")}>
                  Split evenly
                </Button>
                <Button
                  variant="secondary"
                  size="sm"
                  style={{ marginLeft: 0 }}
                  disabled={!b.hasPriorYearSpend}
                  title={b.hasPriorYearSpend ? undefined : `No spend recorded in ${year - 1}`}
                  onClick={() => run(() => budgetApi.weightByLastYear(year), `Monthly plan weighted by ${year - 1} spend`)}
                >
                  Weight by last year
                </Button>
              </div>
            )}
          </div>
          <div className="tbl-wrap">
            <table className="bg-table">
              <thead>
                <tr>
                  <th>Month</th>
                  <th className="num" style={{ width: 170 }}>
                    Allocation
                  </th>
                  <th className="num">Share</th>
                  <th className="num">Actual</th>
                  <th style={{ width: 170 }}>Utilization</th>
                </tr>
              </thead>
              <tbody>
                {b.months.map((m) => (
                  <tr key={m.month} className={b.currentMonth === m.month ? "cur" : ""}>
                    <td className="t-main">
                      {MONTHS[m.month - 1]} {b.currentMonth === m.month && <span className="badge ok">Current</span>}
                    </td>
                    <td>
                      <CommitInput
                        value={m.amount}
                        step={1000}
                        readOnly={!isAdmin}
                        label={`${MONTHS[m.month - 1]} allocation`}
                        onCommit={(v) => run(() => budgetApi.setMonth(year, m.month, v))}
                      />
                    </td>
                    <td className="num t-sub">{m.sharePercent.toFixed(1)}%</td>
                    <td className="num mono">{m.actual ? formatMoneyCompact(m.actual) : "—"}</td>
                    <td>{m.actual ? <Util pct={m.utilizationPercent} /> : <span className="t-sub">no spend</span>}</td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr>
                  <td>Total allocated</td>
                  <td className="num mono" style={{ color: Math.abs(drift) > 1 ? "var(--red)" : undefined }}>
                    {formatMoney(b.allocatedTotal)}
                  </td>
                  <td className="num t-sub">{b.amount ? ((b.allocatedTotal / b.amount) * 100).toFixed(0) : 0}%</td>
                  <td className="num mono">{formatMoneyCompact(b.actualTotal)}</td>
                  <td />
                </tr>
              </tfoot>
            </table>
          </div>
          {Math.abs(drift) > 1 && (
            <div className={`banner ${drift > 0 ? "bad" : "warn"}`} style={{ margin: "16px 22px" }}>
              <AlertTriangle />
              <div>
                <div className="bt">
                  Monthly plan is {drift > 0 ? "over" : "under"} the annual budget by {formatMoney(Math.abs(drift))}
                </div>
                <div className="bd">Adjust a month or press Split evenly to bring the plan back in line.</div>
              </div>
            </div>
          )}
        </div>
      </div>

      <div className="card">
        <div className="card-h">
          <div>
            <h2>Category allocation</h2>
            <p className="t-sub">Percentages of the annual budget per expense type — manage the types in the Expense types tab</p>
          </div>
          {isAdmin && (
            <Button
              variant="secondary"
              size="sm"
              disabled={!b.hasPriorYearSpend}
              title={b.hasPriorYearSpend ? undefined : `No spend recorded in ${year - 1}`}
              onClick={() => run(() => budgetApi.distributeFromLastYear(year), `Shares distributed from ${year - 1} actuals`)}
            >
              Distribute from last year
            </Button>
          )}
        </div>
        <div className="tbl-wrap">
          <table className="bg-table">
            <thead>
              <tr>
                <th>Category</th>
                <th className="num" style={{ width: 120 }}>
                  Share %
                </th>
                <th className="num">Budget</th>
                <th className="num">Actual · {year}</th>
                <th className="num">Remaining</th>
                <th style={{ width: 170 }}>Utilization</th>
              </tr>
            </thead>
            <tbody>
              {b.categories.map((c) => (
                <tr key={c.expenseTypeId}>
                  <td>
                    <div className="flex">
                      <i className="dotc" style={{ background: c.color }} />
                      <span className="t-main">{c.name}</span>
                    </div>
                  </td>
                  <td>
                    <CommitInput
                      value={c.sharePercent}
                      step={0.5}
                      readOnly={!isAdmin}
                      label={`${c.name} share`}
                      onCommit={(v) => run(() => budgetApi.setShare(year, c.expenseTypeId, v))}
                    />
                  </td>
                  <td className="num mono">{formatMoneyCompact(c.budget)}</td>
                  <td className="num mono" style={{ fontWeight: 600 }}>
                    {formatMoneyCompact(c.actual)}
                  </td>
                  <td className="num mono" style={{ color: c.remaining < 0 ? "var(--red)" : undefined }}>
                    {formatMoneyCompact(c.remaining)}
                  </td>
                  <td>
                    <Util pct={c.utilizationPercent} />
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <td>Total</td>
                <td className="num mono" style={{ color: shareOff ? "var(--red)" : undefined }}>
                  {b.sharePercentTotal.toFixed(1)}%
                </td>
                <td className="num mono">{formatMoneyCompact((b.amount * b.sharePercentTotal) / 100)}</td>
                <td className="num mono">{formatMoneyCompact(b.actualTotal)}</td>
                <td colSpan={2} />
              </tr>
            </tfoot>
          </table>
        </div>
        {shareOff && (
          <div className="banner bad" style={{ margin: "16px 22px" }}>
            <BarChart3 />
            <div>
              <div className="bt">Category shares add up to {b.sharePercentTotal.toFixed(1)}%</div>
              <div className="bd">They need to total 100% before the allocation is meaningful.</div>
            </div>
            {isAdmin && b.hasPriorYearSpend && (
              <Button variant="secondary" size="sm" onClick={() => run(() => budgetApi.distributeFromLastYear(year), "Shares fixed from last year's mix")}>
                Fix
              </Button>
            )}
          </div>
        )}
      </div>
    </>
  );
}
