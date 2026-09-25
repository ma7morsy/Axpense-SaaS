import { useCallback, useEffect, useState } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import { AlertTriangle, ClipboardList, FileText, Map as MapIcon, Pencil, Repeat, ShieldAlert, Trash2, UserPlus, Wrench } from "lucide-react";
import { PageHeader } from "../components/ui/PageHeader";
import { Button } from "../components/ui/Button";
import { PageSpinner } from "../components/ui/Spinner";
import { ConfirmDialog } from "../components/ui/ConfirmDialog";
import { useToast } from "../components/ui/Toast";
import { VehicleArt } from "../components/vehicles/VehicleArt";
import { VehicleFormModal } from "../components/vehicles/VehicleFormModal";
import { HandOffModal } from "../components/vehicles/HandOffModal";
import { WorkOrderFormModal, type WorkOrderPreset } from "../components/workorders/WorkOrderFormModal";
import { InspectionRunModal, StartInspectionModal } from "../components/inspections/InspectionModals";
import {
  DriversTab, ExpensesTab, FuelTab, InspectionsTab, IssuesTab, MaintenanceTab, OdometerTab, PartsTab, VEHICLE_TABS, type TabProps, type VehicleTabKey,
} from "../components/vehicles/VehicleTabs";
import {
  PM_TRIGGER_LABELS, PmGauge, alertTone, money, num, renewalTone, useVehicleOptions, useVehiclePhoto, vehicleStatusTone,
} from "../components/vehicles/vehicleUi";
import { ApiError, vehiclesApi } from "../lib/api";
import type { VehicleAlert, VehicleDetail } from "../lib/types";
import { formatDay, initials } from "../lib/utils";

type SchedulePreset = WorkOrderPreset;

/** Vehicle profile: header actions, renewal / maintenance banners, the vehicle card and one tab per record type. */
export default function VehicleProfilePage() {
  const { id = "" } = useParams();
  const [params, setParams] = useSearchParams();
  const navigate = useNavigate();
  const toast = useToast();
  const options = useVehicleOptions();

  const [v, setV] = useState<VehicleDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<number | null>(null);
  const [editOpen, setEditOpen] = useState(false);
  const [handOffMode, setHandOffMode] = useState<"assign" | "handoff" | null>(null);
  const [schedule, setSchedule] = useState<SchedulePreset | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [photoVersion, setPhotoVersion] = useState(0);
  const [tabKey, setTabKey] = useState(0);
  const [inspectOpen, setInspectOpen] = useState(false);
  const [runId, setRunId] = useState<string | null>(null);

  const tab = (params.get("tab") as VehicleTabKey) || "parts";
  const photo = useVehiclePhoto(v?.id, !!v?.hasPhoto, photoVersion);

  const load = useCallback(
    () =>
      vehiclesApi
        .get(id)
        .then((d) => {
          setV(d);
          setError(null);
        })
        .catch((err) => setError(err instanceof ApiError ? err.status : 0))
        .finally(() => setLoading(false)),
    [id]
  );

  useEffect(() => {
    setLoading(true);
    load();
  }, [load]);

  const setStatus = async (status: string) => {
    if (!v) return;
    try {
      setV(await vehiclesApi.setStatus(v.id, status));
      toast(`${v.name} is now ${status.toLowerCase()}`);
    } catch (err) {
      toast(err instanceof Error ? err.message : "Could not change the status", "bad");
    }
  };

  const remove = async () => {
    if (!v) return;
    try {
      await vehiclesApi.remove(v.id);
      toast("Vehicle deleted");
      navigate("/vehicles");
    } catch (err) {
      toast(err instanceof Error ? err.message : "Could not delete the vehicle", "bad");
    }
  };

  const alertAction = (a: VehicleAlert) => {
    switch (a.action) {
      case "schedule":
        return { label: "Schedule it", run: () => setSchedule({ description: "Preventive maintenance service", type: "Preventive", tasks: [{ description: "Preventive maintenance service per PM schedule" }] }) };
      case "raise":
        return { label: "Raise work order", run: () => setSchedule({ description: "Overdue preventive maintenance service", type: "Preventive", priority: "High", tasks: [{ description: "Preventive maintenance service per PM schedule" }] }) };
      case "renew":
        return { label: a.kind === "insurance" ? "Renew" : "Update", run: () => setEditOpen(true) };
      case "replace":
      case "claim":
        return { label: a.action === "claim" ? "Claim warranty" : "Open parts", run: () => setParams({ tab: "parts" }) };
      default:
        return null;
    }
  };

  if (loading) return <PageSpinner />;
  if (!v) {
    return (
      <div className="empty">
        <h3>{error === 404 ? "Vehicle not found" : error === 403 ? "You don't have access to this vehicle" : "The vehicle could not be loaded"}</h3>
        <p>
          <Button variant="secondary" onClick={() => navigate("/vehicles")}>
            Back to vehicles
          </Button>
        </p>
      </div>
    );
  }

  const unit = v.readingUnit;
  const reminder = options?.renewalReminderDays ?? 30;
  const tabProps: TabProps = {
    vehicle: v,
    onChanged: load,
    onSchedule: (preset) => setSchedule(preset ?? {}),
    onHandOff: () => setHandOffMode(v.currentDriver ? "handoff" : "assign"),
    onInspect: () => setInspectOpen(true),
    onResume: (inspectionId) => setRunId(inspectionId),
  };
  const TabBody = {
    parts: PartsTab, issues: IssuesTab, fuel: FuelTab, odo: OdometerTab, maintenance: MaintenanceTab,
    expenses: ExpensesTab, inspections: InspectionsTab, drivers: DriversTab,
  }[tab] ?? PartsTab;

  return (
    <div>
      <PageHeader
        eyebrow={v.plateNumber}
        title={
          <>
            {v.name} <span style={{ color: "var(--muted)", fontWeight: 400 }}>{v.modelYear}</span>{" "}
            <span className={`badge ${vehicleStatusTone(v.status)}`} style={{ marginLeft: 10, verticalAlign: "middle" }}>
              {v.status}
            </span>
          </>
        }
        subtitle={`${v.category} · ${v.ownerType}${v.ownerName ? ` — ${v.ownerName}` : ""}`}
        action={
          <>
            <Button variant="secondary" onClick={() => setHandOffMode("assign")}>
              <UserPlus /> Assign
            </Button>
            {v.currentDriver && (
              <Button variant="secondary" onClick={() => setHandOffMode("handoff")}>
                <Repeat /> Hand off
              </Button>
            )}
            <Button variant="secondary" onClick={() => setInspectOpen(true)}>
              <ClipboardList /> Inspect
            </Button>
            <Button variant="secondary" onClick={() => setSchedule({ vehicleId: v.id })}>
              <Wrench /> Schedule maintenance
            </Button>
            <Button onClick={() => setEditOpen(true)}>
              <Pencil /> Edit
            </Button>
          </>
        }
      />

      {v.alerts
        .filter((a) => a.kind === "pm" || a.kind === "insurance" || a.kind === "registration")
        .map((a) => {
          const act = alertAction(a);
          const Icon = a.kind === "pm" ? AlertTriangle : a.kind === "insurance" ? ShieldAlert : FileText;
          return (
            <div key={a.key} className={`banner ${alertTone(a)}`} role="alert">
              <Icon />
              <div>
                <div className="bt">{a.title}</div>
                <div className="bd">{a.detail}</div>
              </div>
              {act && (
                <Button variant="secondary" size="sm" onClick={act.run}>
                  {act.label}
                </Button>
              )}
            </div>
          );
        })}
      {(() => {
        const partAlerts = v.alerts.filter((a) => a.kind === "part" || a.kind === "warranty");
        if (!partAlerts.length) return null;
        const critical = partAlerts.some((a) => a.severity === "critical");
        return (
          <div className={`banner ${critical ? "bad" : "warn"}`} role="alert">
            <Wrench />
            <div>
              <div className="bt">
                {partAlerts.length} part{partAlerts.length === 1 ? "" : "s"} need{partAlerts.length === 1 ? "s" : ""} attention
              </div>
              <div className="bd">{partAlerts.map((a) => a.title).join(" · ")}</div>
            </div>
            <Button variant="secondary" size="sm" onClick={() => setParams({ tab: "parts" })}>
              Open parts
            </Button>
          </div>
        );
      })()}

      <div className="split">
        <div>
          <div className="card" style={{ marginBottom: 14, overflow: "hidden" }}>
            {v.dispatchBlocked && <div className="hazard" />}
            <div className="hero-art">
              {photo ? <img className="hero-photo" src={photo} alt={v.name} /> : <VehicleArt category={v.category} className="hero-svg" />}
            </div>
            <div className="card-b">
              <div className="between" style={{ marginBottom: 12 }}>
                <span className="plate" style={{ fontSize: 13, padding: "3px 8px" }}>
                  {v.plateNumber}
                </span>
                <select
                  aria-label="Vehicle status"
                  value={v.status}
                  onChange={(e) => setStatus(e.target.value)}
                  style={{ padding: "5px 8px", border: "1px solid var(--line)", borderRadius: "var(--r)", fontSize: 12.5, fontWeight: 600 }}
                >
                  {(options?.statuses ?? [v.status]).map((s) => (
                    <option key={s}>{s}</option>
                  ))}
                </select>
              </div>
              <div className="note" style={{ marginBottom: 14 }}>
                <div className="between">
                  <span>Assigned driver</span>
                  <Button variant="ghost" size="sm" onClick={() => setHandOffMode("assign")}>
                    <Repeat /> Reassign
                  </Button>
                </div>
                <div className="flex" style={{ marginTop: 7 }}>
                  <span className={`avatar-sm ${v.currentDriver ? "" : "teal"}`}>{v.currentDriver ? initials(v.currentDriver.fullName) : "—"}</span>
                  <div style={{ flex: 1 }}>
                    {v.currentDriver ? (
                      <div className="link" style={{ fontWeight: 600 }} onClick={() => navigate(`/drivers/${v.currentDriver!.id}`)}>
                        {v.currentDriver.fullName}
                      </div>
                    ) : (
                      <div style={{ fontWeight: 600, color: "var(--ink)" }}>Nobody assigned</div>
                    )}
                    <div className="t-sub">{v.currentDriver ? v.currentDriver.licenseClass : "Assign a driver before dispatch"}</div>
                  </div>
                </div>
              </div>
              <div className="fs-t">Maintenance runway</div>
              <PmGauge odometer={v.currentOdometer} unit={unit} pm={v.pm} wide />
              <div className="divider" />
              <dl className="kv">
                <dt>VIN</dt>
                <dd className="mono" style={{ fontSize: 12 }}>
                  {v.vin ?? "—"}
                </dd>
                <dt>Odometer</dt>
                <dd className="mono">
                  {num(v.currentOdometer)} {unit}
                </dd>
                <dt>Engine</dt>
                <dd>{v.engineType}</dd>
                <dt>Fuel</dt>
                <dd>{v.fuelType}</dd>
                <dt>Tank capacity</dt>
                <dd className="mono">{v.tankCapacity != null ? `${num(v.tankCapacity, 1)} ${v.tankUnit}` : "—"}</dd>
                <dt>PM trigger</dt>
                <dd>{PM_TRIGGER_LABELS[v.pmTrigger] ?? v.pmTrigger}</dd>
              </dl>
              <div className="divider" />
              <dl className="kv">
                <dt>Purchased</dt>
                <dd>{formatDay(v.purchaseDate)}</dd>
                <dt>Cost</dt>
                <dd className="mono">{money(v.purchasePrice, 0)}</dd>
                <dt>Supplier</dt>
                <dd>{v.supplier ?? "—"}</dd>
                <dt>Warranty</dt>
                <dd>{v.warrantyUntil == null ? "—" : (v.warrantyDaysLeft ?? 0) < 0 ? <span className="badge plain">Expired</span> : formatDay(v.warrantyUntil)}</dd>
              </dl>
              <div className="divider" />
              <dl className="kv">
                <dt>Insurance</dt>
                <dd>{v.insuranceProvider ?? "—"}</dd>
                <dt>Policy</dt>
                <dd className="mono" style={{ fontSize: 12 }}>
                  {v.insurancePolicyNumber ?? "—"}
                </dd>
                <dt>Renews</dt>
                <dd>{v.insuranceRenewalDate ? <span className={`badge ${renewalTone(v.insuranceDaysLeft, reminder)}`}>{formatDay(v.insuranceRenewalDate)}</span> : "—"}</dd>
                <dt>Registration</dt>
                <dd className="mono" style={{ fontSize: 12 }}>
                  {v.registrationNumber ?? "—"}
                </dd>
                <dt>Renews</dt>
                <dd>
                  {v.registrationRenewalDate ? <span className={`badge ${renewalTone(v.registrationDaysLeft, reminder)}`}>{formatDay(v.registrationRenewalDate)}</span> : "—"}
                </dd>
                <dt>Lifetime spend</dt>
                <dd className="mono">{money(v.lifetimeSpend, 0)}</dd>
              </dl>
              <div className="divider" />
              <div className="between">
                <Button variant="ghost" size="sm" onClick={() => navigate("/aerial")}>
                  <MapIcon /> Show on map
                </Button>
                <Button variant="ghost" size="sm" onClick={() => setDeleting(true)} style={{ color: "var(--red)" }}>
                  <Trash2 /> Delete
                </Button>
              </div>
            </div>
          </div>
        </div>
        <div>
          <div className="tabs" role="tablist">
            {VEHICLE_TABS.map((t) => (
              <div
                key={t.key}
                role="tab"
                aria-selected={tab === t.key}
                tabIndex={0}
                className={`tab ${tab === t.key ? "on" : ""}`}
                onClick={() => setParams({ tab: t.key }, { replace: true })}
                onKeyDown={(e) => e.key === "Enter" && setParams({ tab: t.key }, { replace: true })}
              >
                {t.label}
                <span className="c">{v.counts[t.count]}</span>
              </div>
            ))}
          </div>
          <TabBody key={`${tab}-${tabKey}`} {...tabProps} />
        </div>
      </div>

      <VehicleFormModal
        open={editOpen}
        vehicle={v}
        onClose={() => setEditOpen(false)}
        onSaved={(saved) => {
          setEditOpen(false);
          setV(saved);
          setPhotoVersion((n) => n + 1);
          setTabKey((n) => n + 1);
        }}
      />
      <HandOffModal
        vehicle={handOffMode ? { id: v.id, name: v.name, plateNumber: v.plateNumber, currentDriver: v.currentDriver ?? null, mode: handOffMode } : null}
        onClose={() => setHandOffMode(null)}
        onSaved={(inspectionId) => {
          setHandOffMode(null);
          if (inspectionId) setRunId(inspectionId);
          load();
          setTabKey((n) => n + 1);
        }}
      />
      <WorkOrderFormModal
        open={!!schedule}
        workOrder={null}
        preset={schedule ? { ...schedule, vehicleId: v.id } : undefined}
        onClose={() => setSchedule(null)}
        onSaved={() => {
          setSchedule(null);
          load();
          setTabKey((n) => n + 1);
        }}
      />
      <StartInspectionModal
        open={inspectOpen}
        vehicleId={v.id}
        onClose={() => setInspectOpen(false)}
        onStarted={(inspectionId) => {
          setInspectOpen(false);
          setRunId(inspectionId);
          load();
          setTabKey((n) => n + 1);
        }}
      />
      <InspectionRunModal
        inspectionId={runId}
        onClose={() => {
          setRunId(null);
          setTabKey((n) => n + 1);
        }}
        onDone={() => {
          setRunId(null);
          load();
          setParams({ tab: "inspections" }, { replace: true });
          setTabKey((n) => n + 1);
        }}
      />
      <ConfirmDialog
        open={deleting}
        title="Delete vehicle"
        message={`${v.name} (${v.plateNumber}) and its parts, readings, fuel, maintenance, inspections and driver history will be removed. Its expenses stay in the books as fleet-wide. This cannot be undone.`}
        onConfirm={remove}
        onClose={() => setDeleting(false)}
      />
    </div>
  );
}
