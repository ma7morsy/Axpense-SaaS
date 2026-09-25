import { useEffect, useMemo, useState, type FormEvent } from "react";
import { Plus, Trash2 } from "lucide-react";
import { Modal } from "../ui/Modal";
import { Button } from "../ui/Button";
import { useToast } from "../ui/Toast";
import { ApiError, vehiclesApi, workOrdersApi } from "../../lib/api";
import type { Vehicle, WorkOrderDetail } from "../../lib/types";
import { todayPlusDays, toDateInput } from "../../lib/utils";
import { egp, resetWorkOrderOptions, useWorkOrderOptions } from "./workorderUi";
import "./workorders.css";

export interface WorkOrderPreset {
  vehicleId?: string;
  type?: string;
  priority?: string;
  description?: string;
  issueId?: string;
  tasks?: { description: string; partCategoryName?: string | null; cost?: number | null }[];
}

interface TaskRow {
  key: number;
  description: string;
  partCategoryId: string;
  taskCategoryId: string;
  cost: string;
}
let seq = 0;

/** New / edit work order — multi-task job; the total adds up as you type. */
export function WorkOrderFormModal({
  open,
  workOrder,
  preset,
  onClose,
  onSaved,
}: {
  open: boolean;
  workOrder: WorkOrderDetail | null;
  preset?: WorkOrderPreset;
  onClose: () => void;
  onSaved: (w: WorkOrderDetail) => void;
}) {
  const toast = useToast();
  const options = useWorkOrderOptions();
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);
  const [form, setForm] = useState({ vehicleId: "", type: "Preventive", priority: "Medium", status: "Scheduled", scheduledDate: "", technicianUserId: "", description: "", notes: "" });
  const [tasks, setTasks] = useState<TaskRow[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (open) {
      resetWorkOrderOptions();
      vehiclesApi.list().then(setVehicles).catch(() => setVehicles([]));
    }
  }, [open]);

  useEffect(() => {
    if (!open || !options) return;
    const firstPart = options.partCategories[0]?.id ?? "";
    const firstTask = options.taskCategories[0]?.id ?? "";
    const byName = (name?: string | null) => options.partCategories.find((c) => c.name === name)?.id ?? firstPart;
    setErrors({});
    if (workOrder) {
      setForm({
        vehicleId: workOrder.vehicleId, type: workOrder.type, priority: workOrder.priority, status: workOrder.status,
        scheduledDate: toDateInput(workOrder.scheduledDate), technicianUserId: workOrder.technicianUserId ?? "",
        description: workOrder.description, notes: workOrder.notes ?? "",
      });
      setTasks(
        workOrder.tasks.map((t) => ({
          key: ++seq, description: t.description, partCategoryId: t.partCategoryId ?? "", taskCategoryId: t.taskCategoryId ?? "", cost: String(t.cost),
        }))
      );
    } else {
      setForm({
        vehicleId: preset?.vehicleId ?? "", type: preset?.type ?? "Preventive", priority: preset?.priority ?? "Medium", status: "Scheduled",
        scheduledDate: todayPlusDays(0), technicianUserId: "", description: preset?.description ?? "", notes: "",
      });
      setTasks(
        preset?.tasks?.length
          ? preset.tasks.map((t) => ({ key: ++seq, description: t.description, partCategoryId: byName(t.partCategoryName), taskCategoryId: firstTask, cost: t.cost ? String(t.cost) : "" }))
          : [{ key: ++seq, description: "", partCategoryId: firstPart, taskCategoryId: firstTask, cost: "" }]
      );
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, options, workOrder]);

  const total = useMemo(() => tasks.reduce((s, t) => s + (Number(t.cost) || 0), 0), [tasks]);
  const set = (k: keyof typeof form, v: string) => {
    setForm((f) => ({ ...f, [k]: v }));
    setErrors(({ [k]: _r, ...rest }) => rest);
  };
  const setTask = (key: number, patch: Partial<TaskRow>) => setTasks((ts) => ts.map((t) => (t.key === key ? { ...t, ...patch } : t)));

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const missing: Record<string, string> = {};
    (["vehicleId", "type", "scheduledDate", "description"] as const).forEach((k) => !form[k].trim() && (missing[k] = "Required"));
    const filled = tasks.filter((t) => t.description.trim() || Number(t.cost));
    if (!filled.length) missing.tasks = "Add at least one task.";
    if (Object.keys(missing).length) {
      setErrors(missing);
      toast("Fill in the highlighted fields to continue", "bad");
      return;
    }
    const body = {
      vehicleId: form.vehicleId, type: form.type, priority: form.priority, status: form.status, scheduledDate: form.scheduledDate,
      technicianUserId: form.technicianUserId || null, description: form.description.trim(), notes: form.notes.trim() || null,
      issueId: workOrder ? workOrder.issueId ?? null : preset?.issueId ?? null,
      tasks: filled.map((t) => ({
        description: t.description.trim(), partCategoryId: t.partCategoryId || null, taskCategoryId: t.taskCategoryId || null, cost: t.cost === "" ? 0 : Number(t.cost),
      })),
    };
    setSaving(true);
    try {
      const saved = workOrder ? await workOrdersApi.update(workOrder.id, body) : await workOrdersApi.create(body);
      toast(workOrder ? `${saved.code} updated` : `${saved.code} created`);
      onSaved(saved);
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.details).length) setErrors(err.details);
      toast(err instanceof Error ? err.message : "Could not save the work order", "bad");
    } finally {
      setSaving(false);
    }
  };

  const bad = (k: string) => (errors[k] ? { borderColor: "var(--red)" } : undefined);
  const msg = (k: string) =>
    errors[k] && errors[k] !== "Required" ? (
      <span className="hint" style={{ color: "var(--red)" }}>
        {errors[k]}
      </span>
    ) : null;
  const sel = (k: keyof typeof form, list: { value: string; label: string }[], blank?: string) => (
    <select id={`wo-${k}`} value={form[k]} onChange={(e) => set(k, e.target.value)} style={bad(k)}>
      {blank !== undefined && <option value="">{blank}</option>}
      {list.map((o) => (
        <option key={o.value} value={o.value}>
          {o.label}
        </option>
      ))}
    </select>
  );
  const plain = (xs: string[] | undefined) => (xs ?? []).map((x) => ({ value: x, label: x }));
  const vehicleOptions = vehicles
    .filter((v) => v.status !== "Retired" || v.id === form.vehicleId)
    .map((v) => ({ value: v.id, label: `${v.name} · ${v.plateNumber}` }));

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="wide"
      title={workOrder ? `Edit ${workOrder.code}` : "New work order"}
      description="Multi-task job — the total adds up as you type."
      footer={
        <>
          <Button variant="secondary" type="button" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="wo-form" disabled={saving || !options}>
            {saving ? "Saving…" : workOrder ? "Save changes" : "Create work order"}
          </Button>
        </>
      }
    >
      <form id="wo-form" onSubmit={submit} noValidate>
        <div className="form-grid">
          <div className="f">
            <label htmlFor="wo-vehicleId">
              Vehicle<span className="req"> *</span>
            </label>
            {sel("vehicleId", vehicleOptions, "Select a vehicle")}
            {msg("vehicleId")}
          </div>
          <div className="f">
            <label htmlFor="wo-type">
              Type<span className="req"> *</span>
            </label>
            {sel("type", plain(options?.types))}
            {msg("type")}
          </div>
          <div className="f">
            <label htmlFor="wo-priority">Priority</label>
            {sel("priority", plain(options?.priorities))}
          </div>
          <div className="f">
            <label htmlFor="wo-status">Status</label>
            {sel("status", plain(options?.statuses))}
            {msg("status")}
          </div>
          <div className="f">
            <label htmlFor="wo-scheduledDate">
              Scheduled date<span className="req"> *</span>
            </label>
            <input id="wo-scheduledDate" type="date" value={form.scheduledDate} onChange={(e) => set("scheduledDate", e.target.value)} style={bad("scheduledDate")} />
            {msg("scheduledDate")}
          </div>
          <div className="f">
            <label htmlFor="wo-technicianUserId">Assigned technician</label>
            {sel("technicianUserId", (options?.technicians ?? []).map((t) => ({ value: t.id, label: t.name })), "Unassigned")}
            {msg("technicianUserId")}
          </div>
          <div className="f full">
            <label htmlFor="wo-description">
              Description<span className="req"> *</span>
            </label>
            <input id="wo-description" value={form.description} placeholder="What is being done and why" onChange={(e) => set("description", e.target.value)} style={bad("description")} />
            {msg("description")}
          </div>
        </div>

        <div className="fieldset">
          <div className="fs-t">Tasks</div>
          <div className="task-row wo-task-head">
            <span>Task</span>
            <span>Part category</span>
            <span>Task category</span>
            <span style={{ textAlign: "right" }}>Cost</span>
            <span />
          </div>
          {tasks.map((t, ix) => (
            <div key={t.key} className="task-row">
              <div>
                <input value={t.description} placeholder="Describe the task" aria-label={`Task ${ix + 1}`} onChange={(e) => setTask(t.key, { description: e.target.value })} style={bad(`tasks[${ix}].description`)} />
                {msg(`tasks[${ix}].description`)}
              </div>
              <select value={t.partCategoryId} aria-label={`Task ${ix + 1} part category`} onChange={(e) => setTask(t.key, { partCategoryId: e.target.value })}>
                <option value="">—</option>
                {(options?.partCategories ?? []).map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
              <select value={t.taskCategoryId} aria-label={`Task ${ix + 1} task category`} onChange={(e) => setTask(t.key, { taskCategoryId: e.target.value })}>
                <option value="">—</option>
                {(options?.taskCategories ?? []).map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
              <input
                className="mono"
                type="number"
                step="0.01"
                min="0"
                value={t.cost}
                placeholder="0.00"
                aria-label={`Task ${ix + 1} cost`}
                style={{ textAlign: "right", ...(bad(`tasks[${ix}].cost`) ?? {}) }}
                onChange={(e) => setTask(t.key, { cost: e.target.value })}
              />
              <button
                type="button"
                className="act"
                title="Remove task"
                onClick={() => (tasks.length > 1 ? setTasks((ts) => ts.filter((x) => x.key !== t.key)) : toast("A work order needs at least one task", "bad"))}
              >
                <Trash2 />
              </button>
            </div>
          ))}
          {errors.tasks && (
            <div className="t-sub" style={{ color: "var(--red)" }}>
              {errors.tasks}
            </div>
          )}
          <div className="between" style={{ marginTop: 4 }}>
            <Button
              type="button"
              variant="secondary"
              size="sm"
              onClick={() => setTasks((ts) => [...ts, { key: ++seq, description: "", partCategoryId: options?.partCategories[0]?.id ?? "", taskCategoryId: options?.taskCategories[0]?.id ?? "", cost: "" }])}
            >
              <Plus /> Add task
            </Button>
            <div className="wo-total">
              <span>Total estimated cost</span>
              <b>{egp(total, total % 1 ? 2 : 0)}</b>
            </div>
          </div>
        </div>

        <div className="fieldset">
          <div className="fs-t">Notes</div>
          <div className="f full">
            <label htmlFor="wo-notes">Internal notes</label>
            <textarea id="wo-notes" rows={4} value={form.notes} onChange={(e) => set("notes", e.target.value)} style={bad("notes")} />
            {msg("notes")}
          </div>
        </div>
      </form>
    </Modal>
  );
}
