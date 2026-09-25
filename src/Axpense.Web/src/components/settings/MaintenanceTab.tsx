import { useEffect, useState } from "react";
import { Check, ListChecks, Pencil, Plus, Trash2, X } from "lucide-react";
import { Button } from "../ui/Button";
import { RowMenu } from "../ui/RowMenu";
import { ConfirmDialog } from "../ui/ConfirmDialog";
import { useToast } from "../ui/Toast";
import { ApiError, pmEngineApi, taskCategoriesApi } from "../../lib/api";
import type { EscalationStep, PmEngine, TaskCategory } from "../../lib/types";
import { SimpleNameModal } from "./SimpleNameModal";
import { PmTaskLibrary } from "./PmTaskLibrary";
import "./settings.css";

/** Settings → Maintenance: work-order task categories, the PM engine rules and the PM task library. */
export function MaintenanceTab({
  categories,
  engine,
  isAdmin,
  onChanged,
}: {
  categories: TaskCategory[];
  engine: PmEngine;
  isAdmin: boolean;
  onChanged: () => Promise<unknown>;
}) {
  return (
    <>
      <div className="mt-grid">
        <TaskCategoriesCard categories={categories} isAdmin={isAdmin} onChanged={onChanged} />
        <PmEngineCard engine={engine} isAdmin={isAdmin} onSaved={onChanged} />
      </div>
      <PmTaskLibrary isAdmin={isAdmin} />
    </>
  );
}

function TaskCategoriesCard({ categories, isAdmin, onChanged }: { categories: TaskCategory[]; isAdmin: boolean; onChanged: () => Promise<unknown> }) {
  const toast = useToast();
  const [form, setForm] = useState<{ category: TaskCategory | null } | null>(null);
  const [removing, setRemoving] = useState<TaskCategory | null>(null);

  return (
    <div className="card">
      <div className="card-h">
        <div>
          <h2>Work order task categories</h2>
          <p className="t-sub">Every work-order task is filed under one of these.</p>
        </div>
        {isAdmin && (
          <Button onClick={() => setForm({ category: null })}>
            <Plus /> Add category
          </Button>
        )}
      </div>
      {categories.length ? (
        categories.map((c) => (
          <div key={c.id} className="tc-row">
            <b>{c.name}</b>
            <span className="cnt">
              {c.taskCount} task{c.taskCount === 1 ? "" : "s"}
            </span>
            {isAdmin && (
              <RowMenu
                items={[
                  { label: "Edit category", icon: Pencil, onSelect: () => setForm({ category: c }) },
                  { separator: true },
                  { label: "Delete category", icon: Trash2, danger: true, onSelect: () => setRemoving(c) },
                ]}
              />
            )}
          </div>
        ))
      ) : (
        <div className="empty">
          <ListChecks />
          <h3>No task categories</h3>
        </div>
      )}

      <SimpleNameModal
        open={!!form}
        title={form?.category ? `Edit ${form.category.name}` : "Add task category"}
        label="Category name"
        placeholder="Hydraulics"
        submitLabel={form?.category ? "Save changes" : "Add category"}
        initialName={form?.category?.name}
        onClose={() => setForm(null)}
        onSubmit={async (v) => {
          if (form?.category) {
            await taskCategoriesApi.update(form.category.id, { name: v.name });
            toast("Task category updated");
          } else {
            await taskCategoriesApi.create({ name: v.name });
            toast(`${v.name} added`);
          }
          setForm(null);
          await onChanged();
        }}
      />
      <ConfirmDialog
        open={!!removing}
        title="Delete task category"
        message={`${removing?.name ?? "This category"} will be removed from the list.`}
        onConfirm={async () => {
          if (!removing) return;
          try {
            await taskCategoriesApi.remove(removing.id);
            toast(`${removing.name} deleted`);
            await onChanged();
          } catch (err) {
            toast(err instanceof Error ? err.message : "Could not delete", "bad");
          }
          setRemoving(null);
        }}
        onClose={() => setRemoving(null)}
      />
    </div>
  );
}

function PmEngineCard({ engine, isAdmin, onSaved }: { engine: PmEngine; isAdmin: boolean; onSaved: () => Promise<unknown> }) {
  const toast = useToast();
  const [form, setForm] = useState(() => toForm(engine));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);
  useEffect(() => setForm(toForm(engine)), [engine]);

  const setNum = (k: "distancePreAlertKm" | "timePreAlertDays" | "engineHourPreAlert") => (e: { target: { value: string } }) =>
    setForm((f) => ({ ...f, [k]: e.target.value }));
  const setStep = (i: number, patch: Partial<{ afterDaysOverdue: string; role: string }>) =>
    setForm((f) => ({ ...f, steps: f.steps.map((s, ix) => (ix === i ? { ...s, ...patch } : s)) }));

  const save = async () => {
    setSaving(true);
    setErrors({});
    try {
      await pmEngineApi.save({
        distancePreAlertKm: Number(form.distancePreAlertKm || 0),
        timePreAlertDays: Number(form.timePreAlertDays || 0),
        engineHourPreAlert: Number(form.engineHourPreAlert || 0),
        autoGenerateWorkOrders: form.autoGenerateWorkOrders,
        blockDispatchOnCriticalOverdue: form.blockDispatchOnCriticalOverdue,
        escalationSteps: form.steps.map((s) => ({ afterDaysOverdue: Number(s.afterDaysOverdue || 0), role: s.role })),
      });
      toast("PM engine rules saved");
      await onSaved();
    } catch (err) {
      if (err instanceof ApiError) setErrors(err.details);
      toast(err instanceof Error ? err.message : "Could not save the rules", "bad");
    } finally {
      setSaving(false);
    }
  };

  const err = (k: string) =>
    errors[k] ? (
      <span className="hint" style={{ color: "var(--red)" }}>
        {errors[k]}
      </span>
    ) : null;
  const ro = !isAdmin;
  const dayLabel = (d: string) => (Number(d) === 0 ? "On due date" : `+${d} days overdue`);

  return (
    <div className="card">
      <div className="card-h">
        <div>
          <h2>PM engine</h2>
          <p className="t-sub">Rules for preventive maintenance alerts and work orders.</p>
        </div>
      </div>
      <div className="card-b">
        <div className="form-grid">
          <div className="f">
            <label htmlFor="pm-km">Distance pre-alert (km)</label>
            <input id="pm-km" type="number" min={0} readOnly={ro} value={form.distancePreAlertKm} onChange={setNum("distancePreAlertKm")} />
            {err("distancePreAlertKm")}
          </div>
          <div className="f">
            <label htmlFor="pm-days">Time pre-alert (days)</label>
            <input id="pm-days" type="number" min={0} readOnly={ro} value={form.timePreAlertDays} onChange={setNum("timePreAlertDays")} />
            {err("timePreAlertDays")}
          </div>
          <div className="f">
            <label htmlFor="pm-hours">Engine hour pre-alert</label>
            <input id="pm-hours" type="number" min={0} readOnly={ro} value={form.engineHourPreAlert} onChange={setNum("engineHourPreAlert")} />
            {err("engineHourPreAlert")}
          </div>
          <div className="f">
            <label htmlFor="pm-auto">Auto work orders</label>
            <select
              id="pm-auto"
              disabled={ro}
              value={form.autoGenerateWorkOrders ? "auto" : "manual"}
              onChange={(e) => setForm((f) => ({ ...f, autoGenerateWorkOrders: e.target.value === "auto" }))}
            >
              <option value="auto">Generate automatically</option>
              <option value="manual">Suggest only (manual)</option>
            </select>
          </div>
          <div className="f full">
            <label htmlFor="pm-safety">Safety rule</label>
            <select
              id="pm-safety"
              disabled={ro}
              value={form.blockDispatchOnCriticalOverdue ? "block" : "warn"}
              onChange={(e) => setForm((f) => ({ ...f, blockDispatchOnCriticalOverdue: e.target.value === "block" }))}
            >
              <option value="block">Block dispatch when critical PM is overdue</option>
              <option value="warn">Warn only — allow dispatch</option>
            </select>
          </div>
          <span className="hint full" style={{ gridColumn: "1/-1", marginTop: -6 }}>
            Service items turn “due” once they are within the distance or time pre-alert.
          </span>
        </div>

        <div className="divider" />
        <div className="fs-t">Escalation chain</div>
        {ro ? (
          <dl className="kv">
            {form.steps.map((s, i) => (
              <div key={i} style={{ display: "contents" }}>
                <dt>{dayLabel(s.afterDaysOverdue)}</dt>
                <dd>{s.role}</dd>
              </div>
            ))}
          </dl>
        ) : (
          <>
            <div className="esc-head">
              <span>Days overdue</span>
              <span>Escalate to</span>
              <span />
            </div>
            {form.steps.map((s, i) => (
              <div key={i} className="esc-row">
                <input
                  type="number"
                  min={0}
                  aria-label={`Step ${i + 1} days overdue`}
                  value={s.afterDaysOverdue}
                  onChange={(e) => setStep(i, { afterDaysOverdue: e.target.value })}
                />
                <select aria-label={`Step ${i + 1} role`} value={s.role} onChange={(e) => setStep(i, { role: e.target.value })}>
                  {engine.roles.map((r) => (
                    <option key={r}>{r}</option>
                  ))}
                </select>
                <button
                  type="button"
                  className="act danger"
                  aria-label="Remove step"
                  disabled={form.steps.length <= 1}
                  onClick={() => setForm((f) => ({ ...f, steps: f.steps.filter((_, ix) => ix !== i) }))}
                >
                  <X />
                </button>
              </div>
            ))}
            {err("escalationSteps")}
            {form.steps.length < 6 && (
              <Button
                variant="ghost"
                size="sm"
                onClick={() =>
                  setForm((f) => ({
                    ...f,
                    steps: [...f.steps, { afterDaysOverdue: String(Math.max(0, ...f.steps.map((s) => Number(s.afterDaysOverdue || 0))) + 7), role: engine.roles[0] }],
                  }))
                }
              >
                <Plus /> Add step
              </Button>
            )}
            <Button style={{ width: "100%", justifyContent: "center", marginTop: 16 }} onClick={save} disabled={saving}>
              <Check /> {saving ? "Saving…" : "Save rules"}
            </Button>
          </>
        )}
      </div>
    </div>
  );
}

function toForm(e: PmEngine) {
  return {
    distancePreAlertKm: String(e.distancePreAlertKm),
    timePreAlertDays: String(e.timePreAlertDays),
    engineHourPreAlert: String(e.engineHourPreAlert),
    autoGenerateWorkOrders: e.autoGenerateWorkOrders,
    blockDispatchOnCriticalOverdue: e.blockDispatchOnCriticalOverdue,
    steps: e.escalationSteps.map((s: EscalationStep) => ({ afterDaysOverdue: String(s.afterDaysOverdue), role: s.role })),
  };
}
