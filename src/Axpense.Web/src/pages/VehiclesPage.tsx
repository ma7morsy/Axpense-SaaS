import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { ClipboardList, Eye, Pencil, Plus, Repeat, Trash2, Truck, UserPlus, Wrench } from "lucide-react";
import { PageHeader } from "../components/ui/PageHeader";
import { Button } from "../components/ui/Button";
import { Card } from "../components/ui/Card";
import { PageSpinner } from "../components/ui/Spinner";
import { FilterDropdown } from "../components/ui/FilterDropdown";
import { RowMenu } from "../components/ui/RowMenu";
import { ConfirmDialog } from "../components/ui/ConfirmDialog";
import { useToast } from "../components/ui/Toast";
import { VehicleFormModal } from "../components/vehicles/VehicleFormModal";
import { HandOffModal } from "../components/vehicles/HandOffModal";
import { WorkOrderFormModal } from "../components/workorders/WorkOrderFormModal";
import { InspectionRunModal, StartInspectionModal } from "../components/inspections/InspectionModals";
import { PM_FILTERS, PmGauge, VehicleThumb, useVehicleOptions, vehicleStatusTone } from "../components/vehicles/vehicleUi";
import { ApiError, vehiclesApi } from "../lib/api";
import type { Vehicle, VehicleDetail, VehicleListResponse } from "../lib/types";
import { initials } from "../lib/utils";

export default function VehiclesPage() {
  const navigate = useNavigate();
  const toast = useToast();
  const options = useVehicleOptions();

  const [q, setQ] = useState("");
  const [debouncedQ, setDebouncedQ] = useState("");
  const [status, setStatus] = useState("");
  const [category, setCategory] = useState("");
  const [owner, setOwner] = useState("");
  const [pm, setPm] = useState("");
  const [data, setData] = useState<VehicleListResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<VehicleDetail | null>(null);
  const [handOff, setHandOff] = useState<{ v: Vehicle; mode: "assign" | "handoff" } | null>(null);
  const [scheduleFor, setScheduleFor] = useState<Vehicle | null>(null);
  const [deleting, setDeleting] = useState<Vehicle | null>(null);
  const [inspect, setInspect] = useState<{ open: boolean; vehicleId?: string }>({ open: false });
  const [runId, setRunId] = useState<string | null>(null);

  useEffect(() => {
    const t = window.setTimeout(() => setDebouncedQ(q.trim()), 250);
    return () => window.clearTimeout(t);
  }, [q]);

  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    return vehiclesApi
      .search({ q: debouncedQ, status, category, owner, pm })
      .then(setData)
      .catch((err) =>
        setError(err instanceof ApiError && err.status === 403 ? "You don't have permission to view vehicles." : "Vehicles could not be loaded.")
      )
      .finally(() => setLoading(false));
  }, [debouncedQ, status, category, owner, pm]);

  useEffect(() => {
    load();
  }, [load]);

  const openEdit = async (id: string) => {
    try {
      setEditing(await vehiclesApi.get(id));
      setFormOpen(true);
    } catch {
      toast("Could not open the vehicle", "bad");
    }
  };

  const confirmDelete = async () => {
    if (!deleting) return;
    try {
      await vehiclesApi.remove(deleting.id);
      toast("Vehicle deleted");
      setDeleting(null);
      load();
    } catch (err) {
      toast(err instanceof Error ? err.message : "Could not delete the vehicle", "bad");
    }
  };

  const rows = data?.items ?? [];
  const filtered = !!(debouncedQ || status || category || owner || pm);

  return (
    <div>
      <PageHeader
        eyebrow="Fleet register"
        title="Vehicles"
        subtitle="Every asset, its service runway and who is behind the wheel."
        action={
          <>
            <Button variant="secondary" onClick={() => setInspect({ open: true })}>
              <ClipboardList /> Run inspection
            </Button>
            <Button
              onClick={() => {
                setEditing(null);
                setFormOpen(true);
              }}
            >
              <Plus /> Add vehicle
            </Button>
          </>
        }
      />

      <Card>
        <div className="filters">
          <input
            placeholder="Filter by plate, VIN, model, driver"
            value={q}
            onChange={(e) => setQ(e.target.value)}
            style={{ width: 250 }}
            aria-label="Filter vehicles"
          />
          <FilterDropdown label="Status" value={status} options={options?.statuses ?? []} onChange={setStatus} allLabel="Any status" />
          <FilterDropdown label="Category" value={category} options={options?.categories ?? []} onChange={setCategory} allLabel="Any category" />
          <FilterDropdown label="Owner" value={owner} options={options?.ownerTypes ?? []} onChange={setOwner} allLabel="Any owner" />
          <FilterDropdown label="PM state" value={pm} options={PM_FILTERS} onChange={setPm} allLabel="Any PM state" />
          {data && (
            <span className="count">
              {rows.length} of {data.total}
            </span>
          )}
        </div>

        {loading && !data ? (
          <PageSpinner />
        ) : error ? (
          <div className="empty">
            <Truck />
            <h3>{error}</h3>
            <p>
              <Button variant="secondary" size="sm" onClick={load}>
                Try again
              </Button>
            </p>
          </div>
        ) : (
          <div className="tbl-wrap">
            <table>
              <thead>
                <tr>
                  <th>Vehicle</th>
                  <th>VIN / plate</th>
                  <th>Status</th>
                  <th style={{ width: 250 }}>Odometer · maintenance</th>
                  <th>Assigned driver</th>
                  <th style={{ textAlign: "right" }}>Actions</th>
                </tr>
              </thead>
              <tbody style={{ opacity: loading ? 0.6 : 1 }}>
                {rows.length ? (
                  rows.map((v) => (
                    <tr key={v.id} className="clickable" onClick={() => navigate(`/vehicles/${v.id}`)}>
                      <td>
                        <div className="flex">
                          <VehicleThumb id={v.id} category={v.category} hasPhoto={v.hasPhoto} />
                          <div>
                            <div className="t-main">{v.name}</div>
                            <div className="t-sub">
                              {v.modelYear} · {v.category} · {v.ownerType}
                            </div>
                          </div>
                        </div>
                      </td>
                      <td>
                        <div className="mono" style={{ fontSize: 11.5 }}>
                          {v.vin ?? "—"}
                        </div>
                        <div style={{ marginTop: 4 }}>
                          <span className="plate">{v.plateNumber}</span>
                        </div>
                      </td>
                      <td>
                        <span className={`badge ${vehicleStatusTone(v.status)}`}>{v.status}</span>
                        {v.dispatchBlocked && (
                          <div className="badge bad" style={{ marginTop: 5 }}>
                            Dispatch blocked
                          </div>
                        )}
                        {v.alertCount > 0 && !v.dispatchBlocked && (
                          <div className="t-sub" style={{ marginTop: 5, color: "#B45309", fontWeight: 600 }}>
                            {v.alertCount} alert{v.alertCount === 1 ? "" : "s"}
                          </div>
                        )}
                      </td>
                      <td>
                        <PmGauge odometer={v.currentOdometer} unit={v.readingUnit} pm={v.pm} />
                      </td>
                      <td>
                        {v.currentDriver ? (
                          <div className="flex">
                            <span className="avatar-sm">{initials(v.currentDriver.fullName)}</span>
                            <div>
                              <div style={{ fontSize: 13, fontWeight: 500 }}>{v.currentDriver.fullName}</div>
                              <div className="t-sub mono">{v.currentDriver.licenseNumber || "—"}</div>
                            </div>
                          </div>
                        ) : (
                          <span className="tag">Unassigned</span>
                        )}
                      </td>
                      <td>
                        <RowMenu
                          items={[
                            { label: "Open profile", icon: Eye, onSelect: () => navigate(`/vehicles/${v.id}`) },
                            { label: v.currentDriver ? "Assign another driver" : "Assign driver", icon: UserPlus, onSelect: () => setHandOff({ v, mode: "assign" }) },
                            ...(v.currentDriver ? [{ label: "Hand off", icon: Repeat, onSelect: () => setHandOff({ v, mode: "handoff" }) }] : []),
                            { label: "Schedule maintenance", icon: Wrench, onSelect: () => setScheduleFor(v) },
                            { label: "Run inspection", icon: ClipboardList, onSelect: () => setInspect({ open: true, vehicleId: v.id }) },
                            { separator: true },
                            { label: "Edit vehicle", icon: Pencil, onSelect: () => openEdit(v.id) },
                            { label: "Delete vehicle", icon: Trash2, danger: true, onSelect: () => setDeleting(v) },
                          ]}
                        />
                      </td>
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td colSpan={6}>
                      <div className="empty">
                        <Truck />
                        <h3>{filtered ? "No vehicles match these filters" : "No vehicles yet"}</h3>
                        <p>{filtered ? "Clear a filter, or add the vehicle you were looking for." : "Add your first vehicle to start tracking its runway and records."}</p>
                      </div>
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <VehicleFormModal
        open={formOpen}
        vehicle={editing}
        onClose={() => setFormOpen(false)}
        onSaved={(saved) => {
          setFormOpen(false);
          if (editing) load();
          else navigate(`/vehicles/${saved.id}`);
        }}
      />

      <HandOffModal
        vehicle={
          handOff
            ? { id: handOff.v.id, name: handOff.v.name, plateNumber: handOff.v.plateNumber, currentDriver: handOff.v.currentDriver ?? null, mode: handOff.mode }
            : null
        }
        onClose={() => setHandOff(null)}
        onSaved={(inspectionId) => {
          setHandOff(null);
          if (inspectionId) setRunId(inspectionId);
          load();
        }}
      />

      <WorkOrderFormModal
        open={!!scheduleFor}
        workOrder={null}
        preset={scheduleFor ? { vehicleId: scheduleFor.id } : undefined}
        onClose={() => setScheduleFor(null)}
        onSaved={() => {
          setScheduleFor(null);
          load();
        }}
      />

      <StartInspectionModal
        open={inspect.open}
        vehicleId={inspect.vehicleId}
        onClose={() => setInspect({ open: false })}
        onStarted={(id) => {
          setInspect({ open: false });
          setRunId(id);
        }}
      />
      <InspectionRunModal
        inspectionId={runId}
        onClose={() => setRunId(null)}
        onDone={() => {
          setRunId(null);
          load();
        }}
      />

      <ConfirmDialog
        open={!!deleting}
        title="Delete vehicle"
        message={`${deleting?.name ?? "This vehicle"} (${deleting?.plateNumber ?? ""}) and its parts, readings, fuel, maintenance, inspections and driver history will be removed. Its expenses stay in the books as fleet-wide. This cannot be undone.`}
        onConfirm={confirmDelete}
        onClose={() => setDeleting(null)}
      />
    </div>
  );
}
