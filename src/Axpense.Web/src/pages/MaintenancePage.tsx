import { useCallback, useEffect, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { AlertTriangle, Check, History, Plus, RotateCcw, Trash2, Truck, Wrench } from "lucide-react";
import { PageHeader } from "../components/ui/PageHeader";
import { Button } from "../components/ui/Button";
import { Card } from "../components/ui/Card";
import { PageSpinner } from "../components/ui/Spinner";
import { FilterDropdown } from "../components/ui/FilterDropdown";
import { RowMenu } from "../components/ui/RowMenu";
import { ConfirmDialog } from "../components/ui/ConfirmDialog";
import { useToast } from "../components/ui/Toast";
import { WorkOrderTable } from "../components/workorders/WorkOrderViews";
import { WorkOrderFormModal, type WorkOrderPreset } from "../components/workorders/WorkOrderFormModal";
import { egp, useWorkOrderOptions } from "../components/workorders/workorderUi";
import { ApiError, vehiclesApi, workOrdersApi } from "../lib/api";
import type { FleetIssue, Vehicle, WorkOrderListResponse } from "../lib/types";
import { formatDay } from "../lib/utils";
import { PmEngineModal } from "../components/workorders/PmEngineModal";

export default function MaintenancePage() {
  const toast = useToast();
  const navigate = useNavigate();
  const options = useWorkOrderOptions();
  const [params, setParams] = useSearchParams();
  const tab = params.get("tab") === "issues" ? "issues" : "orders";

  const [status, setStatus] = useState("");
  const [type, setType] = useState("");
  const [vehicleId, setVehicleId] = useState("");
  const [data, setData] = useState<WorkOrderListResponse | null>(null);
  const [issues, setIssues] = useState<FleetIssue[] | null>(null);
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [form, setForm] = useState<{ open: boolean; preset?: WorkOrderPreset }>({ open: false });
  const [pmOpen, setPmOpen] = useState(false);
  const [deletingIssue, setDeletingIssue] = useState<FleetIssue | null>(null);

  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    return Promise.all([workOrdersApi.list({ status, type, vehicleId }), workOrdersApi.issues()])
      .then(([d, i]) => {
        setData(d);
        setIssues(i);
      })
      .catch((err) => setError(err instanceof ApiError && err.status === 403 ? "You don't have permission to view maintenance." : "Work orders could not be loaded."))
      .finally(() => setLoading(false));
  }, [status, type, vehicleId]);

  useEffect(() => {
    load();
  }, [load]);
  useEffect(() => {
    vehiclesApi.list().then(setVehicles).catch(() => setVehicles([]));
  }, []);

  const runPm = () => setPmOpen(true);


  const setIssueStatus = async (i: FleetIssue, s: string) => {
    try {
      await vehiclesApi.issues.setStatus(i.vehicleId, i.id, s);
      toast(s === "Resolved" ? `${i.code} resolved` : `${i.code} reopened`);
      load();
    } catch (err) {
      toast(err instanceof Error ? err.message : "Could not update the issue", "bad");
    }
  };

  const s = data?.stats;
  const openIssues = (issues ?? []).filter((i) => i.status !== "Resolved").length;

  return (
    <div>
      <PageHeader
        eyebrow="Work orders"
        title="Maintenance"
        subtitle="Corrective repairs, preventive schedules and inspection follow-ups in one pipeline."
        action={
          <>
            <Button variant="secondary" onClick={runPm}>
              <History /> Run PM engine
            </Button>
            <Button onClick={() => setForm({ open: true })}>
              <Plus /> New work order
            </Button>
          </>
        }
      />

      {s && (
        <div className="grid g-4" style={{ marginBottom: 16 }}>
          <div className="stat">
            <div className="k">Scheduled</div>
            <div className="v">{s.scheduledNext30Days}</div>
            <div className="m">next 30 days</div>
          </div>
          <div className="stat warn">
            <div className="k">In progress</div>
            <div className="v">{s.inProgress}</div>
            <div className="m">in the bay now</div>
          </div>
          <div className="stat bad">
            <div className="k">Overdue</div>
            <div className="v">{s.overdue}</div>
            <div className="m">{s.overdue ? `escalated to ${s.overdueEscalatedTo}` : "nothing late"}</div>
          </div>
          <div className="stat neutral">
            <div className="k">Committed cost</div>
            <div className="v">{egp(s.committedCost)}</div>
            <div className="m">open orders</div>
          </div>
        </div>
      )}

      <div className="tabs" role="tablist">
        <div role="tab" tabIndex={0} aria-selected={tab === "orders"} className={`tab ${tab === "orders" ? "on" : ""}`} onClick={() => setParams({}, { replace: true })}>
          Work orders<span className="c">{data?.total ?? 0}</span>
        </div>
        <div role="tab" tabIndex={0} aria-selected={tab === "issues"} className={`tab ${tab === "issues" ? "on" : ""}`} onClick={() => setParams({ tab: "issues" }, { replace: true })}>
          Issues<span className="c">{openIssues}</span>
        </div>
      </div>

      {loading && !data ? (
        <PageSpinner />
      ) : error ? (
        <Card>
          <div className="empty">
            <Wrench />
            <h3>{error}</h3>
            <p>
              <Button variant="secondary" size="sm" onClick={load}>
                Try again
              </Button>
            </p>
          </div>
        </Card>
      ) : tab === "orders" ? (
        <Card>
          <div className="filters">
            <FilterDropdown label="Status" value={status} options={options?.filterStatuses ?? []} onChange={setStatus} allLabel="Any status" />
            <FilterDropdown label="Type" value={type} options={options?.types ?? []} onChange={setType} allLabel="Any type" />
            <FilterDropdown
              label="Vehicle"
              value={vehicleId}
              options={vehicles.map((v) => ({ value: v.id, label: `${v.name} · ${v.plateNumber}` }))}
              onChange={setVehicleId}
              allLabel="Any vehicle"
            />
            <span className="count">{data?.items.length ?? 0} orders</span>
          </div>
          <div style={{ opacity: loading ? 0.6 : 1 }}>
            <WorkOrderTable
              rows={data?.items ?? []}
              onChanged={load}
              empty={
                status || type || vehicleId
                  ? { title: "No work orders match these filters", text: "Clear a filter to see the whole pipeline." }
                  : { title: "No work orders yet", text: "Create one, or run the PM engine to raise preventive orders." }
              }
            />
          </div>
        </Card>
      ) : (
        <Card>
          <div className="tbl-wrap">
            <table>
              <thead>
                <tr>
                  <th>ID</th>
                  <th>Vehicle</th>
                  <th>Issue</th>
                  <th>Part</th>
                  <th>Priority</th>
                  <th>Source</th>
                  <th>Reported</th>
                  <th>Work order</th>
                  <th>Status</th>
                  <th style={{ textAlign: "right" }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {issues?.length ? (
                  issues.map((i) => (
                    <tr key={i.id}>
                      <td className="mono" style={{ fontSize: 12 }}>
                        {i.code}
                      </td>
                      <td>
                        <div className="t-main">{i.vehicleName}</div>
                        <div className="t-sub">
                          <span className="plate">{i.plateNumber}</span>
                        </div>
                      </td>
                      <td style={{ minWidth: 200 }}>
                        <div className="t-main">{i.title}</div>
                        {i.note && <div className="t-sub">{i.note}</div>}
                      </td>
                      <td>{i.partName ? <span className="tag">{i.partName}</span> : "—"}</td>
                      <td>
                        <span className={`badge ${i.priority === "High" ? "bad" : i.priority === "Medium" ? "warn" : "plain"}`}>{i.priority}</span>
                      </td>
                      <td className="t-sub">{i.source}</td>
                      <td className="mono" style={{ fontSize: 12 }}>
                        {formatDay(i.reportedDate)}
                      </td>
                      <td className="mono" style={{ fontSize: 12 }}>
                        {i.workOrderCode ?? "—"}
                      </td>
                      <td>
                        <span className={`badge ${i.status === "Open" ? "bad" : i.status === "In progress" ? "warn" : "ok"}`}>{i.status}</span>
                      </td>
                      <td>
                        <RowMenu
                          items={[
                            ...(i.status !== "Resolved" && !i.workOrderId
                              ? [
                                  {
                                    label: "Raise work order",
                                    icon: Wrench,
                                    onSelect: () =>
                                      setForm({
                                        open: true,
                                        preset: {
                                          vehicleId: i.vehicleId, type: "Corrective", priority: i.priority === "High" ? "High" : "Medium",
                                          description: i.title, issueId: i.id, tasks: [{ description: `Fix: ${i.title}` }],
                                        },
                                      }),
                                  },
                                ]
                              : []),
                            i.status === "Resolved"
                              ? { label: "Reopen issue", icon: RotateCcw, onSelect: () => setIssueStatus(i, "Open") }
                              : { label: "Mark resolved", icon: Check, onSelect: () => setIssueStatus(i, "Resolved") },
                            { label: "Open vehicle", icon: Truck, onSelect: () => navigate(`/vehicles/${i.vehicleId}?tab=issues`) },
                            { separator: true },
                            { label: "Delete issue", icon: Trash2, danger: true, onSelect: () => setDeletingIssue(i) },
                          ]}
                        />
                      </td>
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td colSpan={10}>
                      <div className="empty">
                        <AlertTriangle />
                        <h3>No issues reported</h3>
                        <p>Failed inspection checks and driver reports land here.</p>
                      </div>
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </Card>
      )}

      <WorkOrderFormModal
        open={form.open}
        workOrder={null}
        preset={form.preset}
        onClose={() => setForm({ open: false })}
        onSaved={() => {
          setForm({ open: false });
          setParams({}, { replace: true });
          load();
        }}
      />
      <ConfirmDialog
        open={!!deletingIssue}
        title="Delete issue"
        message={`${deletingIssue?.code ?? ""} — ${deletingIssue?.title ?? ""} will be deleted.`}
        onConfirm={async () => {
          if (!deletingIssue) return;
          try {
            await vehiclesApi.issues.remove(deletingIssue.vehicleId, deletingIssue.id);
            toast("Issue deleted");
            setDeletingIssue(null);
            load();
          } catch (err) {
            toast(err instanceof Error ? err.message : "Could not delete the issue", "bad");
          }
        }}
        onClose={() => setDeletingIssue(null)}
      />
      <PmEngineModal open={pmOpen} onClose={() => setPmOpen(false)} onDone={load} />
    </div>
  );
}
