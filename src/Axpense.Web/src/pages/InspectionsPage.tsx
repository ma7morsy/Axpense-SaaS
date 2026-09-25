import { useCallback, useEffect, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { ClipboardList, Eye, Pencil, Play, Plus, Trash2, Truck } from "lucide-react";
import { PageHeader } from "../components/ui/PageHeader";
import { Button } from "../components/ui/Button";
import { Card } from "../components/ui/Card";
import { PageSpinner } from "../components/ui/Spinner";
import { FilterDropdown } from "../components/ui/FilterDropdown";
import { RowMenu } from "../components/ui/RowMenu";
import { ConfirmDialog } from "../components/ui/ConfirmDialog";
import { useToast } from "../components/ui/Toast";
import { TemplateFormModal } from "../components/inspections/TemplateFormModal";
import { InspectionRunModal, InspectionViewModal, StartInspectionModal } from "../components/inspections/InspectionModals";
import "../components/inspections/inspections.css";
import { ApiError, inspectionTemplatesApi, inspectionsApi, vehiclesApi } from "../lib/api";
import type { InspectionListResponse, InspectionRow, InspectionTemplate, Vehicle } from "../lib/types";
import { formatDay } from "../lib/utils";

const num = (v: number | null | undefined) => (v == null ? "—" : Number(v).toLocaleString("en-US", { maximumFractionDigits: 1 }));

export default function InspectionsPage() {
  const navigate = useNavigate();
  const toast = useToast();
  const [params, setParams] = useSearchParams();
  const tab = params.get("tab") === "templates" ? "templates" : "log";

  const [vehicleF, setVehicleF] = useState("");
  const [templateF, setTemplateF] = useState("");
  const [data, setData] = useState<InspectionListResponse | null>(null);
  const [templates, setTemplates] = useState<InspectionTemplate[] | null>(null);
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const [tplForm, setTplForm] = useState<{ open: boolean; template: InspectionTemplate | null }>({ open: false, template: null });
  const [start, setStart] = useState<{ open: boolean; templateId?: string }>({ open: false });
  const [runId, setRunId] = useState<string | null>(null);
  const [viewId, setViewId] = useState<string | null>(null);
  const [deleting, setDeleting] = useState<InspectionRow | null>(null);
  const [deletingTpl, setDeletingTpl] = useState<InspectionTemplate | null>(null);

  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    return Promise.all([inspectionsApi.list({ vehicleId: vehicleF, templateId: templateF }), inspectionTemplatesApi.list()])
      .then(([d, t]) => {
        setData(d);
        setTemplates(t);
      })
      .catch((err) => setError(err instanceof ApiError && err.status === 403 ? "You don't have permission to view inspections." : "Inspections could not be loaded."))
      .finally(() => setLoading(false));
  }, [vehicleF, templateF]);

  useEffect(() => {
    load();
  }, [load]);
  useEffect(() => {
    vehiclesApi.list().then(setVehicles).catch(() => setVehicles([]));
  }, []);

  const removeInspection = async () => {
    if (!deleting) return;
    try {
      await inspectionsApi.remove(deleting.id);
      toast(deleting.status === "Completed" ? "Inspection deleted" : "Draft discarded");
      setDeleting(null);
      load();
    } catch (err) {
      toast(err instanceof Error ? err.message : "Could not delete the inspection", "bad");
    }
  };

  const removeTemplate = async () => {
    if (!deletingTpl) return;
    try {
      await inspectionTemplatesApi.remove(deletingTpl.id);
      toast("Template deleted");
      setDeletingTpl(null);
      load();
    } catch (err) {
      toast(err instanceof Error ? err.message : "Could not delete the template", "bad");
    }
  };

  const s = data?.stats;
  const rows = data?.items ?? [];

  return (
    <div>
      <PageHeader
        eyebrow="Checklists"
        title="Inspections"
        subtitle="Run a template against a vehicle. Failed items become issues on the spot."
        action={
          <>
            <Button variant="secondary" onClick={() => setTplForm({ open: true, template: null })}>
              <Plus /> New template
            </Button>
            <Button onClick={() => setStart({ open: true })}>
              <Play /> Run inspection
            </Button>
          </>
        }
      />

      {s && (
        <div className="grid g-4" style={{ marginBottom: 16 }}>
          <div className="stat">
            <div className="k">Completed</div>
            <div className="v">{s.completed}</div>
            <div className="m">{s.inProgress ? `${s.inProgress} in progress` : "all time"}</div>
          </div>
          <div className="stat bad">
            <div className="k">Failed items</div>
            <div className="v">{s.failedItems}</div>
            <div className="m">across completed inspections</div>
          </div>
          <div className="stat neutral">
            <div className="k">Templates</div>
            <div className="v">{s.templates}</div>
            <div className="m">{s.activeTemplates} active</div>
          </div>
          <div className="stat warn">
            <div className="k">Vehicles never inspected</div>
            <div className="v">{s.vehiclesNeverInspected}</div>
            <div className="m">of {s.fleetSize} in the fleet</div>
          </div>
        </div>
      )}

      <div className="tabs" role="tablist">
        <div role="tab" tabIndex={0} aria-selected={tab === "log"} className={`tab ${tab === "log" ? "on" : ""}`} onClick={() => setParams({}, { replace: true })}>
          Inspection log<span className="c">{rows.length}</span>
        </div>
        <div
          role="tab"
          tabIndex={0}
          aria-selected={tab === "templates"}
          className={`tab ${tab === "templates" ? "on" : ""}`}
          onClick={() => setParams({ tab: "templates" }, { replace: true })}
        >
          Templates<span className="c">{templates?.length ?? 0}</span>
        </div>
      </div>

      {loading && !data ? (
        <PageSpinner />
      ) : error ? (
        <Card>
          <div className="empty">
            <ClipboardList />
            <h3>{error}</h3>
            <p>
              <Button variant="secondary" size="sm" onClick={load}>
                Try again
              </Button>
            </p>
          </div>
        </Card>
      ) : tab === "log" ? (
        <Card>
          <div className="filters">
            <FilterDropdown
              label="Vehicle"
              value={vehicleF}
              options={vehicles.map((v) => ({ value: v.id, label: `${v.name} · ${v.plateNumber}` }))}
              onChange={setVehicleF}
              allLabel="Any vehicle"
            />
            <FilterDropdown label="Template" value={templateF} options={(templates ?? []).map((t) => ({ value: t.id, label: t.name }))} onChange={setTemplateF} allLabel="Any template" />
            <span className="count">{rows.length} inspections</span>
          </div>
          <div className="tbl-wrap">
            <table>
              <thead>
                <tr>
                  <th>ID</th>
                  <th>Vehicle</th>
                  <th>Template</th>
                  <th>Date</th>
                  <th className="num">Odometer</th>
                  <th>Inspector</th>
                  <th>Result</th>
                  <th style={{ textAlign: "right" }}>Actions</th>
                </tr>
              </thead>
              <tbody style={{ opacity: loading ? 0.6 : 1 }}>
                {rows.length ? (
                  rows.map((r) => {
                    const draft = r.status !== "Completed";
                    const open = () => (draft ? setRunId(r.id) : setViewId(r.id));
                    return (
                      <tr key={r.id} className="clickable" onClick={open}>
                        <td className="mono" style={{ fontSize: 12 }}>
                          {r.code}
                        </td>
                        <td>
                          <div className="t-main">{r.vehicleName}</div>
                          <div className="t-sub">
                            <span className="plate">{r.plateNumber}</span>
                          </div>
                        </td>
                        <td>{r.templateName}</td>
                        <td className="mono" style={{ fontSize: 12 }}>
                          {formatDay(r.date)}
                        </td>
                        <td className="num">{num(r.odometer)}</td>
                        <td>{r.inspectorName ?? "—"}</td>
                        <td>
                          {draft ? (
                            <span className="badge warn">
                              In progress · {r.summary.answered}/{r.summary.total}
                            </span>
                          ) : (
                            <div className="flex">
                              <div className={`bar-mini ${r.summary.failed ? "bad" : ""}`} style={{ width: 60, flex: "none" }}>
                                <i style={{ width: `${r.summary.total ? (r.summary.passed / r.summary.total) * 100 : 0}%` }} />
                              </div>
                              <span className="mono" style={{ fontSize: 12 }}>
                                {r.summary.passed}/{r.summary.total}
                              </span>
                              {r.summary.failed ? <span className="badge bad">{r.summary.failed} failed</span> : <span className="badge ok">Clear</span>}
                            </div>
                          )}
                        </td>
                        <td>
                          <RowMenu
                            items={[
                              draft
                                ? { label: "Resume inspection", icon: Play, onSelect: () => setRunId(r.id) }
                                : { label: "Open inspection", icon: Eye, onSelect: () => setViewId(r.id) },
                              { label: "Open vehicle", icon: Truck, onSelect: () => navigate(`/vehicles/${r.vehicleId}?tab=inspections`) },
                              { separator: true },
                              { label: draft ? "Discard draft" : "Delete inspection", icon: Trash2, danger: true, onSelect: () => setDeleting(r) },
                            ]}
                          />
                        </td>
                      </tr>
                    );
                  })
                ) : (
                  <tr>
                    <td colSpan={8}>
                      <div className="empty">
                        <ClipboardList />
                        <h3>{vehicleF || templateF ? "No inspections match these filters" : "No inspections yet"}</h3>
                        <p>{vehicleF || templateF ? "Clear a filter to see the whole log." : "Run the first one against a template."}</p>
                      </div>
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </Card>
      ) : (
        <div className="grid g-2" style={{ alignItems: "start" }}>
          {(templates ?? []).map((t) => (
            <div key={t.id} className="card tpl-card">
              <div className="card-h">
                <div>
                  <h2>{t.name}</h2>
                  <div className="t-sub">
                    {t.code} · {t.checkCount} checks · scope: {t.scope} · run {t.timesRun} time{t.timesRun === 1 ? "" : "s"}
                  </div>
                </div>
                <span className="chip-row" style={{ marginLeft: "auto" }}>
                  {t.isSystem && (
                    <span className="badge info" title="Used by the vehicle hand-off; editable, can't be deleted">
                      Built-in
                    </span>
                  )}
                  <span className={`badge ${t.active ? "ok" : "plain"}`}>{t.active ? "Active" : "Draft"}</span>
                </span>
              </div>
              <div className="card-b" style={{ padding: 0 }}>
                <table>
                  <tbody>
                    {t.items.map((it) => (
                      <tr key={it.id}>
                        <td style={{ padding: "9px 14px" }}>
                          <div className="between">
                            <div>
                              <div style={{ fontWeight: 500 }}>{it.label}</div>
                              <div className="t-sub">
                                {it.partName} · {it.fieldTypeLabel}
                                {it.fieldType === "gauge" ? ` · ${it.unit ?? ""} ${num(it.min)}–${num(it.max)}` : ""}
                              </div>
                            </div>
                            <div className="chip-row">
                              {it.critical ? <span className="badge bad">Critical</span> : <span className="badge plain">Standard</span>}
                              {it.issueOnFail && (
                                <span className="badge info" title="Creates an issue on failure">
                                  Auto issue
                                </span>
                              )}
                            </div>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="modal-f" style={{ borderRadius: "0 0 var(--r-lg) var(--r-lg)" }}>
                {!t.isSystem && (
                  <Button variant="danger" size="sm" onClick={() => setDeletingTpl(t)}>
                    <Trash2 /> Delete
                  </Button>
                )}
                <Button variant="secondary" size="sm" onClick={() => setTplForm({ open: true, template: t })}>
                  <Pencil /> Edit template
                </Button>
                <button className="btn sm teal" type="button" disabled={!t.active} title={t.active ? undefined : "Activate the template to run it"} onClick={() => setStart({ open: true, templateId: t.id })}>
                  <Play /> Run this
                </button>
              </div>
            </div>
          ))}
          <div className="card tpl-new" role="button" tabIndex={0} onClick={() => setTplForm({ open: true, template: null })}>
            <div className="empty">
              <Plus />
              <h3>New template</h3>
              <p>Build a checklist from your parts catalogue.</p>
            </div>
          </div>
        </div>
      )}

      <TemplateFormModal
        open={tplForm.open}
        template={tplForm.template}
        onClose={() => setTplForm({ open: false, template: null })}
        onSaved={() => {
          setTplForm({ open: false, template: null });
          setParams({ tab: "templates" }, { replace: true });
          load();
        }}
      />
      <StartInspectionModal
        open={start.open}
        templateId={start.templateId}
        onClose={() => setStart({ open: false })}
        onStarted={(id) => {
          setStart({ open: false });
          setRunId(id);
          load();
        }}
      />
      <InspectionRunModal
        inspectionId={runId}
        onClose={() => {
          setRunId(null);
          load();
        }}
        onDone={() => {
          setRunId(null);
          load();
        }}
      />
      <InspectionViewModal inspectionId={viewId} onClose={() => setViewId(null)} />
      <ConfirmDialog
        open={!!deleting}
        title={deleting?.status === "Completed" ? "Delete inspection" : "Discard draft"}
        message={
          deleting?.status === "Completed"
            ? `${deleting?.code} will be removed from the log. Issues it raised stay open on the vehicle.`
            : `${deleting?.code ?? "This draft"} and its answers and photos will be deleted.`
        }
        onConfirm={removeInspection}
        onClose={() => setDeleting(null)}
      />
      <ConfirmDialog
        open={!!deletingTpl}
        title="Delete template"
        message={`${deletingTpl?.name ?? "This template"} will be deleted. Past inspections that used it stay in the log with their own copy of the checklist.`}
        onConfirm={removeTemplate}
        onClose={() => setDeletingTpl(null)}
      />
    </div>
  );
}
