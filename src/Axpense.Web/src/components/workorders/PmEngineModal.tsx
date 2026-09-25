import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { AlertTriangle, CheckCircle2 } from "lucide-react";
import { Modal } from "../ui/Modal";
import { Button } from "../ui/Button";
import { PageSpinner } from "../ui/Spinner";
import { useToast } from "../ui/Toast";
import { workOrdersApi } from "../../lib/api";
import type { PmEnginePreview } from "../../lib/types";

const nf = (n: number) => n.toLocaleString("en-US", { maximumFractionDigits: 2 });
const KIND: Record<string, string> = { distance: "Distance", time: "Time", "engine hours": "Engine hours" };
const UNIT: Record<string, string> = { km: "km", days: "days", hr: "hr" };

/**
 * Preview of what the PM engine will do (vehicles due / overdue, the library task it will raise, estimated cost, whether an
 * open preventive order already covers the vehicle), then generates the work orders on confirm.
 */
export function PmEngineModal({ open, onClose, onDone }: { open: boolean; onClose: () => void; onDone: () => void }) {
  const toast = useToast();
  const [data, setData] = useState<PmEnginePreview | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!open) return;
    setData(null);
    setError("");
    workOrdersApi
      .pmPreview()
      .then(setData)
      .catch((e) => setError(e instanceof Error ? e.message : "Could not load the PM engine preview"));
  }, [open]);

  const run = async () => {
    setBusy(true);
    try {
      const r = await workOrdersApi.runPmEngine();
      toast(
        r.created
          ? `${r.created} preventive work order${r.created === 1 ? "" : "s"} generated (${r.codes.join(", ")})`
          : "Nothing new to generate — every due vehicle already has an open preventive order"
      );
      onDone();
      onClose();
    } catch (e) {
      toast(e instanceof Error ? e.message : "Could not run the PM engine", "bad");
    } finally {
      setBusy(false);
    }
  };

  const sub = data
    ? `Pre-alert ${nf(data.distancePreAlertKm)} km / ${data.timePreAlertDays} days ahead · auto work orders ${data.autoWorkOrders ? "enabled" : "disabled"}`
    : undefined;

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Preventive maintenance engine"
      description={sub}
      size="wide"
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={busy}>
            {data && data.toCreate === 0 ? "Close" : "Cancel"}
          </Button>
          {data && data.toCreate > 0 && (
            <Button onClick={run} disabled={busy}>
              {busy ? "Generating…" : `Generate ${data.toCreate} work order(s)`}
            </Button>
          )}
        </>
      }
    >
      {error ? (
        <div className="empty">
          <h3>Unable to load</h3>
          <p>{error}</p>
        </div>
      ) : !data ? (
        <PageSpinner />
      ) : data.rows.length === 0 ? (
        <div className="empty">
          <CheckCircle2 />
          <h3>Every vehicle is inside its interval</h3>
          <p>Nothing to generate right now.</p>
        </div>
      ) : (
        <>
          <div className="tbl-wrap pm-prev">
            <table>
              <thead>
                <tr>
                  <th>Vehicle</th>
                  <th>Trigger</th>
                  <th>State</th>
                  <th>Task to generate</th>
                  <th className="num">Est. cost</th>
                  <th>Result</th>
                </tr>
              </thead>
              <tbody>
                {data.rows.map((r) => (
                  <tr key={r.vehicleId}>
                    <td>
                      <Link className="t-main link" to={`/vehicles/${r.vehicleId}`} onClick={onClose}>
                        {r.vehicleName}
                      </Link>
                      <div className="t-sub">
                        <span className="plate">{r.plateNumber}</span>
                      </div>
                    </td>
                    <td className="t-sub">
                      {KIND[r.leadKind] ?? r.leadKind} · Every {nf(r.interval)} {UNIT[r.unit] ?? r.unit}
                    </td>
                    <td>
                      <span className={`badge ${r.status === "overdue" ? "bad" : "warn"}`}>{r.label}</span>
                    </td>
                    <td>
                      {r.taskName}
                      <div className="t-sub">
                        {r.role}
                        {r.durationHours ? ` · ${nf(r.durationHours)} h` : ""}
                      </div>
                    </td>
                    <td className="num">EGP {nf(r.estimatedCost)}</td>
                    <td>
                      {r.willCreate ? (
                        <span className="badge ok">Will be created</span>
                      ) : (
                        <span className="badge info">{r.openOrderCode} already open</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {data.blockDispatch && data.dispatchBlocked > 0 && (
            <div className="banner bad" style={{ margin: "16px 0 0" }}>
              <AlertTriangle />
              <div>
                <div className="bt">Dispatch is blocked on {data.dispatchBlocked} vehicle(s)</div>
                <div className="bd">The safety rule keeps them off the road until the generated order is closed.</div>
              </div>
            </div>
          )}
          {data.toCreate > 0 && (
            <p className="t-sub" style={{ marginTop: 12 }}>
              Estimated total for the new orders: <b>EGP {nf(data.estimatedTotal)}</b>. Tasks and costs come from the PM task library in Settings → Maintenance.
            </p>
          )}
        </>
      )}
    </Modal>
  );
}
