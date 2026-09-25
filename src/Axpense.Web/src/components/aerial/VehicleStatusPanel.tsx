import { useEffect, useState, type CSSProperties } from "react";
import { useNavigate } from "react-router-dom";
import { Droplet, Gauge, MapPin, Phone, Route as RouteIcon, Timer, Users, Wrench, X } from "lucide-react";
import type { TrackedVehicleDetail } from "../../lib/types";
import { initials } from "../../lib/utils";
import { VehicleArt } from "../vehicles/VehicleArt";
import { MOTION } from "./motion";

const nf = (n: number | null | undefined, digits = 0) =>
  n == null ? "—" : Number(n).toLocaleString("en-US", { maximumFractionDigits: digits, minimumFractionDigits: 0 });

function ago(iso: string, now: number) {
  const s = Math.max(0, Math.round((now - new Date(iso).getTime()) / 1000));
  return s < 60 ? `${s} s ago` : `${Math.round(s / 60)} min ago`;
}

function eta(iso: string, remainingKm: number, speed: number) {
  const d = new Date(iso);
  const hours = remainingKm / Math.max(30, speed || 30);
  const h = Math.floor(hours);
  const m = Math.round((hours - h) * 60);
  return { at: d.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit" }), left: h ? `${h}h ${m}m` : `${m}m` };
}

/** Right-hand live panel for the selected vehicle on the aerial view. */
export function VehicleStatusPanel({
  detail,
  open,
  onClose,
}: {
  detail: TrackedVehicleDetail | null;
  open: boolean;
  onClose: () => void;
}) {
  const navigate = useNavigate();
  const [now, setNow] = useState(Date.now());
  useEffect(() => {
    const t = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(t);
  }, []);

  const d = detail;
  const m = d ? MOTION[d.motion] : null;
  const over = d ? d.speedKmh > d.speedLimitKmh : false;
  const e = d?.trip ? eta(d.trip.etaUtc, d.trip.remainingKm, d.speedKmh) : null;

  return (
    <aside className={`aer-panel ${open ? "open" : ""}`} aria-hidden={!open} aria-label="Vehicle status">
      {d && m && (
        <>
          <div className="ap-h">
            <div className="ap-art">
              <VehicleArt category={d.category} />
            </div>
            <div className="ap-t">
              <b>{d.name}</b>
              <span>
                {d.plateNumber} · {d.category}
              </span>
              <span className="ap-st" style={{ "--c": m.color } as CSSProperties}>
                <i />
                {m.panel}
              </span>
            </div>
            <button className="ap-x" onClick={onClose} aria-label="Close">
              <X size={16} />
            </button>
          </div>

          <div className="ap-body">
            <div className="ap-speed">
              <div className="sp">
                <b>{d.speedKmh}</b>
                <small>km/h</small>
              </div>
              <div className="sl">
                <div className={`bar-mini ${over ? "bad" : ""}`}>
                  <i style={{ width: `${Math.min(100, (d.speedKmh / 120) * 100)}%` }} />
                </div>
                <span className={over ? "over" : ""}>
                  Limit {d.speedLimitKmh} km/h · engine {d.engineOn ? "on" : "off"}
                  {over ? " · over the limit" : ""}
                </span>
              </div>
            </div>

            <div className="ap-grid">
              <div>
                <RouteIcon />
                <b>{nf(d.distanceTodayKm)} km</b>
                <span>Distance today</span>
              </div>
              <div>
                <Droplet />
                <b>
                  {nf(d.fuelUsedToday, 1)} {d.fuelUnit}
                </b>
                <span>{d.fuelUnit === "kWh" ? "Energy" : "Fuel"} today</span>
              </div>
              <div>
                <Timer />
                <b>{d.idleMinutesToday} min</b>
                <span>Idle time today</span>
              </div>
              <div>
                <Gauge />
                <b>{d.odometerKm != null ? `${nf(d.odometerKm)} km` : "—"}</b>
                <span>Odometer</span>
              </div>
            </div>

            <section>
              <h4>
                <MapPin />
                Location
              </h4>
              <p className="ap-addr">{d.address}</p>
              <small className="mut">
                {d.latitude.toFixed(4)}°N, {d.longitude.toFixed(4)}°E · updated {ago(d.updatedAtUtc, now)}
              </small>
            </section>

            <section>
              <h4>
                <RouteIcon />
                Route
              </h4>
              {d.trip && e ? (
                <div className="ap-route">
                  <div className="rt">
                    <i className="a" />
                    <div>
                      <b>{d.trip.origin}</b>
                      <span>Origin</span>
                    </div>
                  </div>
                  <div className="rp">
                    <div className="bar-mini">
                      <i style={{ width: `${d.trip.progressPercent}%`, background: "#1F7BD8" }} />
                    </div>
                    <div className="rs">
                      <span>
                        <b>{nf(d.trip.remainingKm)} km</b> remaining
                      </span>
                      <span>
                        ETA <b>{e.at}</b> ({e.left})
                      </span>
                    </div>
                  </div>
                  <div className="rt">
                    <i className="b" />
                    <div>
                      <b>{d.trip.destination}</b>
                      <span>Destination</span>
                    </div>
                  </div>
                </div>
              ) : (
                <>
                  <p className="mut" style={{ margin: 0 }}>
                    No active trip.
                  </p>
                  {d.lastTrip && <small className="mut">Last trip: {d.lastTrip}</small>}
                </>
              )}
            </section>

            <section>
              <h4>
                <Users />
                Driver
              </h4>
              {d.driver ? (
                <div className="ap-drv">
                  <div className="avatar-sm">{initials(d.driver.fullName)}</div>
                  <div>
                    <b className="link" onClick={() => navigate(`/drivers/${d.driver!.id}`)}>
                      {d.driver.fullName}
                    </b>
                    <span>
                      {d.driver.licenseClass} · rating {Number(d.driver.rating).toFixed(1)}
                    </span>
                  </div>
                  {d.driver.phone && (
                    <a className="btn sm" href={`tel:${d.driver.phone.replace(/\s/g, "")}`}>
                      <Phone /> Call
                    </a>
                  )}
                </div>
              ) : (
                <p className="mut" style={{ margin: 0 }}>
                  No driver assigned.
                </p>
              )}
            </section>

            <section>
              <h4>
                <Wrench />
                Maintenance due
              </h4>
              {d.maintenanceDue.length ? (
                d.maintenanceDue.map((x) => (
                  <div key={x.id} className={`ap-m ${x.status}`}>
                    <div className="mh">
                      <b>{x.name}</b>
                      <span>{x.label}</span>
                    </div>
                    <div className={`bar-mini ${x.status === "overdue" ? "bad" : x.status === "due" ? "warn" : ""}`}>
                      <i style={{ width: `${Math.min(100, x.usedPercent)}%` }} />
                    </div>
                  </div>
                ))
              ) : (
                <p className="mut" style={{ margin: 0 }}>
                  No PM schedule or fitted parts yet. Set them up from the vehicle profile.
                </p>
              )}
            </section>
          </div>

          <div className="ap-f">
            <button className="btn pri" onClick={() => navigate(`/vehicles/${d.id}`)}>
              View details
            </button>
            <button className="btn" onClick={() => navigate(`/maintenance?vehicleId=${d.id}`)}>
              <Wrench /> Schedule maintenance
            </button>
          </div>
        </>
      )}
    </aside>
  );
}
