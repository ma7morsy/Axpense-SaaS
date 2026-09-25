import { useCallback, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { AlertTriangle, Eye, FileText, Pencil, Plus, Trash2, Truck } from "lucide-react";
import { PageHeader } from "../components/ui/PageHeader";
import { Button } from "../components/ui/Button";
import { PageSpinner } from "../components/ui/Spinner";
import { RowMenu, type RowMenuItem } from "../components/ui/RowMenu";
import { ConfirmDialog } from "../components/ui/ConfirmDialog";
import { useToast } from "../components/ui/Toast";
import { DriverFormModal } from "../components/drivers/DriverFormModal";
import { DocumentFormModal } from "../components/drivers/DocumentFormModal";
import { documentStateLabel, documentStateTone, expiryTone, statusTone, EXPIRY_REMINDER_DAYS } from "../components/drivers/driverUi";
import { ApiError, driversApi } from "../lib/api";
import type { DriverDocument, DriverProfile, DriverTimelineEvent } from "../lib/types";
import { formatDay, initials } from "../lib/utils";

function timelineText(e: DriverTimelineEvent) {
  if (e.kind === "fuel") {
    return {
      title: `Fuel entry — ${e.station || "Unknown station"}`,
      sub: `${(e.liters ?? 0).toLocaleString("en-US")} L · EGP ${Math.round(e.amount ?? 0).toLocaleString("en-US")}`,
    };
  }
  return {
    title: `Assigned to ${e.vehicleName ?? "a vehicle"}`,
    sub: e.note || (e.endDate ? `Ended ${formatDay(e.endDate)}` : "Current assignment"),
  };
}

export default function DriverProfilePage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const toast = useToast();

  const [driver, setDriver] = useState<DriverProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<{ status: number; message: string } | null>(null);
  const [editOpen, setEditOpen] = useState(false);
  const [docOpen, setDocOpen] = useState(false);
  const [removingDoc, setRemovingDoc] = useState<DriverDocument | null>(null);

  const load = useCallback(() => {
    setError(null);
    return driversApi
      .get(id)
      .then(setDriver)
      .catch((err) => setError({ status: err instanceof ApiError ? err.status : 0, message: err instanceof Error ? err.message : "" }))
      .finally(() => setLoading(false));
  }, [id]);

  useEffect(() => {
    setLoading(true);
    load();
  }, [load]);

  const viewScan = async (doc: DriverDocument) => {
    // Open the tab synchronously (popup blockers), then point it at the fetched file.
    const tab = window.open("", "_blank");
    try {
      const url = await driversApi.documentFileUrl(id, doc.id);
      if (tab) tab.location.href = url;
      else window.location.assign(url);
    } catch (err) {
      tab?.close();
      toast(err instanceof Error ? err.message : "Could not open the file", "bad");
    }
  };

  const removeDoc = async () => {
    if (!removingDoc) return;
    try {
      await driversApi.removeDocument(id, removingDoc.id);
      toast("Document removed");
      setRemovingDoc(null);
      load();
    } catch (err) {
      toast(err instanceof Error ? err.message : "Could not remove the document", "bad");
    }
  };

  if (loading) return <PageSpinner />;

  if (error || !driver) {
    const title = error?.status === 404 ? "Driver not found" : error?.status === 403 ? "You don't have access to this driver" : "The driver could not be loaded";
    return (
      <div className="empty">
        <h3>{title}</h3>
        <p>
          <Button variant="secondary" onClick={() => navigate("/drivers")}>
            Back to drivers
          </Button>
        </p>
      </div>
    );
  }

  const n = driver.licenseDaysLeft;
  const s = driver.stats;

  return (
    <div>
      <PageHeader
        eyebrow={driver.employeeNumber}
        title={driver.fullName}
        subtitle={`${driver.licenseClass}${driver.hireDate ? ` · joined ${formatDay(driver.hireDate)}` : ""}`}
        action={
          <>
            <Button variant="secondary" onClick={() => setDocOpen(true)}>
              <FileText /> Add document
            </Button>
            <Button onClick={() => setEditOpen(true)}>
              <Pencil /> Edit
            </Button>
          </>
        }
      />

      {n != null && n <= EXPIRY_REMINDER_DAYS && (
        <div className={`banner ${n < 0 ? "bad" : "warn"}`} role="alert">
          <AlertTriangle />
          <div>
            <div className="bt">{n < 0 ? `Licence expired ${Math.abs(n)} days ago` : `Licence expires in ${n} days`}</div>
            <div className="bd">
              {driver.licenseNumber} · {driver.licenseClass} · renew before the next dispatch.
            </div>
          </div>
          <Button variant="secondary" size="sm" onClick={() => setEditOpen(true)}>
            Update licence
          </Button>
        </div>
      )}

      <div className="split">
        <div className="card">
          <div className="card-b">
            <div className="flex" style={{ marginBottom: 14 }}>
              <span className="avatar" style={{ width: 46, height: 46, fontSize: 15 }}>
                {initials(driver.fullName)}
              </span>
              <div>
                <div style={{ fontWeight: 600, fontSize: 16 }}>{driver.fullName}</div>
                <span className={`badge ${statusTone(driver.status)}`}>{driver.status}</span>
              </div>
            </div>
            <dl className="kv">
              <dt>Employee no.</dt>
              <dd className="mono">{driver.employeeNumber}</dd>
              <dt>Phone</dt>
              <dd className="mono" style={{ fontSize: 12.5 }}>
                {driver.phone || "—"}
              </dd>
              <dt>Email</dt>
              <dd style={{ fontSize: 12.5 }}>{driver.email || "—"}</dd>
              <dt>National ID</dt>
              <dd className="mono" style={{ fontSize: 12 }}>
                {driver.nationalId || "—"}
              </dd>
            </dl>
            <div className="divider" />
            <div className="fs-t">Licence</div>
            <dl className="kv">
              <dt>Number</dt>
              <dd className="mono" style={{ fontSize: 12.5 }}>
                {driver.licenseNumber || "—"}
              </dd>
              <dt>Class</dt>
              <dd>{driver.licenseClass}</dd>
              <dt>Issued</dt>
              <dd>{formatDay(driver.licenseIssuedDate)}</dd>
              <dt>Expires</dt>
              <dd>{driver.licenseExpiryDate ? <span className={`badge ${expiryTone(n)}`}>{formatDay(driver.licenseExpiryDate)}</span> : "—"}</dd>
            </dl>
            <div className="divider" />
            <div className="fs-t">Current vehicle</div>
            {driver.currentVehicle ? (
              <div className="flex">
                <div className="veh-photo" style={{ display: "grid", placeItems: "center" }}>
                  <Truck style={{ width: 26, height: 26, color: "var(--red)" }} />
                </div>
                <div>
                  <div className="link" onClick={() => navigate("/vehicles")}>
                    {driver.currentVehicle.name}
                  </div>
                  <div className="t-sub">
                    <span className="plate">{driver.currentVehicle.plateNumber}</span>
                  </div>
                </div>
              </div>
            ) : (
              <div className="note">Not driving anything right now. Assign from a vehicle profile.</div>
            )}
          </div>
        </div>

        <div>
          <div className="grid g-3" style={{ marginBottom: 14 }}>
            <div className="stat">
              <div className="k">Rating</div>
              <div className="v">{s.rating.toFixed(1)}</div>
              <div className="m">out of 5.0</div>
            </div>
            <div className="stat neutral">
              <div className="k">Inspections run</div>
              <div className="v">{s.inspectionsRun}</div>
              <div className="m">{s.failedItemsFound} failed items found</div>
            </div>
            <div className={`stat ${s.issuesOpen ? "warn" : ""}`}>
              <div className="k">Issues reported</div>
              <div className="v">{s.issuesReported}</div>
              <div className="m">{s.issuesOpen} still open</div>
            </div>
          </div>

          <div className="card" style={{ marginBottom: 14 }}>
            <div className="card-h">
              <h2>Documents</h2>
              <Button size="sm" className="teal" onClick={() => setDocOpen(true)}>
                <Plus /> Add document
              </Button>
            </div>
            <div className="tbl-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Document</th>
                    <th>Expires</th>
                    <th>State</th>
                    <th style={{ textAlign: "right" }}>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {driver.documents.length ? (
                    driver.documents.map((doc) => {
                      const items: RowMenuItem[] = [];
                      if (doc.hasFile) items.push({ label: "View scan", icon: Eye, onSelect: () => viewScan(doc) });
                      items.push({ label: "Remove document", icon: Trash2, danger: true, onSelect: () => setRemovingDoc(doc) });
                      return (
                        <tr key={doc.id}>
                          <td className="t-main">{doc.name}</td>
                          <td className="mono" style={{ fontSize: 12 }}>
                            {formatDay(doc.expiryDate)}
                          </td>
                          <td>
                            <span className={`badge ${documentStateTone(doc.state)}`}>{documentStateLabel(doc.state, doc.daysLeft)}</span>
                          </td>
                          <td>
                            <RowMenu items={items} />
                          </td>
                        </tr>
                      );
                    })
                  ) : (
                    <tr>
                      <td colSpan={4}>
                        <div className="empty">
                          <FileText />
                          <h3>No documents on file</h3>
                          <p>Upload the ID and training certificates.</p>
                        </div>
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>

          <div className="card">
            <div className="card-h">
              <h2>Performance history</h2>
            </div>
            <div className="card-b">
              {driver.timeline.length ? (
                <div className="timeline">
                  {driver.timeline.map((e, i) => {
                    const t = timelineText(e);
                    return (
                      <div key={i} className={`tl-item ${e.isCurrent ? "on" : ""}`}>
                        <div className="tl-d">{formatDay(e.date)}</div>
                        <div style={{ fontWeight: 500 }}>{t.title}</div>
                        <div className="t-sub">{t.sub}</div>
                      </div>
                    );
                  })}
                </div>
              ) : (
                <div className="note">No activity recorded yet. Assignments and fuel entries will appear here.</div>
              )}
            </div>
          </div>
        </div>
      </div>

      <DriverFormModal
        open={editOpen}
        driver={driver}
        onClose={() => setEditOpen(false)}
        onSaved={(saved) => {
          setEditOpen(false);
          setDriver(saved);
        }}
      />
      <DocumentFormModal
        open={docOpen}
        driverId={driver.id}
        driverName={driver.fullName}
        onClose={() => setDocOpen(false)}
        onSaved={() => {
          setDocOpen(false);
          load();
        }}
      />
      <ConfirmDialog
        open={!!removingDoc}
        title="Remove document"
        message={`${removingDoc?.name ?? "This document"} and its attached scan will be removed from ${driver.fullName}'s file.`}
        confirmLabel="Remove"
        onConfirm={removeDoc}
        onClose={() => setRemovingDoc(null)}
      />
    </div>
  );
}
