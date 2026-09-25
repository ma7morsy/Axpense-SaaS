import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Eye, FileText, Pencil, Plus, Trash2, Users as UsersIcon } from "lucide-react";
import { PageHeader } from "../components/ui/PageHeader";
import { Button } from "../components/ui/Button";
import { Card } from "../components/ui/Card";
import { PageSpinner } from "../components/ui/Spinner";
import { FilterDropdown } from "../components/ui/FilterDropdown";
import { RowMenu } from "../components/ui/RowMenu";
import { ConfirmDialog } from "../components/ui/ConfirmDialog";
import { useToast } from "../components/ui/Toast";
import { DriverFormModal } from "../components/drivers/DriverFormModal";
import { DocumentFormModal } from "../components/drivers/DocumentFormModal";
import { expiryTone, ratingBarTone, statusTone } from "../components/drivers/driverUi";
import { ApiError, driversApi } from "../lib/api";
import { DRIVER_STATUSES, type DriverListItem, type DriverListResponse, type DriverProfile } from "../lib/types";
import { formatDay, initials } from "../lib/utils";

export default function DriversPage() {
  const navigate = useNavigate();
  const toast = useToast();

  const [q, setQ] = useState("");
  const [debouncedQ, setDebouncedQ] = useState("");
  const [status, setStatus] = useState("");
  const [data, setData] = useState<DriverListResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<DriverProfile | null>(null);
  const [docFor, setDocFor] = useState<DriverListItem | null>(null);
  const [deleting, setDeleting] = useState<DriverListItem | null>(null);

  useEffect(() => {
    const t = window.setTimeout(() => setDebouncedQ(q.trim()), 250);
    return () => window.clearTimeout(t);
  }, [q]);

  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    return driversApi
      .list({ q: debouncedQ, status, pageSize: 200 })
      .then(setData)
      .catch((err) =>
        setError(err instanceof ApiError && err.status === 403 ? "You don't have permission to view drivers." : "Drivers could not be loaded.")
      )
      .finally(() => setLoading(false));
  }, [debouncedQ, status]);

  useEffect(() => {
    load();
  }, [load]);

  const openEdit = async (id: string) => {
    try {
      setEditing(await driversApi.get(id));
      setFormOpen(true);
    } catch {
      toast("Could not open the driver", "bad");
    }
  };

  const confirmDelete = async () => {
    if (!deleting) return;
    try {
      await driversApi.remove(deleting.id);
      toast("Driver deleted");
      setDeleting(null);
      load();
    } catch (err) {
      toast(err instanceof Error ? err.message : "Could not delete the driver", "bad");
    }
  };

  const rows = data?.items ?? [];

  return (
    <div>
      <PageHeader
        eyebrow="People"
        title="Drivers"
        subtitle="Licences, documents and performance for everyone who moves the fleet."
        action={
          <Button
            onClick={() => {
              setEditing(null);
              setFormOpen(true);
            }}
          >
            <Plus /> Add driver
          </Button>
        }
      />

      <Card>
        <div className="filters">
          <input
            placeholder="Filter by name, licence, employee no."
            value={q}
            onChange={(e) => setQ(e.target.value)}
            style={{ width: 250 }}
            aria-label="Filter drivers"
          />
          <FilterDropdown label="Status" value={status} options={DRIVER_STATUSES} onChange={setStatus} allLabel="Any status" />
          {data && (
            <span className="count">
              {data.filteredCount} of {data.totalCount}
            </span>
          )}
        </div>

        {loading && !data ? (
          <PageSpinner />
        ) : error ? (
          <div className="empty">
            <UsersIcon />
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
                  <th>Driver</th>
                  <th>Licence</th>
                  <th>Expires</th>
                  <th>Assigned vehicle</th>
                  <th>Documents</th>
                  <th>Rating</th>
                  <th>Status</th>
                  <th style={{ textAlign: "right" }}>Actions</th>
                </tr>
              </thead>
              <tbody style={{ opacity: loading ? 0.6 : 1 }}>
                {rows.length ? (
                  rows.map((dr) => {
                    const n = dr.licenseDaysLeft;
                    return (
                      <tr key={dr.id} className="clickable" onClick={() => navigate(`/drivers/${dr.id}`)}>
                        <td>
                          <div className="flex">
                            <span className="avatar-sm">{initials(dr.fullName)}</span>
                            <div>
                              <div className="t-main">{dr.fullName}</div>
                              <div className="t-sub mono">{dr.employeeNumber}</div>
                            </div>
                          </div>
                        </td>
                        <td>
                          <div style={{ fontSize: 13 }}>{dr.licenseClass}</div>
                          <div className="t-sub mono">{dr.licenseNumber ?? "—"}</div>
                        </td>
                        <td>
                          {dr.licenseExpiryDate ? (
                            <span className={`badge ${expiryTone(n)}`}>{n != null && n < 0 ? `Expired ${Math.abs(n)}d` : formatDay(dr.licenseExpiryDate)}</span>
                          ) : (
                            "—"
                          )}
                        </td>
                        <td>
                          {dr.assignedVehicle ? (
                            <>
                              <span
                                className="link"
                                onClick={(e) => {
                                  e.stopPropagation();
                                  navigate("/vehicles");
                                }}
                              >
                                {dr.assignedVehicle.name}
                              </span>
                              <div className="t-sub">
                                <span className="plate">{dr.assignedVehicle.plateNumber}</span>
                              </div>
                            </>
                          ) : (
                            <span className="tag">None</span>
                          )}
                        </td>
                        <td>
                          {dr.documentCount} on file
                          {dr.expiringDocumentCount > 0 && (
                            <>
                              {" "}
                              <span className="badge warn">{dr.expiringDocumentCount} expiring</span>
                            </>
                          )}
                        </td>
                        <td>
                          <div className="flex">
                            <div className={`bar-mini ${ratingBarTone(dr.rating)}`} style={{ width: 52, flex: "none" }}>
                              <i style={{ width: `${(dr.rating / 5) * 100}%` }} />
                            </div>
                            <span className="mono" style={{ fontSize: 12 }}>
                              {dr.rating.toFixed(1)}
                            </span>
                          </div>
                        </td>
                        <td>
                          <span className={`badge ${statusTone(dr.status)}`}>{dr.status}</span>
                        </td>
                        <td>
                          <RowMenu
                            items={[
                              { label: "Open profile", icon: Eye, onSelect: () => navigate(`/drivers/${dr.id}`) },
                              { label: "Add document", icon: FileText, onSelect: () => setDocFor(dr) },
                              { label: "Edit driver", icon: Pencil, onSelect: () => openEdit(dr.id) },
                              { separator: true },
                              { label: "Delete driver", icon: Trash2, danger: true, onSelect: () => setDeleting(dr) },
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
                        <UsersIcon />
                        <h3>{data?.totalCount ? "No drivers match" : "No drivers yet"}</h3>
                        <p>{data?.totalCount ? "Adjust the filters or add a driver." : "Add your first driver to start tracking licences and documents."}</p>
                      </div>
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <DriverFormModal
        open={formOpen}
        driver={editing}
        onClose={() => setFormOpen(false)}
        onSaved={(saved) => {
          setFormOpen(false);
          if (editing) load();
          else navigate(`/drivers/${saved.id}`);
        }}
      />

      <DocumentFormModal
        open={!!docFor}
        driverId={docFor?.id ?? ""}
        driverName={docFor?.fullName ?? ""}
        onClose={() => setDocFor(null)}
        onSaved={() => {
          setDocFor(null);
          load();
        }}
      />

      <ConfirmDialog
        open={!!deleting}
        title="Delete driver"
        message={`${deleting?.fullName ?? "This driver"} will be removed and unassigned from any vehicle. Their documents are deleted too.`}
        onConfirm={confirmDelete}
        onClose={() => setDeleting(null)}
      />
    </div>
  );
}
