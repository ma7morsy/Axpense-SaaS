import { useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { History } from "lucide-react";
import { Button } from "../../components/ui/Button";
import { PmGauge } from "../../components/vehicles/vehicleUi";
import { Bar, EmptyRow, ReportCard, StatGrid, fday, money, nf } from "../../components/reports/reportUi";
import type {
  BudgetReport,
  CostReport,
  DriverReport,
  ExpenseReport,
  FuelReport,
  IssueReport,
  OdometerReport,
  PmReport,
  UptimeReport,
  WorkOrderReport,
} from "./reportTypes";

const statusTone = (s: string) => (s === "Over" || s === "Overdue" ? "bad" : s === "At risk" || s === "In progress" ? "warn" : s === "Completed" ? "ok" : "info");

// =====================================================================
// Fuel
// =====================================================================
export function FuelTab({ d }: { d: FuelReport }) {
  const navigate = useNavigate();
  const [veh, setVeh] = useState("");
  const [q, setQ] = useState("");
  const maxCost = Math.max(1, ...d.byVehicle.map((r) => r.cost));
  const log = useMemo(
    () => d.log.filter((r) => (!veh || r.vehicleId === veh) && (!q || `${r.station ?? ""} ${r.plate} ${r.vehicle}`.toLowerCase().includes(q.toLowerCase()))),
    [d.log, veh, q]
  );
  const vehicles = useMemo(() => [...new Map(d.log.map((r) => [r.vehicleId, `${r.vehicle} · ${r.plate}`])).entries()], [d.log]);
  return (
    <>
      <StatGrid stats={d.stats} />
      <ReportCard
        title="Consumption by vehicle"
        note="Consumption is measured between the first and last fill-up with an odometer, so it needs two fill-ups to appear."
        style={{ marginBottom: 18 }}
        csv={{
          file: `fuel-consumption-${d.days}d`,
          headers: ["Vehicle", "Plate", "Fuel", "Entries", "Quantity", "Unit", "Cost (EGP)", "Share %", "Per 100 km", "Cost per km"],
          rows: d.byVehicle.map((r) => [r.vehicle, r.plate, r.fuelType, r.entries, r.quantity, r.unit, r.cost, r.sharePct, r.perHundred, r.costPerKm]),
        }}
      >
        <div className="tbl-wrap">
          <table>
            <thead>
              <tr>
                <th>Vehicle</th>
                <th>Fuel</th>
                <th className="num">Entries</th>
                <th className="num">Quantity</th>
                <th className="num">Cost</th>
                <th style={{ width: 150 }}>Share of spend</th>
                <th className="num">Consumption</th>
                <th className="num">Cost / km</th>
              </tr>
            </thead>
            <tbody>
              {d.byVehicle.length === 0 && <EmptyRow cols={8} title="No fuel logged in this window" />}
              {d.byVehicle.map((r) => (
                <tr key={r.vehicleId} className="clickable" onClick={() => navigate(`/vehicles/${r.vehicleId}?tab=fuel`)}>
                  <td>
                    <div className="t-main">{r.vehicle}</div>
                    <div className="t-sub">
                      <span className="plate">{r.plate}</span>
                    </div>
                  </td>
                  <td>
                    <span className="tag">{r.fuelType || "—"}</span>
                  </td>
                  <td className="num">{r.entries}</td>
                  <td className="num">
                    {nf(r.quantity)} {r.unit}
                  </td>
                  <td className="num">{money(r.cost)}</td>
                  <td>
                    <Bar pct={(r.cost / maxCost) * 100} />
                  </td>
                  <td className="num">{r.perHundred != null ? `${nf(r.perHundred, 1)} ${r.unit}/100km` : "—"}</td>
                  <td className="num">{r.costPerKm != null ? `EGP ${nf(r.costPerKm, 2)}` : "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </ReportCard>
      <ReportCard
        title="Fuel log"
        note={`Every fill-up from ${fday(d.from)} to ${fday(d.to)}${d.logTotal > d.log.length ? ` · latest ${d.log.length} of ${d.logTotal}` : ""}.`}
        extra={
          <>
            <input className="rp-search" placeholder="Search station or plate" value={q} onChange={(e) => setQ(e.target.value)} aria-label="Search fuel log" />
            <label className="pillsel" style={{ margin: 0 }}>
              <select value={veh} onChange={(e) => setVeh(e.target.value)} aria-label="Vehicle">
                <option value="">All vehicles</option>
                {vehicles.map(([id, label]) => (
                  <option key={id} value={id}>
                    {label}
                  </option>
                ))}
              </select>
            </label>
          </>
        }
        csv={{
          file: `fuel-log-${d.days}d`,
          headers: ["Date", "Vehicle", "Plate", "Station", "Fuel", "Quantity", "Unit", "Unit price", "Total (EGP)", "Odometer"],
          rows: log.map((r) => [r.date.slice(0, 10), r.vehicle, r.plate, r.station, r.fuelType, r.quantity, r.unit, r.unitPrice, r.total, r.odometer]),
        }}
      >
        <div className="tbl-wrap rp-scroll">
          <table>
            <thead>
              <tr>
                <th>Date</th>
                <th>Vehicle</th>
                <th>Station</th>
                <th>Fuel</th>
                <th className="num">Quantity</th>
                <th className="num">Unit price</th>
                <th className="num">Total</th>
                <th className="num">Odometer</th>
              </tr>
            </thead>
            <tbody>
              {log.length === 0 && <EmptyRow cols={8} title="No fill-ups match" />}
              {log.map((r) => (
                <tr key={r.id}>
                  <td className="mono" style={{ fontSize: 12.5 }}>
                    {fday(r.date)}
                  </td>
                  <td>
                    <div className="t-main">{r.vehicle}</div>
                    <div className="t-sub">
                      <span className="plate">{r.plate}</span>
                    </div>
                  </td>
                  <td>{r.station ?? "—"}</td>
                  <td className="t-sub">{r.fuelType ?? "—"}</td>
                  <td className="num">
                    {nf(r.quantity, 2)} {r.unit}
                  </td>
                  <td className="num">{nf(r.unitPrice, 2)}</td>
                  <td className="num" style={{ fontWeight: 600 }}>
                    {money(r.total)}
                  </td>
                  <td className="num">{r.odometer != null ? nf(r.odometer) : "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </ReportCard>
    </>
  );
}

// =====================================================================
// Budget
// =====================================================================
export function BudgetTab({ d }: { d: BudgetReport }) {
  const navigate = useNavigate();
  const max = Math.max(1, ...d.plan.map((p) => Math.max(p.budget, p.actual)));
  return (
    <>
      <StatGrid stats={d.stats} />
      <ReportCard
        title="Annual plan vs actual"
        note={d.hasAnnual ? "Monthly allocation of the annual budget (Settings → Budget) against actual spend." : "No annual budget for this year — actual spend only."}
        style={{ marginBottom: 18 }}
        extra={
          <Button variant="secondary" size="sm" onClick={() => navigate("/administration/settings?tab=budget")}>
            Set budget
          </Button>
        }
        csv={{
          file: `budget-plan-${d.year}`,
          headers: ["Month", "Budget (EGP)", "Actual (EGP)", "Variance (EGP)", "Variance %"],
          rows: d.plan.map((p) => [p.label, p.budget, p.actual, p.variance, p.variancePct]),
        }}
      >
        <div className="card-b">
          {d.plan.map((p) => (
            <div className="hbar" key={p.month}>
              <div className="row">
                <span style={{ fontWeight: 600 }}>
                  {p.label} {d.year}
                </span>
                <span>
                  <span className="mono">{money(p.actual)}</span>{" "}
                  <span className="t-sub" style={{ margin: 0 }}>
                    of {money(p.budget)}
                  </span>
                  {p.variancePct != null && !p.future && (
                    <span className={`badge ${p.over ? "bad" : p.variancePct > -10 ? "warn" : "ok"}`} style={{ marginLeft: 8 }}>
                      {p.variancePct > 0 ? "+" : ""}
                      {nf(p.variancePct, 0)}%
                    </span>
                  )}
                  {p.future && (
                    <span className="badge plain" style={{ marginLeft: 8 }}>
                      Upcoming
                    </span>
                  )}
                </span>
              </div>
              <div className="track">
                <i style={{ width: `${Math.min(100, (p.actual / max) * 100)}%`, background: p.over ? "var(--red)" : "var(--teal)" }} />
                {p.budget > 0 && <span className="mark" style={{ left: `${Math.min(100, (p.budget / max) * 100)}%` }} title="Budget" />}
              </div>
            </div>
          ))}
          <div className="t-sub" style={{ marginTop: 10 }}>
            The marker on each track is the month's budget.
          </div>
        </div>
      </ReportCard>
      <div className="grid g-2">
        <ReportCard
          title="Budget log"
          note="Every monthly limit set on the Budgets page this year."
          extra={
            <Button variant="secondary" size="sm" onClick={() => navigate("/budgets")}>
              Open budgets
            </Button>
          }
          csv={{
            file: `budget-log-${d.year}`,
            headers: ["Budget", "Category", "Period", "Limit (EGP)", "Actual (EGP)", "Remaining (EGP)", "Utilization %", "Status"],
            rows: d.log.map((r) => [r.name, r.category, r.period, r.limit, r.actual, r.remaining, r.utilizationPct, r.status]),
          }}
        >
          <div className="tbl-wrap">
            <table>
              <thead>
                <tr>
                  <th>Period</th>
                  <th>Budget</th>
                  <th className="num">Limit</th>
                  <th className="num">Actual</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {d.log.length === 0 && <EmptyRow cols={5} title="No monthly limits this year" />}
                {d.log.map((r) => (
                  <tr key={r.id}>
                    <td>{r.period}</td>
                    <td>
                      <div className="t-main">{r.name}</div>
                      <div className="t-sub">{r.category}</div>
                    </td>
                    <td className="num">{money(r.limit)}</td>
                    <td className="num">
                      {money(r.actual)}
                      <div className="t-sub">{r.utilizationPct}%</div>
                    </td>
                    <td>
                      <span className={`badge ${statusTone(r.status) === "info" ? "ok" : statusTone(r.status)}`}>{r.status}</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </ReportCard>
        <ReportCard
          title="By category"
          note="Annual budget × category share against the year's spend."
          csv={{
            file: `budget-categories-${d.year}`,
            headers: ["Category", "Share %", "Budget (EGP)", "Actual (EGP)", "Remaining (EGP)", "Utilization %"],
            rows: d.categories.map((c) => [c.name, c.sharePct, c.budget, c.actual, c.remaining, c.utilizationPct]),
          }}
        >
          <div className="tbl-wrap">
            <table>
              <thead>
                <tr>
                  <th>Category</th>
                  <th className="num">Budget</th>
                  <th className="num">Actual</th>
                  <th style={{ width: 130 }}>Used</th>
                </tr>
              </thead>
              <tbody>
                {d.categories.map((c) => (
                  <tr key={c.name}>
                    <td>
                      <span className="bd-cat">
                        <i style={{ background: c.color, width: 9, height: 9, borderRadius: "50%", display: "inline-block", marginRight: 8 }} />
                        {c.name}
                      </span>
                      <div className="t-sub">{nf(c.sharePct, 1)}% share</div>
                    </td>
                    <td className="num">{c.budget ? money(c.budget) : "—"}</td>
                    <td className="num">{money(c.actual)}</td>
                    <td>
                      {c.utilizationPct != null ? (
                        <div className="flex" style={{ gap: 8, alignItems: "center", display: "flex" }}>
                          <Bar pct={c.utilizationPct} tone={c.utilizationPct > 100 ? "bad" : c.utilizationPct > 85 ? "warn" : ""} />
                          <span className="mono" style={{ fontSize: 12 }}>
                            {nf(c.utilizationPct, 0)}%
                          </span>
                        </div>
                      ) : (
                        "—"
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </ReportCard>
      </div>
    </>
  );
}

// =====================================================================
// PM compliance
// =====================================================================
export function PmTab({ d, onRunPm }: { d: PmReport; onRunPm: () => void }) {
  const navigate = useNavigate();
  return (
    <>
      <StatGrid stats={d.stats} />
      <ReportCard
        title="Compliance by vehicle"
        note="Interval consumption against the trigger that will fire first."
        style={{ marginBottom: 18 }}
        extra={
          <Button size="sm" onClick={onRunPm}>
            <History /> Run PM engine
          </Button>
        }
        csv={{
          file: "pm-compliance",
          headers: ["Vehicle", "Plate", "Trigger", "Interval", "Unit", "Consumed %", "Reading", "Runway", "Last service", "State", "Dispatch blocked"],
          rows: d.rows.map((r) => [r.vehicle, r.plate, r.trigger, r.interval, r.unit, r.consumedPct, r.reading, r.label, r.lastService?.slice(0, 10),
            r.status === "overdue" ? "Overdue" : r.status === "due" ? "Due soon" : "On schedule", r.dispatchBlocked ? "Yes" : "No"]),
        }}
      >
        <div className="tbl-wrap">
          <table>
            <thead>
              <tr>
                <th>Vehicle</th>
                <th>Trigger</th>
                <th className="num">Interval</th>
                <th className="num">Consumed</th>
                <th style={{ width: 240 }}>Runway</th>
                <th>Last service</th>
                <th>State</th>
              </tr>
            </thead>
            <tbody>
              {d.rows.length === 0 && <EmptyRow cols={7} title="No vehicle has a PM schedule" sub="Set PM intervals on each vehicle's profile." />}
              {d.rows.map((r) => (
                <tr key={r.vehicleId} className="clickable" onClick={() => navigate(`/vehicles/${r.vehicleId}`)}>
                  <td>
                    <div className="t-main link">{r.vehicle}</div>
                    <div className="t-sub">
                      <span className="plate">{r.plate}</span>
                    </div>
                  </td>
                  <td>
                    <span className="tag" style={{ textTransform: "capitalize" }}>
                      {r.trigger}
                    </span>
                  </td>
                  <td className="num">
                    {nf(r.interval)} {r.unit}
                  </td>
                  <td className="num">{nf(r.consumedPct, 0)}%</td>
                  <td>
                    <PmGauge
                      odometer={r.reading}
                      unit={r.readingUnit}
                      pm={{ status: r.status, percent: r.consumedPct, label: r.label, unit: r.unit, preAlertPercent: r.preAlertPct, trigger: r.trigger }}
                    />
                  </td>
                  <td>{fday(r.lastService)}</td>
                  <td>
                    <span className={`badge ${r.status === "overdue" ? "bad" : r.status === "due" ? "warn" : "ok"}`}>
                      {r.status === "overdue" ? "Overdue" : r.status === "due" ? "Due soon" : "On schedule"}
                    </span>
                    {r.dispatchBlocked && <div className="t-sub" style={{ marginTop: 4 }}>Dispatch blocked</div>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {d.noSchedule > 0 && (
          <div className="t-sub" style={{ padding: "10px 22px 16px" }}>
            {d.noSchedule} vehicle{d.noSchedule === 1 ? " has" : "s have"} no PM schedule and {d.noSchedule === 1 ? "is" : "are"} not counted.
          </div>
        )}
      </ReportCard>
      <ReportCard
        title="PM task library"
        note="What the engine puts on each preventive work order."
        extra={
          <Button variant="secondary" size="sm" onClick={() => navigate("/administration/settings?tab=maintenance")}>
            Manage tasks
          </Button>
        }
        csv={{
          file: "pm-task-library",
          headers: ["Task", "Part category", "Trigger", "Interval", "Duration (h)", "Est. cost (EGP)", "Role"],
          rows: d.tasks.map((t) => [t.name, t.partCategory, t.trigger, t.interval, t.durationHours, t.estimatedCost, t.role]),
        }}
      >
        <div className="tbl-wrap">
          <table>
            <thead>
              <tr>
                <th>Task</th>
                <th>Part category</th>
                <th>Trigger</th>
                <th>Interval</th>
                <th className="num">Duration</th>
                <th className="num">Est. cost</th>
                <th>Required role</th>
              </tr>
            </thead>
            <tbody>
              {d.tasks.length === 0 && <EmptyRow cols={7} title="No PM tasks" />}
              {d.tasks.map((t) => (
                <tr key={t.id}>
                  <td className="t-main">{t.name}</td>
                  <td>{t.partCategory ? <span className="tag">{t.partCategory}</span> : "—"}</td>
                  <td style={{ textTransform: "capitalize" }}>{t.trigger}</td>
                  <td className="mono" style={{ fontSize: 12.5 }}>
                    {t.interval || "—"}
                  </td>
                  <td className="num">{nf(t.durationHours, 2)} h</td>
                  <td className="num">{money(t.estimatedCost)}</td>
                  <td className="t-sub">{t.role}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </ReportCard>
    </>
  );
}

// =====================================================================
// Work orders
// =====================================================================
export function WorkOrdersTab({ d }: { d: WorkOrderReport }) {
  const maxT = Math.max(1, ...d.types.map((t) => t.orders));
  return (
    <>
      <StatGrid stats={d.stats} />
      <div className="grid g-2" style={{ marginBottom: 18 }}>
        <ReportCard
          title="By technician"
          note="Load and closure per person."
          csv={{ file: "work-orders-by-technician", headers: ["Technician", "Assigned", "Closed", "Open", "Overdue", "Cost (EGP)"],
            rows: d.technicians.map((t) => [t.technician, t.assigned, t.closed, t.open, t.overdue, t.cost]) }}
        >
          <div className="tbl-wrap">
            <table>
              <thead>
                <tr>
                  <th>Technician</th>
                  <th className="num">Assigned</th>
                  <th className="num">Closed</th>
                  <th className="num">Open</th>
                  <th className="num">Overdue</th>
                  <th className="num">Cost</th>
                </tr>
              </thead>
              <tbody>
                {d.technicians.length === 0 && <EmptyRow cols={6} title="No work orders" />}
                {d.technicians.map((t) => (
                  <tr key={t.technician}>
                    <td className="t-main">{t.technician}</td>
                    <td className="num">{t.assigned}</td>
                    <td className="num">{t.closed}</td>
                    <td className="num">{t.open}</td>
                    <td className="num">{t.overdue ? <span className="badge bad">{t.overdue}</span> : "0"}</td>
                    <td className="num">{money(t.cost)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </ReportCard>
        <ReportCard
          title="By order type"
          note="Where the maintenance money goes."
          csv={{ file: "work-orders-by-type", headers: ["Type", "Orders", "Tasks", "Share %", "Cost (EGP)"], rows: d.types.map((t) => [t.type, t.orders, t.tasks, t.sharePct, t.cost]) }}
        >
          <div className="tbl-wrap">
            <table>
              <thead>
                <tr>
                  <th>Type</th>
                  <th className="num">Orders</th>
                  <th className="num">Tasks</th>
                  <th style={{ width: 140 }}>Share</th>
                  <th className="num">Cost</th>
                </tr>
              </thead>
              <tbody>
                {d.types.map((t) => (
                  <tr key={t.type}>
                    <td className="t-main">{t.type}</td>
                    <td className="num">{t.orders}</td>
                    <td className="num">{t.tasks}</td>
                    <td>
                      <Bar pct={(t.orders / maxT) * 100} />
                    </td>
                    <td className="num">{money(t.cost)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </ReportCard>
      </div>
      <ReportCard
        title="Order register"
        note="Every job with its age past the scheduled date (latest 300)."
        csv={{
          file: "work-order-register",
          headers: ["Order", "Vehicle", "Plate", "Type", "Priority", "Technician", "Scheduled", "Days late", "Cost (EGP)", "Status"],
          rows: d.register.map((o) => [o.code, o.vehicle, o.plate, o.type, o.priority, o.technician, o.dueDate.slice(0, 10), o.ageDays, o.cost, o.status]),
        }}
      >
        <div className="tbl-wrap rp-scroll">
          <table>
            <thead>
              <tr>
                <th>Order</th>
                <th>Vehicle</th>
                <th>Type</th>
                <th>Technician</th>
                <th>Scheduled</th>
                <th className="num">Days late</th>
                <th className="num">Cost</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {d.register.length === 0 && <EmptyRow cols={8} title="No work orders" />}
              {d.register.map((o) => (
                <tr key={o.id}>
                  <td className="mono" style={{ fontSize: 12.5 }}>
                    {o.code}
                  </td>
                  <td>
                    <div>{o.vehicle}</div>
                    <div className="t-sub">
                      <span className="plate">{o.plate}</span>
                    </div>
                  </td>
                  <td>
                    <span className="tag">{o.type}</span>
                  </td>
                  <td>{o.technician ?? "—"}</td>
                  <td className="mono" style={{ fontSize: 12.5 }}>
                    {fday(o.dueDate)}
                  </td>
                  <td className="num">{o.ageDays ? `${o.ageDays} d` : "—"}</td>
                  <td className="num">{money(o.cost)}</td>
                  <td>
                    <span className={`badge ${statusTone(o.status)}`}>{o.status}</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </ReportCard>
    </>
  );
}

// =====================================================================
// Cost
// =====================================================================
export function CostTab({ d }: { d: CostReport }) {
  const navigate = useNavigate();
  const maxV = Math.max(1, ...d.byVehicle.map((r) => r.total));
  return (
    <>
      <StatGrid stats={d.stats} />
      <ReportCard
        title="Cost by vehicle"
        note={`Expenses, fuel and completed work orders in the last ${d.days} days, with cost per kilometre where readings allow.`}
        style={{ marginBottom: 18 }}
        csv={{
          file: `cost-by-vehicle-${d.days}d`,
          headers: ["Vehicle", "Plate", "Expenses", "Fuel", "Work orders", "Total (EGP)", "Share %", "Distance", "Cost per km"],
          rows: d.byVehicle.map((r) => [r.vehicle, r.plate, r.expenses, r.fuel, r.workOrders, r.total, r.sharePct, r.distance, r.costPerKm]),
        }}
      >
        <div className="tbl-wrap">
          <table>
            <thead>
              <tr>
                <th>Vehicle</th>
                <th className="num">Expenses</th>
                <th className="num">Fuel</th>
                <th className="num">Work orders</th>
                <th className="num">Total</th>
                <th style={{ width: 150 }}>Share</th>
                <th className="num">Distance</th>
                <th className="num">Cost / km</th>
              </tr>
            </thead>
            <tbody>
              {d.byVehicle.map((r) => (
                <tr key={r.vehicleId} className="clickable" onClick={() => navigate(`/vehicles/${r.vehicleId}`)}>
                  <td>
                    <div className="t-main">{r.vehicle}</div>
                    <div className="t-sub">
                      <span className="plate">{r.plate}</span>
                    </div>
                  </td>
                  <td className="num">{money(r.expenses)}</td>
                  <td className="num">{money(r.fuel)}</td>
                  <td className="num">{money(r.workOrders)}</td>
                  <td className="num" style={{ fontWeight: 700 }}>
                    {money(r.total)}
                  </td>
                  <td>
                    <Bar pct={(r.total / maxV) * 100} />
                  </td>
                  <td className="num">{r.distance != null ? nf(r.distance) : "—"}</td>
                  <td className="num">{r.costPerKm != null ? `EGP ${nf(r.costPerKm, 2)}` : "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </ReportCard>
      <ReportCard
        title="Cost by category"
        note="Spend per expense type against the annual category budget prorated to the window."
        csv={{
          file: `cost-by-category-${d.days}d`,
          headers: ["Category", "Entries", "Total (EGP)", "Share %", "Budget (EGP)", "Variance %"],
          rows: d.byCategory.map((c) => [c.name, c.entries, c.total, c.sharePct, c.budget, c.variancePct]),
        }}
      >
        <div className="tbl-wrap">
          <table>
            <thead>
              <tr>
                <th>Category</th>
                <th className="num">Entries</th>
                <th style={{ width: 150 }}>Share</th>
                <th className="num">Total</th>
                <th className="num">Budget</th>
                <th className="num">Variance</th>
              </tr>
            </thead>
            <tbody>
              {d.byCategory.map((c) => (
                <tr key={c.name}>
                  <td className="t-main">
                    <i style={{ background: c.color, width: 9, height: 9, borderRadius: "50%", display: "inline-block", marginRight: 8 }} />
                    {c.name}
                  </td>
                  <td className="num">{c.entries}</td>
                  <td>
                    <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                      <Bar pct={c.sharePct} />
                      <span className="mono" style={{ fontSize: 12 }}>
                        {nf(c.sharePct, 0)}%
                      </span>
                    </div>
                  </td>
                  <td className="num">{money(c.total)}</td>
                  <td className="num">{c.budget != null ? money(c.budget) : "—"}</td>
                  <td className="num">
                    {c.variancePct != null ? (
                      <span className={`badge ${c.variancePct > 0 ? "bad" : c.variancePct > -10 ? "warn" : "ok"}`}>
                        {c.variancePct > 0 ? "+" : ""}
                        {nf(c.variancePct, 0)}%
                      </span>
                    ) : (
                      "—"
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </ReportCard>
    </>
  );
}

// =====================================================================
// Expenses & forecast
// =====================================================================
export function ExpensesTab({ d }: { d: ExpenseReport }) {
  const navigate = useNavigate();
  const maxM = Math.max(1, ...d.months.map((m) => m.total), d.projection);
  return (
    <>
      <StatGrid stats={d.stats} />
      <ReportCard title="Spend trend and forecast" note="Six full months, this month to date, and this month projected at the current run rate." style={{ marginBottom: 18 }}
        csv={{ file: "spend-forecast", headers: ["Month", "Total (EGP)"], rows: [...d.months.map((m) => [m.label + (m.current ? " (to date)" : ""), m.total]), ["Forecast", d.projection]] }}>
        <div className="card-b">
          <div className="rp-fc">
            {d.months.map((m) => (
              <div key={m.key} className="rp-fc-col">
                <div className="rp-fc-bar">
                  <div style={{ height: `${Math.max(3, (m.total / maxM) * 100)}%`, background: m.current ? "var(--teal-l)" : "var(--teal)" }} />
                </div>
                <div className="mono rp-fc-v">{money(m.total)}</div>
                <div className="rp-fc-l">{m.current ? `${m.label} (to date)` : m.label}</div>
              </div>
            ))}
            <div className="rp-fc-col">
              <div className="rp-fc-bar">
                <div className="rp-fc-proj" style={{ height: `${Math.max(3, (d.projection / maxM) * 100)}%` }} />
              </div>
              <div className="mono rp-fc-v" style={{ color: "var(--teal-d)" }}>
                {money(d.projection)}
              </div>
              <div className="rp-fc-l" style={{ color: "var(--teal-d)" }}>
                Forecast
              </div>
            </div>
          </div>
        </div>
      </ReportCard>
      <ReportCard
        title="Tracking by category"
        note="Last 30 days against the 30 before them."
        extra={
          <Button variant="secondary" size="sm" onClick={() => navigate("/expenses")}>
            Open ledger
          </Button>
        }
        csv={{ file: "expense-trend-by-category", headers: ["Category", "Entries", "Lifetime", "Last 30 d", "Prior 30 d", "Trend %"],
          rows: d.byType.map((r) => [r.name, r.entries, r.lifetime, r.last30, r.prior30, r.trendPct]) }}
      >
        <div className="tbl-wrap">
          <table>
            <thead>
              <tr>
                <th>Category</th>
                <th className="num">Entries</th>
                <th className="num">Lifetime</th>
                <th className="num">Last 30 d</th>
                <th className="num">Prior 30 d</th>
                <th>Trend</th>
              </tr>
            </thead>
            <tbody>
              {d.byType.map((r) => (
                <tr key={r.name}>
                  <td className="t-main">
                    <i style={{ background: r.color, width: 9, height: 9, borderRadius: "50%", display: "inline-block", marginRight: 8 }} />
                    {r.name}
                  </td>
                  <td className="num">{r.entries}</td>
                  <td className="num">{money(r.lifetime)}</td>
                  <td className="num" style={{ fontWeight: 600 }}>
                    {money(r.last30)}
                  </td>
                  <td className="num">{money(r.prior30)}</td>
                  <td>
                    <span className={`badge ${r.trendPct > 15 ? "bad" : r.trendPct < 0 ? "ok" : "plain"}`}>
                      {r.trendPct >= 0 ? "+" : ""}
                      {r.trendPct}%
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </ReportCard>
    </>
  );
}

// =====================================================================
// Issues
// =====================================================================
export function IssuesTab({ d }: { d: IssueReport }) {
  const navigate = useNavigate();
  const maxR = Math.max(1, ...d.parts.map((r) => r.occurrences));
  return (
    <>
      <StatGrid stats={d.stats} />
      <ReportCard title="Recurrence by part" note="Parts that keep coming back are the ones worth a design or supplier review." style={{ marginBottom: 18 }}
        csv={{ file: "issue-recurrence", headers: ["Part", "Occurrences", "Vehicles", "Open", "Last seen", "Linked repair cost (EGP)", "Flag"],
          rows: d.parts.map((r) => [r.part, r.occurrences, r.vehicles, r.open, r.lastSeen.slice(0, 10), r.linkedCost, r.recurring ? "Recurring" : "One-off"]) }}>
        <div className="tbl-wrap">
          <table>
            <thead>
              <tr>
                <th>Part</th>
                <th className="num">Occurrences</th>
                <th style={{ width: 160 }}>Frequency</th>
                <th className="num">Vehicles affected</th>
                <th className="num">Still open</th>
                <th>Last seen</th>
                <th className="num">Linked repair cost</th>
                <th>Flag</th>
              </tr>
            </thead>
            <tbody>
              {d.parts.length === 0 && <EmptyRow cols={8} title="No issues recorded" />}
              {d.parts.map((r) => (
                <tr key={r.part}>
                  <td className="t-main">{r.part}</td>
                  <td className="num">{r.occurrences}</td>
                  <td>
                    <Bar pct={(r.occurrences / maxR) * 100} tone={r.recurring ? "warn" : ""} />
                  </td>
                  <td className="num">{r.vehicles}</td>
                  <td className="num">{r.open ? <span className="badge bad">{r.open}</span> : "0"}</td>
                  <td className="mono" style={{ fontSize: 12.5 }}>
                    {fday(r.lastSeen)}
                  </td>
                  <td className="num">{money(r.linkedCost)}</td>
                  <td>{r.recurring ? <span className="badge warn">Recurring</span> : <span className="badge plain">One-off</span>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </ReportCard>
      <div className="grid g-2">
        <ReportCard title="Issues by vehicle" csv={{ file: "issues-by-vehicle", headers: ["Vehicle", "Plate", "Issues", "Open"], rows: d.byVehicle.map((x) => [x.vehicle, x.plate, x.issues, x.open]) }}>
          <div className="tbl-wrap">
            <table>
              <thead>
                <tr>
                  <th>Vehicle</th>
                  <th className="num">Issues</th>
                  <th className="num">Open</th>
                </tr>
              </thead>
              <tbody>
                {d.byVehicle.map((x) => (
                  <tr key={x.vehicleId} className="clickable" onClick={() => navigate(`/vehicles/${x.vehicleId}?tab=issues`)}>
                    <td>
                      <div className="t-main">{x.vehicle}</div>
                      <div className="t-sub">
                        <span className="plate">{x.plate}</span>
                      </div>
                    </td>
                    <td className="num">{x.issues}</td>
                    <td className="num">{x.open ? <span className="badge bad">{x.open}</span> : "0"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </ReportCard>
        <ReportCard title="Where issues come from" csv={{ file: "issues-by-source", headers: ["Source", "Issues", "Share %"], rows: d.bySource.map((x) => [x.source, x.issues, x.sharePct]) }}>
          <div className="tbl-wrap">
            <table>
              <thead>
                <tr>
                  <th>Source</th>
                  <th className="num">Issues</th>
                  <th style={{ width: 150 }}>Share</th>
                </tr>
              </thead>
              <tbody>
                {d.bySource.map((x) => (
                  <tr key={x.source}>
                    <td className="t-main">{x.source}</td>
                    <td className="num">{x.issues}</td>
                    <td>
                      <Bar pct={x.sharePct} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </ReportCard>
      </div>
    </>
  );
}

// =====================================================================
// Vehicle uptime
// =====================================================================
export function UptimeTab({ d }: { d: UptimeReport }) {
  const navigate = useNavigate();
  return (
    <>
      <StatGrid stats={d.stats} />
      <ReportCard title="Uptime and downtime by vehicle" note="Downtime counts workshop days of each work order plus every day a scheduled order is past its date."
        csv={{ file: `uptime-${d.days}d`, headers: ["Vehicle", "Plate", "Status", "Work orders", "Downtime days", "Distance", "Uptime %"],
          rows: d.rows.map((r) => [r.vehicle, r.plate, r.status, r.workOrders, r.downtimeDays, r.distance, r.uptimePct]) }}>
        <div className="tbl-wrap">
          <table>
            <thead>
              <tr>
                <th>Vehicle</th>
                <th>Status</th>
                <th className="num">Work orders</th>
                <th className="num">Downtime</th>
                <th className="num">Distance</th>
                <th style={{ width: 200 }}>Uptime</th>
              </tr>
            </thead>
            <tbody>
              {d.rows.map((r) => (
                <tr key={r.vehicleId} className="clickable" onClick={() => navigate(`/vehicles/${r.vehicleId}`)}>
                  <td>
                    <div className="t-main">{r.vehicle}</div>
                    <div className="t-sub">
                      <span className="plate">{r.plate}</span>
                    </div>
                  </td>
                  <td>
                    <span className={`badge ${r.status === "Active" || r.status === "In transit" ? "ok" : r.status === "Under maintenance" ? "warn" : "bad"}`}>{r.status}</span>
                  </td>
                  <td className="num">{r.workOrders}</td>
                  <td className="num">{r.downtimeDays} d</td>
                  <td className="num">{r.distance != null ? nf(r.distance) : "—"}</td>
                  <td>
                    <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                      <Bar pct={r.uptimePct} tone={r.uptimePct < 80 ? "bad" : r.uptimePct < 95 ? "warn" : ""} />
                      <span className="mono" style={{ fontSize: 13, fontWeight: 600 }}>
                        {nf(r.uptimePct, 1)}%
                      </span>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </ReportCard>
    </>
  );
}

// =====================================================================
// Drivers
// =====================================================================
export function DriversTab({ d }: { d: DriverReport }) {
  const navigate = useNavigate();
  return (
    <>
      <StatGrid stats={d.stats} />
      <ReportCard title="Driver performance" note="Score blends rating (70%), a valid licence (20%) and no expired documents (10%)."
        csv={{ file: "driver-performance", headers: ["Driver", "Licence class", "Status", "Vehicle", "Plate", "Rating", "Licence days left", "Expired documents", "Assignments", `Vehicle spend ${d.days}d`, "Score"],
          rows: d.rows.map((r) => [r.driver, r.licenseClass, r.status, r.vehicle, r.plate, r.rating, r.licenseDays, r.expiredDocuments, r.assignments, r.vehicleSpend, r.score]) }}>
        <div className="tbl-wrap">
          <table>
            <thead>
              <tr>
                <th>Driver</th>
                <th>Assigned vehicle</th>
                <th className="num">Rating</th>
                <th>Licence</th>
                <th className="num">Assignments</th>
                <th className="num">Vehicle spend {d.days}d</th>
                <th style={{ width: 170 }}>Score</th>
              </tr>
            </thead>
            <tbody>
              {d.rows.length === 0 && <EmptyRow cols={7} title="No drivers yet" />}
              {d.rows.map((r) => (
                <tr key={r.driverId} className="clickable" onClick={() => navigate(`/drivers/${r.driverId}`)}>
                  <td>
                    <div className="t-main">{r.driver}</div>
                    <div className="t-sub">
                      {r.licenseClass} · {r.status}
                    </div>
                  </td>
                  <td>
                    {r.vehicle ? (
                      <>
                        <div>{r.vehicle}</div>
                        <div className="t-sub">
                          <span className="plate">{r.plate}</span>
                        </div>
                      </>
                    ) : (
                      <span className="tag">Unassigned</span>
                    )}
                  </td>
                  <td className="num">{nf(r.rating, 1)}</td>
                  <td>
                    <span className={`badge ${r.licenseDays == null || r.licenseDays < 0 ? "bad" : r.licenseDays <= 30 ? "warn" : "ok"}`}>
                      {r.licenseDays == null ? "Missing" : r.licenseDays < 0 ? "Expired" : r.licenseDays <= 30 ? `${r.licenseDays} d` : "Valid"}
                    </span>
                    {r.expiredDocuments > 0 && (
                      <span className="badge bad" style={{ marginLeft: 6 }}>
                        {r.expiredDocuments} doc
                      </span>
                    )}
                  </td>
                  <td className="num">{r.assignments}</td>
                  <td className="num">{money(r.vehicleSpend)}</td>
                  <td>
                    <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                      <Bar pct={r.score} tone={r.score < 60 ? "bad" : r.score < 80 ? "warn" : ""} />
                      <span className="mono" style={{ fontSize: 13, fontWeight: 600 }}>
                        {r.score}
                      </span>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </ReportCard>
    </>
  );
}

// =====================================================================
// Odometer
// =====================================================================
export function OdometerTab({ d }: { d: OdometerReport }) {
  const navigate = useNavigate();
  return (
    <>
      <StatGrid stats={d.stats} />
      <ReportCard title="Odometer report" note="Usage-based PM triggers are only as good as the freshest reading."
        csv={{ file: `odometer-${d.days}d`, headers: ["Vehicle", "Plate", "Current", "Unit", "Readings", "Distance", "Average per day", "Last logged", "Source"],
          rows: d.rows.map((r) => [r.vehicle, r.plate, r.current, r.unit, r.readings, r.distance, r.avgPerDay, r.lastLogged?.slice(0, 10), r.source]) }}>
        <div className="tbl-wrap">
          <table>
            <thead>
              <tr>
                <th>Vehicle</th>
                <th className="num">Current reading</th>
                <th className="num">Readings</th>
                <th className="num">Distance {d.days}d</th>
                <th className="num">Average per day</th>
                <th>Last logged</th>
                <th>Source</th>
              </tr>
            </thead>
            <tbody>
              {d.rows.map((r) => (
                <tr key={r.vehicleId} className="clickable" onClick={() => navigate(`/vehicles/${r.vehicleId}?tab=odo`)}>
                  <td>
                    <div className="t-main">{r.vehicle}</div>
                    <div className="t-sub">
                      <span className="plate">{r.plate}</span>
                    </div>
                  </td>
                  <td className="num" style={{ fontWeight: 600 }}>
                    {nf(r.current)} {r.unit}
                  </td>
                  <td className="num">{r.readings}</td>
                  <td className="num">{r.distance != null ? nf(r.distance) : "—"}</td>
                  <td className="num">{r.avgPerDay != null ? nf(r.avgPerDay, 1) : "—"}</td>
                  <td>{r.lastLogged ? <span className={`badge ${r.stale ? "warn" : "ok"}`}>{fday(r.lastLogged)}</span> : <span className="badge bad">Never</span>}</td>
                  <td className="t-sub">{r.source ?? "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </ReportCard>
    </>
  );
}
