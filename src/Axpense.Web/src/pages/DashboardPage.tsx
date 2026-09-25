import { useCallback, useEffect, useMemo, useState, type CSSProperties, type ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import {
  AlertTriangle,
  ArrowDown,
  ArrowUp,
  Bell,
  CalendarDays,
  Car,
  ChevronDown,
  ChevronRight,
  CircleDot,
  Coins,
  Cog,
  FileText,
  Fuel,
  Gauge,
  Info,
  Leaf,
  LineChart,
  MapPin,
  Receipt,
  Shield,
  Wallet,
  Wrench,
} from "lucide-react";
import { PageSpinner } from "../components/ui/Spinner";
import { Button } from "../components/ui/Button";
import { BudgetTracks, Donut, GROUP_STYLE, MapSvg, StackedChart, TrendChart, cmp, egp, pctFmt } from "../components/dashboard/dashCharts";
import { dashboardApi, notificationsApi, trackingApi } from "../lib/api";
import type {
  BudgetVsActual,
  DashTrendPoint,
  DashboardSummary,
  FleetTracking,
  NotificationItem,
  OperatingTrend,
  RecurrenceRow,
  UpcomingMaintenance,
} from "../lib/types";
import logo from "../assets/axpense-logo.png";

/**
 * Dashboard — fleet overview and key performance indicators, laid out as the reference design.
 * Every number comes from /api/dashboard/* (see DashboardService for the definitions); the page only renders.
 */

const pad = (n: number) => String(n).padStart(2, "0");
const iso = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
const fmtLong = (d: Date) => d.toLocaleDateString("en-GB", { day: "numeric", month: "short", year: "numeric" });

/** The last N complete months, like the reference range picker. */
function rangeOf(n: number) {
  const now = new Date();
  const from = new Date(now.getFullYear(), now.getMonth() - n, 1);
  const to = new Date(now.getFullYear(), now.getMonth(), 0);
  return { from: iso(from), to: iso(to), label: `${fmtLong(from)} – ${fmtLong(to)}` };
}
const RANGES = [1, 3, 6, 12];

/** Loader with its own loading/error state, so one failing panel does not blank the page. */
function usePanel<T>(load: () => Promise<T>) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const run = useCallback(() => {
    setLoading(true);
    setError("");
    load()
      .then(setData)
      .catch((e) => setError(e instanceof Error ? e.message : "Could not load"))
      .finally(() => setLoading(false));
  }, [load]);
  useEffect(run, [run]);
  return { data, error, loading, reload: run };
}

function PanelState({ loading, error, onRetry, h = 180 }: { loading: boolean; error: string; onRetry: () => void; h?: number }) {
  if (loading) return <div className="sk" style={{ height: h, width: "100%" }} />;
  return (
    <div className="empty" style={{ padding: "26px 0" }}>
      <h3>Unable to load</h3>
      <p>{error}</p>
      <div style={{ marginTop: 12 }}>
        <Button variant="secondary" size="sm" onClick={onRetry}>
          Try again
        </Button>
      </div>
    </div>
  );
}

function Delta({ pct, badWhenUp }: { pct: number | null; badWhenUp: boolean }) {
  if (pct == null) return <div className="dk-d flat">— no prior data</div>;
  const up = pct > 0.05, dn = pct < -0.05;
  const cls = !up && !dn ? "flat" : up !== badWhenUp ? "good" : "bad";
  return (
    <div className={`dk-d ${cls}`}>
      {up ? <ArrowUp /> : dn ? <ArrowDown /> : <span style={{ width: 16, textAlign: "center" }}>–</span>}
      {Math.abs(pct).toFixed(0)}%
    </div>
  );
}

function KpiCard({ icon, tone, label, value, delta, sub }: { icon: ReactNode; tone: string; label: string; value: ReactNode; delta: ReactNode; sub: string }) {
  return (
    <div className="dk">
      <span className={`dk-ic ${tone}`}>{icon}</span>
      <div className="dk-t">
        <div className="dk-l">{label}</div>
        <div className="dk-v">{value}</div>
        {delta}
        <div className="dk-s">{sub}</div>
      </div>
    </div>
  );
}
const Money = ({ n }: { n: number }) => (
  <>
    <span className="cur">EGP</span>
    {cmp(n)}
  </>
);

const EXP_ICON: Record<string, [ReactNode, string]> = {
  Fuel: [<Fuel key="f" />, ""],
  Maintenance: [<Wrench key="w" />, "blue"],
  Parts: [<Cog key="p" />, "blue"],
  Tires: [<CircleDot key="t" />, "blue"],
  Insurance: [<Shield key="s" />, ""],
  Other: [<Coins key="o" />, ""],
};

function noteKind(n: NotificationItem): [ReactNode, string] {
  if (n.severity === "Critical" || /overdue/i.test(n.title)) return [<AlertTriangle key="a" />, "red"];
  if (/maint|service|pm\b/i.test(n.type + n.title)) return [<Wrench key="w" />, ""];
  if (/licen|registration|insurance|expir|permit|document/i.test(n.type + n.title)) return [<FileText key="f" />, "blue"];
  return [<Info key="i" />, "blue"];
}
function ago(isoStr: string) {
  const d = new Date(isoStr);
  const days = Math.floor((new Date().setHours(0, 0, 0, 0) - new Date(d).setHours(0, 0, 0, 0)) / 86_400_000);
  const time = d.toLocaleTimeString("en-US", { hour: "2-digit", minute: "2-digit" });
  return days <= 0 ? `Today, ${time}` : days === 1 ? `Yesterday, ${time}` : `${days} days ago, ${time}`;
}
const dueText = (r: UpcomingMaintenance) =>
  r.days == null
    ? r.label ?? "Due"
    : r.days < 0 && r.late
      ? `${Math.abs(r.days)} day${Math.abs(r.days) === 1 ? "" : "s"} overdue`
      : r.days < 0
        ? "In progress"
      : r.days === 0
        ? "Due today"
        : r.days === 1
          ? "Due tomorrow"
          : `Due in ${r.days} days`;
const fday = (s: string) => new Date(s).toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" });

export default function DashboardPage() {
  const navigate = useNavigate();
  const [range, setRange] = useState(1);
  const [months, setMonths] = useState(6);
  const [gran, setGran] = useState<"month" | "week" | "day">("month");
  const [bgroup, setBgroup] = useState<"month" | "category">("month");
  const [pin, setPin] = useState<string | null>(null);
  const r = useMemo(() => rangeOf(range), [range]);

  const summary = usePanel<DashboardSummary>(useCallback(() => dashboardApi.summary(r.from, r.to), [r.from, r.to]));
  const trend = usePanel<DashTrendPoint[]>(useCallback(() => dashboardApi.costTrend(months), [months]));
  const op = usePanel<OperatingTrend>(useCallback(() => dashboardApi.operatingTrend(gran), [gran]));
  const budget = usePanel<BudgetVsActual>(useCallback(() => dashboardApi.budget(bgroup), [bgroup]));
  const recur = usePanel<{ rows: RecurrenceRow[]; totalParts: number }>(useCallback(() => dashboardApi.recurrence(7), []));
  const fleet = usePanel<FleetTracking>(useCallback(() => trackingApi.fleet(), []));
  const notes = usePanel<NotificationItem[]>(useCallback(() => notificationsApi.list(), []));

  if (summary.loading && !summary.data) return <PageSpinner />;
  if (!summary.data)
    return (
      <div className="card">
        <PanelState loading={false} error={summary.error} onRetry={summary.reload} />
      </div>
    );

  const S = summary.data;
  const k = S.kpis;
  const sub = range === 1 ? "vs. last month" : `vs. previous ${range} months`;
  const groups = S.groups.map((g) => ({ ...g, ...GROUP_STYLE[g.key], value: g.amount }));
  const total = k.total.value;

  // Fleet overview: live motion → On Route / Available / Maintenance.
  const items = fleet.data?.items ?? [];
  const stateOf = (m: string) =>
    m === "moving" ? { t: "On Route", c: "#19B394" } : m === "workshop" ? { t: "Maintenance", c: "#1F7BD8" } : { t: "Available", c: "#45C4B0" };
  const onRoute = items.filter((v) => v.motion === "moving").length;
  const maint = items.filter((v) => v.motion === "workshop").length;
  const avail = items.length - onRoute - maint;
  const lats = items.map((v) => v.latitude), lngs = items.map((v) => v.longitude);
  const [minLat, maxLat, minLng, maxLng] = [Math.min(...lats), Math.max(...lats), Math.min(...lngs), Math.max(...lngs)];
  const pins = items.map((v) => ({
    v,
    st: stateOf(v.motion),
    x: 10 + (maxLng > minLng ? ((v.longitude - minLng) / (maxLng - minLng)) * 80 : 40),
    y: 40 + (maxLat > minLat ? ((maxLat - v.latitude) / (maxLat - minLat)) * 50 : 25),
  }));
  const lead = pin ?? pins.find((p) => p.st.t === "On Route")?.v.id ?? pins[0]?.v.id;

  const trendLines = trend.data
    ? (["fuel", "maintenance", "expenses"] as const).map((key) => ({
        key,
        label: key === "fuel" ? "Fuel" : key === "maintenance" ? "Maintenance" : "Expenses",
        values: trend.data!.map((p) => p[key]),
      }))
    : [];

  const B = budget.data;
  const over = B?.rows.filter((x) => x.over) ?? [];

  return (
    <div>
      <div className="page-head dh">
        <div>
          <h1>Dashboard</h1>
          <div className="sub">Fleet overview and key performance indicators</div>
        </div>
        <div className="head-actions">
          <label className="range">
            <CalendarDays />
            <select aria-label="Date range" value={range} onChange={(e) => setRange(Number(e.target.value))}>
              {RANGES.map((n) => (
                <option key={n} value={n}>
                  {rangeOf(n).label}
                </option>
              ))}
            </select>
            <ChevronDown className="cv" />
          </label>
        </div>
      </div>

      <div className="dash-kpis">
        <KpiCard icon={<Car />} tone="" label="Total Vehicles" value={String(k.vehicles.value)} delta={<Delta pct={k.vehicles.changePct} badWhenUp={false} />} sub={sub} />
        <KpiCard
          icon={<Wrench />}
          tone="blue"
          label="Maintenance Due"
          value={String(k.maintenanceDue.due)}
          delta={
            k.maintenanceDue.overdue ? (
              <div className="dk-d bad">
                {k.maintenanceDue.overdue} <span style={{ marginLeft: ".3em" }}>overdue</span>
              </div>
            ) : (
              <div className="dk-d good">On schedule</div>
            )
          }
          sub={`${k.maintenanceDue.due} open or upcoming`}
        />
        <KpiCard icon={<Fuel />} tone="" label="Total Fuel Cost" value={<Money n={k.fuel.value} />} delta={<Delta pct={k.fuel.changePct} badWhenUp />} sub={sub} />
        <KpiCard icon={<Wallet />} tone="blue" label="Total Expenses" value={<Money n={k.expenses.value} />} delta={<Delta pct={k.expenses.changePct} badWhenUp />} sub={sub} />
        <KpiCard icon={<Coins />} tone="" label="Total Fleet Cost" value={<Money n={total} />} delta={<Delta pct={k.total.changePct} badWhenUp />} sub={sub} />
      </div>

      <div className="dr dr-a">
        <section className="dc">
          <div className="dc-h">
            <LineChart />
            <h2>Total Costs Trend</h2>
            <label className="pillsel">
              <select aria-label="Trend window" value={months} onChange={(e) => setMonths(Number(e.target.value))}>
                {[3, 6, 12].map((n) => (
                  <option key={n} value={n}>
                    Last {n} Months
                  </option>
                ))}
              </select>
              <ChevronDown className="cv" />
            </label>
            <div className="tc-legend">
              {trendLines.map((l) => (
                <b key={l.key}>
                  <i style={{ background: GROUP_STYLE[l.key].line }} />
                  {l.label}
                </b>
              ))}
            </div>
          </div>
          {trend.data ? (
            <TrendChart labels={trend.data.map((p) => p.label)} fulls={trend.data.map((p) => p.full)} lines={trendLines} />
          ) : (
            <PanelState loading={trend.loading} error={trend.error} onRetry={trend.reload} h={250} />
          )}
        </section>

        <section className="dc">
          <div className="dc-h">
            <Wallet />
            <h2>Cost Breakdown</h2>
          </div>
          <div className="dn-wrap">
            <Donut items={groups} total={total} uid="cb">
              <b>
                <small>EGP</small>
                {cmp(total)}
              </b>
              <span>Total Cost</span>
            </Donut>
            <div className="lg">
              {groups.map((g) => (
                <div className="lg-r" key={g.key}>
                  <i style={{ background: g.line }} />
                  <div>
                    <div className="n">{g.name}</div>
                    <div className="a">{egp(g.amount)}</div>
                  </div>
                  <div className="p">{total ? Math.round((g.amount / total) * 100) : 0}%</div>
                </div>
              ))}
            </div>
          </div>
        </section>
      </div>

      <div className="dr dr-b">
        <section className="dc">
          <div className="dc-h">
            <MapPin />
            <h2>Fleet Overview</h2>
            <span className="dc-link" onClick={() => navigate("/aerial")}>
              View all
            </span>
          </div>
          {fleet.data ? (
            <div className="fo">
              <div className="map">
                <MapSvg />
                {pins.map((p) => (
                  <div
                    key={p.v.id}
                    className={`pin ${p.v.id === lead ? "on" : ""}`}
                    style={{ left: `${p.x}%`, top: `${p.y}%`, "--pc": p.st.c } as CSSProperties}
                    onClick={() => setPin(p.v.id)}
                    onDoubleClick={() => navigate(`/vehicles/${p.v.id}`)}
                  >
                    <span className="pin-ic">
                      <Car />
                    </span>
                    <div className="pin-pop">
                      <b>{p.v.plateNumber}</b>
                      <small>{p.st.t}</small>
                    </div>
                  </div>
                ))}
              </div>
              <div className="fo-side">
                <Donut
                  uid="fo"
                  sw={13}
                  total={items.length}
                  items={[
                    { value: onRoute, c1: "#46DDB8", c2: "#14A98D" },
                    { value: avail, c1: "#7FD8E6", c2: "#3EB6CF" },
                    { value: maint, c1: "#4AA3F2", c2: "#1565C0" },
                  ]}
                >
                  <b>{items.length}</b>
                  <span>Total Vehicles</span>
                </Donut>
                <div className="fo-lg">
                  <div>
                    <i style={{ background: "#19B394" }} />
                    On Route<b>{onRoute}</b>
                  </div>
                  <div>
                    <i style={{ background: "#4FC3D8" }} />
                    Available<b>{avail}</b>
                  </div>
                  <div>
                    <i style={{ background: "#1F7BD8" }} />
                    Maintenance<b>{maint}</b>
                  </div>
                </div>
              </div>
            </div>
          ) : (
            <PanelState loading={fleet.loading} error={fleet.error} onRetry={fleet.reload} h={222} />
          )}
        </section>

        <section className="dc">
          <div className="dc-h">
            <Bell />
            <h2>Notifications</h2>
            <span className="dc-link" onClick={() => navigate("/notifications")}>
              View all
            </span>
          </div>
          <div className="nt-list">
            {!notes.data ? (
              <PanelState loading={notes.loading} error={notes.error} onRetry={notes.reload} h={200} />
            ) : notes.data.length === 0 ? (
              <div className="empty" style={{ padding: "26px 0" }}>
                <h3>All clear</h3>
                <p>No alerts right now.</p>
              </div>
            ) : (
              notes.data.slice(0, 4).map((n) => {
                const [ic, tone] = noteKind(n);
                return (
                  <div key={n.id} className="nt" onClick={() => navigate("/notifications")}>
                    <span className={`nt-ic ${tone}`}>{ic}</span>
                    <div className="nt-b">
                      <b title={n.title}>{n.title}</b>
                      <span>{n.message}</span>
                      <time>{ago(n.createdAt)}</time>
                    </div>
                  </div>
                );
              })
            )}
          </div>
        </section>
      </div>

      <div className="dr dr-c">
        <section className="dc">
          <div className="dc-h">
            <CalendarDays className="navy" />
            <h2>Upcoming Maintenance</h2>
            <span className="dc-link" onClick={() => navigate("/maintenance")}>
              View all
            </span>
          </div>
          <div>
            {S.upcoming.length === 0 ? (
              <div className="empty" style={{ padding: "26px 0" }}>
                <h3>Nothing scheduled</h3>
              </div>
            ) : (
              S.upcoming.slice(0, 4).map((u) => {
                const hi = u.priority === "Critical" || u.priority === "High" || u.late;
                const lo = u.priority === "Low";
                return (
                  <div key={u.id ?? `pm-${u.vehicleId}`} className="um" onClick={() => navigate(u.id ? "/maintenance" : `/vehicles/${u.vehicleId}`)}>
                    <span className="um-ic">
                      <Car />
                    </span>
                    <div style={{ minWidth: 0 }}>
                      <div className="um-n" title={`${u.vehicle} · ${u.plate}`}>
                        {u.vehicle}
                      </div>
                      <div className="um-t">{u.task}</div>
                    </div>
                    <span className={`um-d ${u.late ? "late" : ""}`}>{dueText(u)}</span>
                    <span className={`prio ${hi ? "high" : lo ? "low" : "med"}`}>{hi ? "High" : lo ? "Low" : "Medium"}</span>
                    <ChevronRight className="chev" />
                  </div>
                );
              })
            )}
          </div>
        </section>

        <section className="dc">
          <div className="dc-h">
            <Receipt />
            <h2>Recent Expenses</h2>
            <span className="dc-link" onClick={() => navigate("/expenses")}>
              View all
            </span>
          </div>
          <div>
            {S.recentExpenses.length === 0 ? (
              <div className="empty" style={{ padding: "26px 0" }}>
                <h3>No expenses yet</h3>
              </div>
            ) : (
              S.recentExpenses.map((x, i) => {
                const [ic, tone] = EXP_ICON[x.category] ?? EXP_ICON.Other;
                return (
                  <div className="ex" key={i}>
                    <span className={`ex-ic ${tone}`}>{ic}</span>
                    <div className="ex-b">
                      <b>{x.category}</b>
                      <span>{[x.plate, x.vendor].filter(Boolean).join(" · ")}</span>
                    </div>
                    <div className="ex-a">
                      <b>{egp(x.amount, true)}</b>
                      <span>{fday(x.date)}</span>
                    </div>
                  </div>
                );
              })
            )}
          </div>
        </section>
      </div>

      <div className="dr dr-full dash-an">
        <div className="card">
          <div className="card-h">
            <div>
              <h2>Operating Cost Trend</h2>
              <div className="t-sub">Stacked by category, compared with the preceding period of equal length</div>
            </div>
            <div style={{ marginLeft: "auto", display: "flex", gap: 8 }}>
              <div className="pill-toggle">
                {(["month", "week", "day"] as const).map((g) => (
                  <button key={g} type="button" className={gran === g ? "on" : ""} onClick={() => setGran(g)}>
                    {g === "month" ? "Monthly" : g === "week" ? "Weekly" : "Daily"}
                  </button>
                ))}
              </div>
            </div>
          </div>
          <div className="card-b">
            {!op.data ? (
              <PanelState loading={op.loading} error={op.error} onRetry={op.reload} h={230} />
            ) : op.data.buckets.some((b) => b.total > 0) ? (
              <StackedChart buckets={op.data.buckets} series={op.data.series} />
            ) : (
              <div className="empty">
                <LineChart />
                <h3>No spend in this window</h3>
                <p>Costs appear here as fuel, expenses and completed work orders are recorded.</p>
              </div>
            )}
          </div>
        </div>
      </div>

      <div className="dr dr-c dash-an">
        <div className="card">
          <div className="card-h">
            <div>
              <h2>Budget vs Actual</h2>
              <div className="t-sub">
                {bgroup === "month" ? "Monthly budget plan against actual spend" : "Annual budget by category against the last 12 months"}
              </div>
            </div>
            <div style={{ marginLeft: "auto" }}>
              <label className="pillsel">
                <select aria-label="Group by" value={bgroup} onChange={(e) => setBgroup(e.target.value as "month" | "category")}>
                  <option value="month">Group by: Month</option>
                  <option value="category">Group by: Category</option>
                </select>
                <ChevronDown className="cv" />
              </label>
            </div>
          </div>
          <div className="card-b">
            {!B ? (
              <PanelState loading={budget.loading} error={budget.error} onRetry={budget.reload} h={260} />
            ) : (
              <>
                <div className="grid g-4" style={{ gap: 10, marginBottom: 18 }}>
                  <div>
                    <div className="t-sub">Budget</div>
                    <div style={{ fontSize: 18, fontWeight: 700 }}>{egp(B.budget)}</div>
                  </div>
                  <div>
                    <div className="t-sub">Actual</div>
                    <div style={{ fontSize: 18, fontWeight: 700 }}>{egp(B.actual)}</div>
                  </div>
                  <div>
                    <div className="t-sub">Remaining</div>
                    <div style={{ fontSize: 18, fontWeight: 700, color: B.remaining < 0 ? "var(--red)" : "#067A52" }}>{egp(B.remaining)}</div>
                  </div>
                  <div>
                    <div className="t-sub">Utilization</div>
                    <div style={{ fontSize: 18, fontWeight: 700 }}>{B.utilizationPct == null ? "—" : `${B.utilizationPct.toFixed(1)}%`}</div>
                  </div>
                </div>
                {!B.hasBudget && (
                  <div className="banner" style={{ marginBottom: 14 }}>
                    <AlertTriangle />
                    <div>
                      <div className="bt">No budget set</div>
                      <div className="bd">
                        Set the annual budget in{" "}
                        <a onClick={() => navigate("/administration/settings?tab=budget")} style={{ cursor: "pointer", textDecoration: "underline" }}>
                          Settings → Budget
                        </a>{" "}
                        to compare spend against it.
                      </div>
                    </div>
                  </div>
                )}
                {over.length > 0 && (
                  <div className="banner warn" style={{ marginBottom: 14 }}>
                    <AlertTriangle />
                    <div>
                      <div className="bt">
                        {over.length} {bgroup === "month" ? "month" : over.length === 1 ? "category" : "categories"} over budget
                      </div>
                      <div className="bd">{over.map((x) => `${x.label} ${x.variancePct == null ? "" : pctFmt(x.variancePct, 0)}`).join(" · ")}</div>
                    </div>
                  </div>
                )}
                <BudgetTracks data={B} />
                <div className="t-sub" style={{ marginTop: 10 }}>
                  The marker on each track is the budget line.
                </div>
              </>
            )}
          </div>
        </div>

        <div className="card">
          <div className="card-h">
            <div>
              <h2>Recurrence by part</h2>
              <div className="t-sub">Parts that keep coming back are worth a design or supplier review.</div>
            </div>
            <span className="dc-link" onClick={() => navigate("/maintenance?tab=issues")}>
              View all
            </span>
          </div>
          <div className="tbl-wrap">
            <table>
              <thead>
                <tr>
                  <th>Part</th>
                  <th className="num">Times</th>
                  <th style={{ width: 140 }}>Frequency</th>
                  <th className="num">Vehicles</th>
                  <th>Flag</th>
                </tr>
              </thead>
              <tbody>
                {!recur.data ? (
                  <tr>
                    <td colSpan={5}>
                      <PanelState loading={recur.loading} error={recur.error} onRetry={recur.reload} h={160} />
                    </td>
                  </tr>
                ) : recur.data.rows.length === 0 ? (
                  <tr>
                    <td colSpan={5}>
                      <div className="empty" style={{ padding: "24px 0" }}>
                        <h3>No issues recorded</h3>
                      </div>
                    </td>
                  </tr>
                ) : (
                  (() => {
                    const maxR = Math.max(1, ...recur.data.rows.map((x) => x.times));
                    return recur.data.rows.map((x) => (
                      <tr key={x.partId ?? "unclassified"}>
                        <td className="t-main" title={x.category ?? undefined}>
                          {x.part}
                        </td>
                        <td className="num">{x.times}</td>
                        <td>
                          <div className={`bar-mini ${x.recurring ? "warn" : ""}`}>
                            <i style={{ width: `${(x.times / maxR) * 100}%` }} />
                          </div>
                        </td>
                        <td className="num">{x.vehicles}</td>
                        <td>{x.recurring ? <span className="badge warn">Recurring</span> : <span className="badge plain">One-off</span>}</td>
                      </tr>
                    ));
                  })()
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <div className="vs">
        {(
          [
            [<Shield key="s" />, "Improve", "Safety"],
            [<Coins key="c" />, "Reduce", "Costs"],
            [<Gauge key="g" />, "Increase", "Efficiency"],
            [<Leaf key="l" />, "Drive", "Sustainability"],
          ] as const
        ).map(([ic, a, b]) => (
          <div className="vs-i" key={b}>
            <span className="vs-ic">{ic}</span>
            <div>
              <small>{a}</small>
              <b>{b}</b>
            </div>
          </div>
        ))}
        <div className="vs-brand">
          <img className="vs-logo" src={logo} alt="Axpense" />
          <small>Your Fleet. Our Priority.</small>
        </div>
      </div>
    </div>
  );
}
