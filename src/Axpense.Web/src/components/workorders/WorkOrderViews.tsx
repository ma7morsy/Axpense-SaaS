import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { AlertTriangle, CalendarClock, Check, Eye, Pencil, Play, Trash2, Wrench } from "lucide-react";
import { Modal } from "../ui/Modal";
import { Button } from "../ui/Button";
import { PageSpinner } from "../ui/Spinner";
import { RowMenu, type RowMenuItem } from "../ui/RowMenu";
import { ConfirmDialog } from "../ui/ConfirmDialog";
import { useToast } from "../ui/Toast";
import { workOrdersApi } from "../../lib/api";
import type { WorkOrderDetail, WorkOrderRow } from "../../lib/types";
import { formatDay } from "../../lib/utils";
import { RecordFormModal } from "../vehicles/RecordFormModal";
import { WorkOrderFormModal } from "./WorkOrderFormModal";
import { egp, priorityTone, woStatusTone } from "./workorderUi";
import "./workorders.css";

const num = (v: number | null | undefined) => (v == null ? "—" : Number(v).toLocaleString("en-US", { maximumFractionDigits: 1 }));

// =====================================================================
// View
// =====================================================================
export function WorkOrderViewModal({
  workOrderId,
  onClose,
  onEdit,
  onAdvance,
}: {
  workOrderId: string | null;
  onClose: () => void;
  onEdit: (w: WorkOrderDetail) => void;
  onAdvance: (w: WorkOrderDetail) => void;
}) {
  const navigate = useNavigate();
  const [w, setW] = useState<WorkOrderDetail | null>(null);
  const [error, setError] = useState(false);
  useEffect(() => {
    setW(null);
    setError(false);
    if (workOrderId) workOrdersApi.get(workOrderId).then(setW).catch(() => setError(true));
  }, [workOrderId]);

  const source =
    w &&
    [
      w.source !== "Manual" ? `Source: ${w.source}${w.sourceRef ? ` ${w.sourceRef}` : ""}` : "Source: manual",
      w.odometerAtRaise != null ? `odometer at raise ${num(w.odometerAtRaise)} ${w.readingUnit}` : null,
      w.odometerAtService != null ? `at service ${num(w.odometerAtService)} ${w.readingUnit}` : null,
    ]
      .filter(Boolean)
      .join(" · ");

  return (
    <Modal
      open={!!workOrderId}
      onClose={onClose}
      size="wide"
      title={w?.code ?? "Work order"}
      description={w ? `${w.vehicleName} · ${w.plateNumber}` : undefined}
      footer={
        <>
          {w && (
            <div style={{ marginRight: "auto" }}>
              {w.nextStatus && (
                <Button variant="secondary" onClick={() => onAdvance(w)}>
                  <Check /> Move to next status
                </Button>
              )}
              {w.status !== "Completed" && (
                <Button variant="secondary" onClick={() => onEdit(w)}>
                  <Pencil /> Edit
                </Button>
              )}
            </div>
          )}
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
        </>
      }
    >
      {error ? (
        <div className="empty">
          <AlertTriangle />
          <h3>The work order could not be loaded</h3>
        </div>
      ) : !w ? (
        <PageSpinner />
      ) : (
        <>
          <div className="grid g-4" style={{ marginBottom: 16 }}>
            <div className="stat">
              <div className="k">Status</div>
              <div style={{ marginTop: 12 }}>
                <span className={`badge ${woStatusTone(w.displayStatus)}`}>{w.displayStatus}</span>
              </div>
              {w.daysOverdue ? <div className="m">{w.daysOverdue} days late</div> : null}
            </div>
            <div className="stat neutral wo-stat">
              <div className="k">Type</div>
              <div className="v sm">{w.type}</div>
              <div className="m">{w.priority} priority</div>
            </div>
            <div className="stat neutral wo-stat">
              <div className="k">Scheduled</div>
              <div className="v sm">{formatDay(w.scheduledDate)}</div>
              <div className="m">{w.technicianName ?? "Unassigned"}</div>
            </div>
            <div className="stat wo-stat">
              <div className="k">{w.status === "Completed" ? "Actual cost" : "Total cost"}</div>
              <div className="v sm">{egp(w.total)}</div>
              <div className="m">
                {w.tasks.length} task{w.tasks.length === 1 ? "" : "s"}
              </div>
            </div>
          </div>
          <div style={{ fontWeight: 600, color: "var(--ink)" }}>{w.description}</div>
          <div className="t-sub" style={{ marginTop: 4 }}>
            {source}
            {w.completedAtUtc ? ` · completed ${formatDay(w.completedAtUtc)}` : ""}
          </div>
          <div className="wo-tasks">
            <table>
              <thead>
                <tr>
                  <th>Task</th>
                  <th>Part category</th>
                  <th>Task category</th>
                  <th className="num">Cost</th>
                </tr>
              </thead>
              <tbody>
                {w.tasks.map((t) => (
                  <tr key={t.id}>
                    <td className="t-main">{t.description}</td>
                    <td>{t.partCategoryName ? <span className="tag">{t.partCategoryName}</span> : "—"}</td>
                    <td>{t.taskCategoryName ?? "—"}</td>
                    <td className="num">{egp(t.cost)}</td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr>
                  <td colSpan={3} style={{ textAlign: "right", fontFamily: "var(--mono)", fontSize: 11, letterSpacing: ".1em", textTransform: "uppercase", color: "var(--muted)", padding: "11px 14px" }}>
                    Total
                  </td>
                  <td className="num" style={{ padding: "11px 14px" }}>
                    {egp(w.total)}
                  </td>
                </tr>
              </tfoot>
            </table>
          </div>
          {w.notes && (
            <div className="note" style={{ marginTop: 14, whiteSpace: "pre-wrap" }}>
              {w.notes}
            </div>
          )}
        </>
      )}
    </Modal>
  );
}

// =====================================================================
// Table + all the flows (view, edit, status, complete, delete)
// =====================================================================
export function WorkOrderTable({
  rows,
  showVehicle = true,
  onChanged,
  empty,
}: {
  rows: WorkOrderRow[];
  showVehicle?: boolean;
  onChanged: () => void;
  empty: { title: string; text: string };
}) {
  const toast = useToast();
  const navigate = useNavigate();
  const [viewId, setViewId] = useState<string | null>(null);
  const [editing, setEditing] = useState<WorkOrderDetail | null>(null);
  const [completing, setCompleting] = useState<{ id: string; code: string; unit: string; odometer?: number | null } | null>(null);
  const [deleting, setDeleting] = useState<WorkOrderRow | null>(null);

  const setStatus = async (id: string, code: string, status: string) => {
    if (status === "Completed") {
      try {
        const w = await workOrdersApi.get(id);
        setCompleting({ id, code, unit: w.readingUnit, odometer: w.odometerAtRaise });
      } catch {
        toast("Could not open the work order", "bad");
      }
      return;
    }
    try {
      await workOrdersApi.setStatus(id, status);
      toast(`${code} is now ${status.toLowerCase()}`);
      onChanged();
    } catch (err) {
      toast(err instanceof Error ? err.message : "Could not change the status", "bad");
    }
  };

  const openEdit = async (id: string) => {
    try {
      setEditing(await workOrdersApi.get(id));
    } catch {
      toast("Could not open the work order", "bad");
    }
  };

  const cols = showVehicle ? 10 : 9;
  return (
    <>
      <div className="tbl-wrap">
        <table>
          <thead>
            <tr>
              <th>Order</th>
              {showVehicle && <th>Vehicle</th>}
              <th>Description</th>
              <th>Type</th>
              <th>Priority</th>
              <th>Scheduled</th>
              <th>Technician</th>
              <th className="num">Total</th>
              <th>Status</th>
              <th style={{ textAlign: "right" }}>Actions</th>
            </tr>
          </thead>
          <tbody>
            {rows.length ? (
              rows.map((w) => {
                const done = w.status === "Completed";
                const items: RowMenuItem[] = [
                  { label: "View", icon: Eye, onSelect: () => setViewId(w.id) },
                  ...(!done ? [{ label: "Edit", icon: Pencil, onSelect: () => openEdit(w.id) }] : []),
                  ...(!done
                    ? ([
                        { separator: true },
                        ...(w.status !== "Scheduled" ? [{ label: "Mark scheduled", icon: CalendarClock, onSelect: () => setStatus(w.id, w.code, "Scheduled") }] : []),
                        ...(w.status !== "In progress" ? [{ label: "Start work (in progress)", icon: Play, onSelect: () => setStatus(w.id, w.code, "In progress") }] : []),
                        { label: "Mark completed", icon: Check, onSelect: () => setStatus(w.id, w.code, "Completed") },
                      ] as RowMenuItem[])
                    : []),
                  ...(showVehicle ? [{ label: "Open vehicle", icon: Wrench, onSelect: () => navigate(`/vehicles/${w.vehicleId}?tab=maintenance`) }] : []),
                  { separator: true },
                  { label: "Delete", icon: Trash2, danger: true, onSelect: () => setDeleting(w) },
                ];
                return (
                  <tr key={w.id} className="clickable" onClick={() => setViewId(w.id)}>
                    <td className="mono" style={{ fontSize: 12, whiteSpace: "nowrap" }}>
                      {w.code}
                    </td>
                    {showVehicle && (
                      <td>
                        <div className="t-main">{w.vehicleName}</div>
                        <div className="t-sub">
                          <span className="plate">{w.plateNumber}</span>
                        </div>
                      </td>
                    )}
                    <td style={{ minWidth: 220 }}>
                      <div className="t-main">{w.description}</div>
                      <div className="t-sub">
                        {w.taskCount} task{w.taskCount === 1 ? "" : "s"}
                        {w.source !== "Manual" ? ` · ${w.source}${w.sourceRef ? ` ${w.sourceRef}` : ""}` : ""}
                      </div>
                    </td>
                    <td>
                      <span className="tag">{w.type}</span>
                    </td>
                    <td>
                      <span className={`badge ${priorityTone(w.priority)}`}>{w.priority}</span>
                    </td>
                    <td className="mono" style={{ fontSize: 12, whiteSpace: "nowrap" }}>
                      {formatDay(w.scheduledDate)}
                    </td>
                    <td>{w.technicianName ?? <span className="t-sub">Unassigned</span>}</td>
                    <td className="num" style={{ whiteSpace: "nowrap" }}>
                      {egp(w.total)}
                    </td>
                    <td>
                      <span className={`badge ${woStatusTone(w.displayStatus)}`} title={w.daysOverdue ? `${w.daysOverdue} days late` : undefined}>
                        {w.displayStatus}
                      </span>
                    </td>
                    <td>
                      <RowMenu items={items} />
                    </td>
                  </tr>
                );
              })
            ) : (
              <tr>
                <td colSpan={cols}>
                  <div className="empty">
                    <Wrench />
                    <h3>{empty.title}</h3>
                    {empty.text && <p>{empty.text}</p>}
                  </div>
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <WorkOrderViewModal
        workOrderId={viewId}
        onClose={() => setViewId(null)}
        onEdit={(w) => {
          setViewId(null);
          setEditing(w);
        }}
        onAdvance={(w) => {
          setViewId(null);
          if (w.nextStatus) setStatus(w.id, w.code, w.nextStatus);
        }}
      />
      <WorkOrderFormModal
        open={!!editing}
        workOrder={editing}
        onClose={() => setEditing(null)}
        onSaved={() => {
          setEditing(null);
          onChanged();
        }}
      />
      <RecordFormModal
        open={!!completing}
        title={`Complete ${completing?.code ?? ""}`}
        description="The order is locked afterwards. A preventive order restarts the vehicle's PM runway; a linked issue is resolved."
        submitLabel="Mark completed"
        fields={[{ name: "odometer", label: `Reading at service (${completing?.unit ?? "km"})`, type: "number", hint: "Leave empty to use the vehicle's current reading" }]}
        initial={{ odometer: "" }}
        onClose={() => setCompleting(null)}
        onSubmit={async (body) => {
          await workOrdersApi.setStatus(completing!.id, "Completed", (body.odometer as number | null) ?? null);
          toast(`${completing!.code} completed`);
          setCompleting(null);
          onChanged();
        }}
      />
      <ConfirmDialog
        open={!!deleting}
        title="Delete work order"
        message={`${deleting?.code ?? "This work order"} and its tasks will be deleted${deleting?.status === "Completed" ? "; its cost leaves the spend figures" : ""}.`}
        onConfirm={async () => {
          if (!deleting) return;
          try {
            await workOrdersApi.remove(deleting.id);
            toast(`${deleting.code} deleted`);
            setDeleting(null);
            onChanged();
          } catch (err) {
            toast(err instanceof Error ? err.message : "Could not delete the work order", "bad");
          }
        }}
        onClose={() => setDeleting(null)}
      />
    </>
  );
}
