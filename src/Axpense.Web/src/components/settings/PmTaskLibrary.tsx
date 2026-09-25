import { useCallback, useEffect, useState } from "react";
import { ListChecks, Pencil, Plus, Trash2 } from "lucide-react";
import { Button } from "../ui/Button";
import { RowMenu } from "../ui/RowMenu";
import { ConfirmDialog } from "../ui/ConfirmDialog";
import { useToast } from "../ui/Toast";
import { RecordFormModal, type FieldSpec, type FormValues } from "../vehicles/RecordFormModal";
import { catalogApi, pmTasksApi, taskCategoriesApi } from "../../lib/api";
import type { PartCategory, PmTask, TaskCategory } from "../../lib/types";

const TRIGGERS = [
  { value: "usage", label: "Usage — distance" },
  { value: "time", label: "Time — days" },
  { value: "hours", label: "Engine hours" },
  { value: "hybrid", label: "Hybrid — first to hit" },
];
const nf = (n: number) => n.toLocaleString("en-US", { maximumFractionDigits: 2 });

export function intervalText(t: Pick<PmTask, "intervalKm" | "intervalDays" | "intervalHours">) {
  return [t.intervalKm ? `${nf(t.intervalKm)} km` : "", t.intervalDays ? `${t.intervalDays} days` : "", t.intervalHours ? `${nf(t.intervalHours)} hr` : ""]
    .filter(Boolean)
    .join(" · ") || "—";
}

/**
 * Settings → Maintenance → PM task library. The PM engine raises preventive work orders from these tasks
 * (name, part/task category, estimated cost, role, duration). Validation lives in the API.
 */
export function PmTaskLibrary({ isAdmin }: { isAdmin: boolean }) {
  const toast = useToast();
  const [tasks, setTasks] = useState<PmTask[] | null>(null);
  const [parts, setParts] = useState<PartCategory[]>([]);
  const [taskCats, setTaskCats] = useState<TaskCategory[]>([]);
  const [error, setError] = useState("");
  const [form, setForm] = useState<{ task: PmTask | null } | null>(null);
  const [removing, setRemoving] = useState<PmTask | null>(null);

  const load = useCallback(() => {
    setError("");
    return pmTasksApi
      .list()
      .then(setTasks)
      .catch((e) => setError(e instanceof Error ? e.message : "Could not load the PM task library"));
  }, []);
  useEffect(() => {
    load();
    catalogApi.get().then((c) => setParts(c.categories)).catch(() => undefined);
    taskCategoriesApi.list().then(setTaskCats).catch(() => undefined);
  }, [load]);

  const fields = (trigger: string): FieldSpec[] => [
    { name: "name", label: "Task name", req: true, full: true, placeholder: "Engine oil & filter change" },
    { name: "partCategoryId", label: "Part category", type: "select", blank: "None", options: parts.map((p) => ({ value: p.id, label: p.name })) },
    { name: "taskCategoryId", label: "Task category", type: "select", blank: "None", options: taskCats.map((c) => ({ value: c.id, label: c.name })) },
    { name: "trigger", label: "Trigger type", type: "select", req: true, options: TRIGGERS, full: true },
    ...(trigger === "usage" || trigger === "hybrid" ? [{ name: "intervalKm", label: "Distance interval (km)", type: "number" as const, req: true }] : []),
    ...(trigger === "time" || trigger === "hybrid" ? [{ name: "intervalDays", label: "Time interval (days)", type: "number" as const, req: true }] : []),
    ...(trigger === "hours" ? [{ name: "intervalHours", label: "Engine hour interval", type: "number" as const, req: true }] : []),
    { name: "durationHours", label: "Estimated duration (h)", type: "number", req: true, step: "0.25" },
    { name: "estimatedCost", label: "Estimated cost (EGP)", type: "number", req: true, step: "0.01" },
    { name: "role", label: "Required role", placeholder: "Technician", full: true },
  ];
  const [trigger, setTrigger] = useState("usage");
  const initial = (t: PmTask | null): FormValues => ({
    name: t?.name ?? "",
    partCategoryId: t?.partCategoryId ?? "",
    taskCategoryId: t?.taskCategoryId ?? "",
    trigger: t?.trigger ?? "usage",
    intervalKm: t?.intervalKm?.toString() ?? "",
    intervalDays: t?.intervalDays?.toString() ?? "",
    intervalHours: t?.intervalHours?.toString() ?? "",
    durationHours: t?.durationHours?.toString() ?? "1",
    estimatedCost: t?.estimatedCost?.toString() ?? "",
    role: t?.role ?? "Technician",
  });

  return (
    <div className="card" style={{ marginTop: 20 }}>
      <div className="card-h">
        <div>
          <h2>PM task library</h2>
          <p className="t-sub">The jobs the PM engine puts on preventive work orders. The first task in this list with an interval in the unit of the vehicle's leading trigger (km, days or hours) is used.</p>
        </div>
        {isAdmin && (
          <Button
            onClick={() => {
              setTrigger("usage");
              setForm({ task: null });
            }}
          >
            <Plus /> Add task
          </Button>
        )}
      </div>
      {error ? (
        <div className="empty">
          <h3>Unable to load</h3>
          <p>{error}</p>
          <Button variant="secondary" onClick={load}>
            Try again
          </Button>
        </div>
      ) : !tasks ? (
        <div className="sk" style={{ height: 180, margin: 20 }} />
      ) : tasks.length === 0 ? (
        <div className="empty">
          <ListChecks />
          <h3>No PM tasks yet</h3>
          <p>Without tasks the engine raises a generic preventive service with no estimated cost.</p>
        </div>
      ) : (
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
                <th>Role</th>
                {isAdmin && <th />}
              </tr>
            </thead>
            <tbody>
              {tasks.map((t) => (
                <tr key={t.id}>
                  <td className="t-main">{t.name}</td>
                  <td>{t.partCategoryName ? <span className="tag">{t.partCategoryName}</span> : "—"}</td>
                  <td style={{ textTransform: "capitalize" }}>{t.trigger}</td>
                  <td className="mono" style={{ fontSize: 12.5 }}>
                    {intervalText(t)}
                  </td>
                  <td className="num">{nf(t.durationHours)} h</td>
                  <td className="num">EGP {nf(t.estimatedCost)}</td>
                  <td className="t-sub">{t.role}</td>
                  {isAdmin && (
                    <td>
                      <RowMenu
                        items={[
                          {
                            label: "Edit task",
                            icon: Pencil,
                            onSelect: () => {
                              setTrigger(t.trigger);
                              setForm({ task: t });
                            },
                          },
                          { separator: true },
                          { label: "Remove task", icon: Trash2, danger: true, onSelect: () => setRemoving(t) },
                        ]}
                      />
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <RecordFormModal
        open={!!form}
        title={form?.task ? "Edit PM task" : "Add PM task"}
        submitLabel={form?.task ? "Save changes" : "Add task"}
        fields={fields(trigger)}
        initial={initial(form?.task ?? null)}
        onValuesChange={(next, changed) => {
          if (changed === "trigger") setTrigger(next.trigger);
          return next;
        }}
        onClose={() => setForm(null)}
        onSubmit={async (body) => {
          const payload = { ...body, isActive: true };
          if (form?.task) await pmTasksApi.update(form.task.id, payload);
          else await pmTasksApi.create(payload);
          toast(form?.task ? "PM task updated" : "PM task added");
          setForm(null);
          await load();
        }}
      />
      <ConfirmDialog
        open={!!removing}
        title="Remove PM task"
        message={`Remove "${removing?.name ?? ""}" from the library? Existing work orders keep their tasks.`}
        confirmLabel="Remove"
        onClose={() => setRemoving(null)}
        onConfirm={async () => {
          try {
            await pmTasksApi.remove(removing!.id);
            toast("PM task removed");
            await load();
          } catch (e) {
            toast(e instanceof Error ? e.message : "Could not remove the task", "bad");
          } finally {
            setRemoving(null);
          }
        }}
      />
    </div>
  );
}
