import { useCallback, useEffect, useState, type ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import {
  AlertTriangle, Check, ClipboardList, Eye, Fuel, Gauge, Pencil, Plus, Receipt, Repeat, RotateCcw, Trash2, Users as UsersIcon, Wrench, Package,
} from "lucide-react";
import { Button } from "../ui/Button";
import { PageSpinner } from "../ui/Spinner";
import { RowMenu } from "../ui/RowMenu";
import { ConfirmDialog } from "../ui/ConfirmDialog";
import { useToast } from "../ui/Toast";
import { vehiclesApi } from "../../lib/api";
import type {
  FuelRecord, OdometerReadingRow, VehicleAssignment, VehicleDetail, VehicleExpense, VehicleInspection, VehicleIssue, VehiclePart, WorkOrderRow,
} from "../../lib/types";
import { formatDay, initials } from "../../lib/utils";
import { ExpenseFormModal, FuelFormModal, IssueFormModal, PartFormModal, ReadingFormModal, ReplacePartModal, issueToWorkOrder } from "./recordModals";
import type { WorkOrderPreset } from "../workorders/WorkOrderFormModal";
import { WorkOrderTable } from "../workorders/WorkOrderViews";
import { workOrdersApi } from "../../lib/api";
import { money, num } from "./vehicleUi";
import { InspectionViewModal } from "../inspections/InspectionModals";
import "./vehicles.css";

export type VehicleTabKey = "parts" | "issues" | "fuel" | "odo" | "maintenance" | "expenses" | "inspections" | "drivers";

export const VEHICLE_TABS: { key: VehicleTabKey; label: string; count: keyof VehicleDetail["counts"] }[] = [
  { key: "parts", label: "Parts", count: "parts" },
  { key: "issues", label: "Issues", count: "issues" },
  { key: "fuel", label: "Fuel records", count: "fuel" },
  { key: "odo", label: "Kilometer records", count: "odometer" },
  { key: "maintenance", label: "Maintenance", count: "maintenance" },
  { key: "expenses", label: "Expenses", count: "expenses" },
  { key: "inspections", label: "Inspections", count: "inspections" },
  { key: "drivers", label: "Drivers", count: "drivers" },
];

export interface TabProps {
  vehicle: VehicleDetail;
  /** Reload the vehicle (header, banners, counts) after a change. */
  onChanged: () => void;
  onSchedule: (preset?: WorkOrderPreset) => void;
  onHandOff: () => void;
  onInspect: () => void;
  onResume: (inspectionId: string) => void;
}

/** Loads one tab's rows; `reload` after a change. */
function useRows<T>(load: () => Promise<T[]>, deps: unknown[]) {
  const [rows, setRows] = useState<T[] | null>(null);
  const [error, setError] = useState(false);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const reload = useCallback(() => load().then((r) => (setRows(r), setError(false))).catch(() => setError(true)), deps);
  useEffect(() => {
    setRows(null);
    reload();
  }, [reload]);
  return { rows, error, reload };
}

function TabCard({ title, addLabel, onAdd, extra, children }: { title: string; addLabel?: string; onAdd?: () => void; extra?: ReactNode; children: ReactNode }) {
  return (
    <div className="card">
      <div className="card-h">
        <h2>{title}</h2>
        {extra}
        {addLabel && (
          <button className="btn teal sm" type="button" onClick={onAdd}>
            <Plus /> {addLabel}
          </button>
        )}
      </div>
      {children}
    </div>
  );
}

function Rows<T>({ rows, error, retry, cols, empty, render }: {
  rows: T[] | null;
  error: boolean;
  retry: () => void;
  cols: number;
  empty: { icon: ReactNode; title: string; text: string };
  render: (row: T, index: number) => ReactNode;
}) {
  if (error)
    return (
      <tr>
        <td colSpan={cols}>
          <div className="empty">
            <AlertTriangle />
            <h3>These records could not be loaded</h3>
            <p>
              <Button variant="secondary" size="sm" onClick={retry}>
                Try again
              </Button>
            </p>
          </div>
        </td>
      </tr>
    );
  if (!rows)
    return (
      <tr>
        <td colSpan={cols}>
          <PageSpinner />
        </td>
      </tr>
    );
  if (!rows.length)
    return (
      <tr>
        <td colSpan={cols}>
          <div className="empty">
            {empty.icon}
            <h3>{empty.title}</h3>
            {empty.text && <p>{empty.text}</p>}
          </div>
        </td>
      </tr>
    );
  return <>{rows.map(render)}</>;
}

const mono12 = { fontSize: 12 } as const;

/** Shared confirm-delete state for a tab. */
function useRemove(onDone: () => void) {
  const toast = useToast();
  const [pending, setPending] = useState<{ title: string; message: string; run: () => Promise<void>; done: string } | null>(null);
  const dialog = (
    <ConfirmDialog
      open={!!pending}
      title={pending?.title ?? ""}
      message={pending?.message ?? ""}
      onClose={() => setPending(null)}
      onConfirm={async () => {
        if (!pending) return;
        try {
          await pending.run();
          toast(pending.done);
          setPending(null);
          onDone();
        } catch (err) {
          toast(err instanceof Error ? err.message : "Could not remove", "bad");
        }
      }}
    />
  );
  return { ask: setPending, dialog };
}

// =====================================================================
// Parts
// =====================================================================
export function PartsTab({ vehicle, onChanged, onSchedule }: TabProps) {
  const { rows, error, reload } = useRows<VehiclePart>(() => vehiclesApi.parts.list(vehicle.id), [vehicle.id]);
  const [form, setForm] = useState<{ open: boolean; part: VehiclePart | null }>({ open: false, part: null });
  const [replacing, setReplacing] = useState<VehiclePart | null>(null);
  const changed = () => (reload(), onChanged());
  const { ask, dialog } = useRemove(changed);
  const covered = rows?.filter((p) => p.wear.warranty.covered).length ?? 0;
  const worn = rows?.filter((p) => p.wear.status === "due" || p.wear.status === "expired").length ?? 0;
  const unit = vehicle.readingUnit;

  return (
    <TabCard title="Fitted parts" addLabel="Add part" onAdd={() => setForm({ open: true, part: null })}>
      <div className="filters" style={{ borderBottom: "1px solid var(--line-s)" }}>
        <span className="badge ok">{covered} under warranty</span>
        <span className={`badge ${worn ? "warn" : "plain"}`}>{worn} near or past service life</span>
        <span className="count">Distance is counted from each part's activation reading</span>
      </div>
      <div className="tbl-wrap vt">
        <table>
          <thead>
            <tr>
              <th>Part</th>
              <th>Serial</th>
              <th>Activated</th>
              <th className="num">{unit === "hr" ? "Hours" : "Kilometres"} run</th>
              <th style={{ width: 200 }}>Service life</th>
              <th>Warranty</th>
              <th className="num">Unit cost</th>
              <th>Status</th>
              <th style={{ textAlign: "right" }}>Actions</th>
            </tr>
          </thead>
          <tbody>
            <Rows
              rows={rows}
              error={error}
              retry={reload}
              cols={9}
              empty={{ icon: <Package />, title: "No parts recorded yet", text: "Add the fitted parts to track wear against the odometer." }}
              render={(p) => {
                const w = p.wear;
                const tone = w.status === "expired" ? "bad" : w.status === "due" ? "warn" : "";
                const retired = p.status === "Retired";
                return (
                  <tr key={p.id} style={retired ? { opacity: 0.6 } : undefined}>
                    <td className="wrap">
                      <div className="t-main">{p.partName}</div>
                      {p.partNameAr && <div className="arabic">{p.partNameAr}</div>}
                      <div className="t-sub mono">
                        {p.code} · {p.categoryName}
                      </div>
                    </td>
                    <td className="mono" style={{ fontSize: 12.5 }}>
                      {p.serial ?? "—"}
                    </td>
                    <td>
                      <div className="mono" style={{ fontSize: 12.5 }}>
                        {formatDay(p.installedDate)}
                      </div>
                      <div className="t-sub mono">
                        @ {num(p.installedReading)} {unit}
                      </div>
                    </td>
                    <td className="num" style={{ fontWeight: 600 }}>
                      {num(w.distanceRun)} {unit}
                      <div className="t-sub mono" style={{ fontWeight: 400 }}>
                        {Math.round(w.daysRun / 30)} mo in service
                      </div>
                    </td>
                    <td>
                      {w.status === "untracked" || retired ? (
                        <span className="t-sub">{retired ? `Retired ${formatDay(p.retiredDate)}` : "Not tracked"}</span>
                      ) : (
                        <>
                          <div className="flex">
                            <div className={`bar-mini ${tone}`}>
                              <i style={{ width: `${Math.min(100, w.percent)}%` }} />
                            </div>
                            <span className="mono" style={{ fontSize: 12, color: "var(--muted)" }}>
                              {Math.round(w.percent)}%
                            </span>
                          </div>
                          <div
                            className="t-sub mono"
                            style={{ marginTop: 4, color: tone === "bad" ? "var(--red)" : tone === "warn" ? "#B45309" : "var(--muted)" }}
                          >
                            {w.label}
                            {w.detail ? ` · ${w.detail}` : ""}
                          </div>
                        </>
                      )}
                    </td>
                    <td>
                      <span className={`badge ${w.warranty.covered ? (w.warranty.endingSoon ? "warn" : "ok") : "plain"}`}>
                        {w.warranty.covered ? "Covered" : w.warranty.state === "none" ? "None" : "Expired"}
                      </span>
                      <div className="t-sub" style={{ marginTop: 4 }}>
                        {w.warranty.label}
                      </div>
                      {w.claimWarranty && (
                        <div className="t-sub" style={{ color: "var(--teal-d)", fontWeight: 600, marginTop: 3 }}>
                          Claim now — free replacement
                        </div>
                      )}
                    </td>
                    <td className="num">{money(p.unitCost)}</td>
                    <td>
                      <span className={`badge ${p.status === "In service" ? "ok" : retired ? "plain" : "warn"}`}>{p.status}</span>
                    </td>
                    <td>
                      <RowMenu
                        items={[
                          ...(!retired
                            ? [
                                { label: w.claimWarranty ? "Claim warranty" : "Replace part", icon: Repeat, onSelect: () => setReplacing(p) },
                                {
                                  label: "Raise maintenance",
                                  icon: Wrench,
                                  onSelect: () =>
                                    onSchedule({
                                      vehicleId: vehicle.id, type: "Corrective", description: `Replace ${p.partName} (${p.code})`,
                                      tasks: [{ description: `Replace ${p.partName}${p.serial ? ` (${p.serial})` : ""}`, partCategoryName: p.categoryName, cost: p.unitCost }],
                                    }),
                                },
                                { separator: true as const },
                              ]
                            : []),
                          { label: "Edit part", icon: Pencil, onSelect: () => setForm({ open: true, part: p }) },
                          {
                            label: "Remove part",
                            icon: Trash2,
                            danger: true,
                            onSelect: () =>
                              ask({
                                title: "Remove part",
                                message: `${p.partName} (${p.serial ?? p.code}) will be removed from this vehicle.`,
                                run: () => vehiclesApi.parts.remove(vehicle.id, p.id),
                                done: "Part removed",
                              }),
                          },
                        ]}
                      />
                    </td>
                  </tr>
                );
              }}
            />
          </tbody>
        </table>
      </div>
      <PartFormModal
        open={form.open}
        vehicle={vehicle}
        part={form.part}
        onClose={() => setForm({ open: false, part: null })}
        onSaved={() => {
          setForm({ open: false, part: null });
          changed();
        }}
      />
      <ReplacePartModal
        vehicle={vehicle}
        part={replacing}
        onClose={() => setReplacing(null)}
        onSaved={() => {
          setReplacing(null);
          changed();
        }}
      />
      {dialog}
    </TabCard>
  );
}

// =====================================================================
// Issues
// =====================================================================
export function IssuesTab({ vehicle, onChanged, onSchedule }: TabProps) {
  const toast = useToast();
  const { rows, error, reload } = useRows<VehicleIssue>(() => vehiclesApi.issues.list(vehicle.id), [vehicle.id]);
  const [open, setOpen] = useState(false);
  const changed = () => (reload(), onChanged());
  const { ask, dialog } = useRemove(changed);
  const setStatus = async (i: VehicleIssue, status: string) => {
    try {
      await vehiclesApi.issues.setStatus(vehicle.id, i.id, status);
      toast(status === "Resolved" ? "Issue resolved" : "Issue reopened");
      changed();
    } catch (err) {
      toast(err instanceof Error ? err.message : "Could not update the issue", "bad");
    }
  };
  return (
    <TabCard title="Issues" addLabel="Report issue" onAdd={() => setOpen(true)}>
      <div className="tbl-wrap vt">
        <table>
          <thead>
            <tr>
              <th>ID</th>
              <th>Issue</th>
              <th>Part</th>
              <th>Priority</th>
              <th>Source</th>
              <th>Reported</th>
              <th>Status</th>
              <th style={{ textAlign: "right" }}>Actions</th>
            </tr>
          </thead>
          <tbody>
            <Rows
              rows={rows}
              error={error}
              retry={reload}
              cols={8}
              empty={{ icon: <AlertTriangle />, title: "No issues on this vehicle", text: "Driver reports and failed inspection items land here." }}
              render={(i) => (
                <tr key={i.id}>
                  <td className="mono" style={mono12}>
                    {i.code}
                  </td>
                  <td className="wrap">
                    <div className="t-main">{i.title}</div>
                    {i.note && <div className="t-sub">{i.note}</div>}
                  </td>
                  <td>{i.partName ? <span className="tag">{i.partName}</span> : "—"}</td>
                  <td>
                    <span className={`badge ${i.priority === "High" ? "bad" : i.priority === "Medium" ? "warn" : "plain"}`}>{i.priority}</span>
                  </td>
                  <td className="t-sub">{i.source}</td>
                  <td className="mono" style={mono12}>
                    {formatDay(i.reportedDate)}
                  </td>
                  <td>
                    <span className={`badge ${i.status === "Open" ? "bad" : i.status === "In progress" ? "warn" : "ok"}`}>{i.status}</span>
                  </td>
                  <td>
                    <RowMenu
                      items={[
                        ...(i.status !== "Resolved" ? [{ label: "Raise maintenance", icon: Wrench, onSelect: () => onSchedule(issueToWorkOrder(i, vehicle.id)) }] : []),
                        i.status === "Resolved"
                          ? { label: "Reopen issue", icon: RotateCcw, onSelect: () => setStatus(i, "Open") }
                          : { label: "Mark resolved", icon: Check, onSelect: () => setStatus(i, "Resolved") },
                        { separator: true },
                        {
                          label: "Delete issue",
                          icon: Trash2,
                          danger: true,
                          onSelect: () =>
                            ask({ title: "Delete issue", message: `${i.code} — ${i.title} will be deleted.`, run: () => vehiclesApi.issues.remove(vehicle.id, i.id), done: "Issue deleted" }),
                        },
                      ]}
                    />
                  </td>
                </tr>
              )}
            />
          </tbody>
        </table>
      </div>
      <IssueFormModal
        open={open}
        vehicle={vehicle}
        onClose={() => setOpen(false)}
        onSaved={() => {
          setOpen(false);
          changed();
        }}
      />
      {dialog}
    </TabCard>
  );
}

// =====================================================================
// Fuel
// =====================================================================
export function FuelTab({ vehicle, onChanged }: TabProps) {
  const { rows, error, reload } = useRows<FuelRecord>(() => vehiclesApi.fuel.list(vehicle.id), [vehicle.id]);
  const [open, setOpen] = useState(false);
  const changed = () => (reload(), onChanged());
  const { ask, dialog } = useRemove(changed);
  return (
    <TabCard title="Fuel records" addLabel="Add fuel entry" onAdd={() => setOpen(true)}>
      <div className="tbl-wrap vt">
        <table>
          <thead>
            <tr>
              <th>Date</th>
              <th>Station</th>
              <th className="num">Quantity</th>
              <th className="num">Cost</th>
              <th className="num">Odometer</th>
              <th className="num">Consumption</th>
              <th style={{ textAlign: "right" }}>Actions</th>
            </tr>
          </thead>
          <tbody>
            <Rows
              rows={rows}
              error={error}
              retry={reload}
              cols={7}
              empty={{ icon: <Fuel />, title: "No fuel entries", text: "Log a fill-up to start tracking consumption." }}
              render={(r) => (
                <tr key={r.id}>
                  <td className="mono" style={mono12}>
                    {formatDay(r.date)}
                  </td>
                  <td>{r.station ?? "—"}</td>
                  <td className="num">
                    {num(r.quantity, 2)} {r.unit}
                  </td>
                  <td className="num">{money(r.totalAmount)}</td>
                  <td className="num">{num(r.odometer)}</td>
                  <td className="num t-sub">{r.consumptionPer100 != null ? `${r.consumptionPer100} ${r.unit}/100km` : "—"}</td>
                  <td>
                    <RowMenu
                      items={[
                        {
                          label: "Delete entry",
                          icon: Trash2,
                          danger: true,
                          onSelect: () =>
                            ask({ title: "Delete fuel entry", message: `The ${formatDay(r.date)} fill-up will be removed.`, run: () => vehiclesApi.fuel.remove(vehicle.id, r.id), done: "Fuel entry removed" }),
                        },
                      ]}
                    />
                  </td>
                </tr>
              )}
            />
          </tbody>
        </table>
      </div>
      <FuelFormModal
        open={open}
        vehicle={vehicle}
        onClose={() => setOpen(false)}
        onSaved={() => {
          setOpen(false);
          changed();
        }}
      />
      {dialog}
    </TabCard>
  );
}

// =====================================================================
// Kilometer records
// =====================================================================
export function OdometerTab({ vehicle, onChanged }: TabProps) {
  const { rows, error, reload } = useRows<OdometerReadingRow>(() => vehiclesApi.readings.list(vehicle.id), [vehicle.id]);
  const [open, setOpen] = useState(false);
  const changed = () => (reload(), onChanged());
  const { ask, dialog } = useRemove(changed);
  return (
    <TabCard title="Kilometer records" addLabel="Add reading" onAdd={() => setOpen(true)}>
      <div className="tbl-wrap vt">
        <table>
          <thead>
            <tr>
              <th>Date</th>
              <th className="num">Reading</th>
              <th className="num">Distance since previous</th>
              <th>Source</th>
              <th>Recorded by</th>
              <th style={{ textAlign: "right" }}>Actions</th>
            </tr>
          </thead>
          <tbody>
            <Rows
              rows={rows}
              error={error}
              retry={reload}
              cols={6}
              empty={{ icon: <Gauge />, title: "No readings logged", text: "Readings drive the usage-based PM triggers." }}
              render={(r) => (
                <tr key={r.id}>
                  <td className="mono" style={mono12}>
                    {formatDay(r.date)}
                  </td>
                  <td className="num" style={{ fontWeight: 600 }}>
                    {num(r.value)} {vehicle.readingUnit}
                  </td>
                  <td className="num t-sub">{r.distanceSincePrevious != null ? `+${num(r.distanceSincePrevious)}` : "—"}</td>
                  <td className="t-sub">{r.source}</td>
                  <td>{r.recordedBy ?? "—"}</td>
                  <td>
                    <RowMenu
                      items={[
                        {
                          label: "Delete reading",
                          icon: Trash2,
                          danger: true,
                          onSelect: () =>
                            ask({
                              title: "Delete reading",
                              message: `The ${num(r.value)} ${vehicle.readingUnit} reading of ${formatDay(r.date)} will be removed; the current odometer becomes the highest remaining reading.`,
                              run: () => vehiclesApi.readings.remove(vehicle.id, r.id),
                              done: "Reading removed",
                            }),
                        },
                      ]}
                    />
                  </td>
                </tr>
              )}
            />
          </tbody>
        </table>
      </div>
      <ReadingFormModal
        open={open}
        vehicle={vehicle}
        onClose={() => setOpen(false)}
        onSaved={() => {
          setOpen(false);
          changed();
        }}
      />
      {dialog}
    </TabCard>
  );
}

// =====================================================================
// Maintenance
// =====================================================================
export function MaintenanceTab({ vehicle, onChanged, onSchedule }: TabProps) {
  const { rows, error, reload } = useRows<WorkOrderRow>(() => workOrdersApi.list({ vehicleId: vehicle.id }).then((r) => r.items), [vehicle.id, vehicle.counts.maintenance]);
  return (
    <TabCard title="Work orders" addLabel="Schedule maintenance" onAdd={() => onSchedule({ vehicleId: vehicle.id })}>
      {error ? (
        <div className="empty">
          <AlertTriangle />
          <h3>These records could not be loaded</h3>
          <p>
            <Button variant="secondary" size="sm" onClick={reload}>
              Try again
            </Button>
          </p>
        </div>
      ) : !rows ? (
        <PageSpinner />
      ) : (
        <WorkOrderTable
          rows={rows}
          showVehicle={false}
          onChanged={() => (reload(), onChanged())}
          empty={{ title: "No maintenance history", text: "Scheduled and corrective jobs will show up here." }}
        />
      )}
    </TabCard>
  );
}

// =====================================================================
// Expenses
// =====================================================================
export function ExpensesTab({ vehicle, onChanged }: TabProps) {
  const { rows, error, reload } = useRows<VehicleExpense>(() => vehiclesApi.expenses.list(vehicle.id), [vehicle.id]);
  const [form, setForm] = useState<{ open: boolean; expense: VehicleExpense | null }>({ open: false, expense: null });
  const changed = () => (reload(), onChanged());
  const { ask, dialog } = useRemove(changed);
  const total = rows?.reduce((s, x) => s + x.amount, 0) ?? 0;
  return (
    <TabCard title="Expenses" addLabel="Add expense" onAdd={() => setForm({ open: true, expense: null })}>
      <div className="tbl-wrap vt">
        <table>
          <thead>
            <tr>
              <th>Date</th>
              <th>Title</th>
              <th>Type</th>
              <th>Note</th>
              <th className="num">Amount</th>
              <th style={{ textAlign: "right" }}>Actions</th>
            </tr>
          </thead>
          <tbody>
            <Rows
              rows={rows}
              error={error}
              retry={reload}
              cols={6}
              empty={{ icon: <Receipt />, title: "No expenses recorded", text: "" }}
              render={(x) => (
                <tr key={x.id}>
                  <td className="mono" style={mono12}>
                    {formatDay(x.date)}
                  </td>
                  <td className="t-main wrap">{x.title}</td>
                  <td>
                    <span className="tag">
                      {x.typeColor && <i style={{ display: "inline-block", width: 8, height: 8, borderRadius: 2, background: x.typeColor, marginRight: 6 }} />}
                      {x.typeName}
                    </span>
                  </td>
                  <td className="t-sub wrap">{x.note ?? ""}</td>
                  <td className="num" style={{ fontWeight: 600 }}>
                    {money(x.amount)}
                  </td>
                  <td>
                    <RowMenu
                      items={[
                        { label: "Edit expense", icon: Pencil, onSelect: () => setForm({ open: true, expense: x }) },
                        {
                          label: "Delete expense",
                          icon: Trash2,
                          danger: true,
                          onSelect: () =>
                            ask({ title: "Delete expense", message: `${x.title} (${money(x.amount)}) will be deleted.`, run: () => vehiclesApi.expenses.remove(vehicle.id, x.id), done: "Expense deleted" }),
                        },
                      ]}
                    />
                  </td>
                </tr>
              )}
            />
          </tbody>
          {!!rows?.length && (
            <tfoot>
              <tr>
                <td colSpan={4} style={{ textAlign: "right", padding: "11px 14px", fontFamily: "var(--mono)", fontSize: 11, letterSpacing: ".1em", textTransform: "uppercase", color: "var(--muted)" }}>
                  Total
                </td>
                <td className="num" style={{ fontWeight: 700, padding: "11px 14px" }}>
                  {money(total)}
                </td>
                <td />
              </tr>
            </tfoot>
          )}
        </table>
      </div>
      <ExpenseFormModal
        open={form.open}
        vehicle={vehicle}
        expense={form.expense}
        onClose={() => setForm({ open: false, expense: null })}
        onSaved={() => {
          setForm({ open: false, expense: null });
          changed();
        }}
      />
      {dialog}
    </TabCard>
  );
}

// =====================================================================
// Inspections (read-only until the Inspections module lands)
// =====================================================================
export function InspectionsTab({ vehicle, onInspect, onResume }: TabProps) {
  const { rows, error, reload } = useRows<VehicleInspection>(() => vehiclesApi.inspections(vehicle.id), [vehicle.id]);
  const [viewId, setViewId] = useState<string | null>(null);
  return (
    <TabCard title="Inspections" addLabel="Run inspection" onAdd={onInspect}>
      <div className="tbl-wrap vt">
        <table>
          <thead>
            <tr>
              <th>ID</th>
              <th>Template</th>
              <th>Date</th>
              <th className="num">Odometer</th>
              <th>Inspector</th>
              <th>Result</th>
              <th style={{ textAlign: "right" }}>Actions</th>
            </tr>
          </thead>
          <tbody>
            <Rows
              rows={rows}
              error={error}
              retry={reload}
              cols={7}
              empty={{ icon: <ClipboardList />, title: "Never inspected", text: "Run the first checklist against a template." }}
              render={(r) => {
                const draft = r.status !== "Completed";
                return (
                  <tr key={r.id} className="clickable" onClick={() => (draft ? onResume(r.id) : setViewId(r.id))}>
                    <td className="mono" style={mono12}>
                      {r.code}
                    </td>
                    <td className="wrap">{r.templateName}</td>
                    <td className="mono" style={mono12}>
                      {formatDay(r.date)}
                    </td>
                    <td className="num">{num(r.odometer)}</td>
                    <td>{r.inspectorName ?? "—"}</td>
                    <td>
                      {draft ? (
                        <span className="badge warn">In progress</span>
                      ) : (
                        <span className={`badge ${r.failed ? "bad" : "ok"}`}>
                          {r.passed} pass · {r.failed} fail
                        </span>
                      )}
                    </td>
                    <td>
                      <RowMenu
                        items={[
                          draft
                            ? { label: "Resume inspection", icon: Eye, onSelect: () => onResume(r.id) }
                            : { label: "Open inspection", icon: Eye, onSelect: () => setViewId(r.id) },
                        ]}
                      />
                    </td>
                  </tr>
                );
              }}
            />
          </tbody>
        </table>
      </div>
      <InspectionViewModal inspectionId={viewId} onClose={() => setViewId(null)} />
    </TabCard>
  );
}

// =====================================================================
// Driver history
// =====================================================================
export function DriversTab({ vehicle, onHandOff }: TabProps) {
  const navigate = useNavigate();
  const { rows, error, reload } = useRows<VehicleAssignment>(() => vehiclesApi.assignments(vehicle.id), [vehicle.id, vehicle.currentDriver?.id]);
  return (
    <TabCard title="Driver history" addLabel={vehicle.currentDriver ? "Hand off" : "Assign driver"} onAdd={onHandOff}>
      <div className="tbl-wrap vt">
        <table>
          <thead>
            <tr>
              <th>Driver</th>
              <th>Licence</th>
              <th>From</th>
              <th>To</th>
              <th>Note</th>
              <th>State</th>
            </tr>
          </thead>
          <tbody>
            <Rows
              rows={rows}
              error={error}
              retry={reload}
              cols={6}
              empty={{ icon: <UsersIcon />, title: "No assignment history", text: "" }}
              render={(a) => (
                <tr key={a.id} className="clickable" onClick={() => navigate(`/drivers/${a.driverId}`)}>
                  <td>
                    <div className="flex">
                      <span className="avatar-sm">{initials(a.driverName)}</span>
                      <span className="t-main">{a.driverName}</span>
                    </div>
                  </td>
                  <td className="mono" style={mono12}>
                    {a.licenseNumber ?? "—"}
                  </td>
                  <td className="mono" style={mono12}>
                    {formatDay(a.from)}
                  </td>
                  <td className="mono" style={mono12}>
                    {a.to ? formatDay(a.to) : "—"}
                  </td>
                  <td className="t-sub">{a.note ?? ""}</td>
                  <td>
                    <span className={`badge ${a.current ? "ok" : "plain"}`}>{a.current ? "Current" : new Date(a.from) > new Date() ? "Upcoming" : "Ended"}</span>
                  </td>
                </tr>
              )}
            />
          </tbody>
        </table>
      </div>
    </TabCard>
  );
}
