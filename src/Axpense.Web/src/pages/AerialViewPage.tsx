import { useCallback, useEffect, useMemo, useRef, useState, type CSSProperties } from "react";
import { Search } from "lucide-react";
import { FleetMap } from "../components/aerial/FleetMap";
import { VehicleStatusPanel } from "../components/aerial/VehicleStatusPanel";
import { MOTION, MOTION_FILTERS } from "../components/aerial/motion";
import { VehicleArt } from "../components/vehicles/VehicleArt";
import { PageSpinner } from "../components/ui/Spinner";
import { trackingApi } from "../lib/api";
import type { FleetTracking, Motion, TrackedVehicleDetail } from "../lib/types";
import "../components/aerial/aerial.css";

const POLL_MS = 4000;

export default function AerialViewPage() {
  const [fleet, setFleet] = useState<FleetTracking | null>(null);
  const [detail, setDetail] = useState<TrackedVehicleDetail | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [q, setQ] = useState("");
  const [filter, setFilter] = useState<"all" | Motion>("all");
  const [satellite, setSatellite] = useState(false);
  const [offline, setOffline] = useState(false);
  const [loading, setLoading] = useState(true);
  const selectedRef = useRef<string | null>(null);
  selectedRef.current = selectedId;

  const poll = useCallback(async () => {
    try {
      const f = await trackingApi.fleet();
      setFleet(f);
      const sel = selectedRef.current;
      if (sel) {
        if (f.items.some((v) => v.id === sel)) {
          const d = await trackingApi.vehicle(sel);
          if (selectedRef.current === sel) setDetail(d);
        } else {
          setSelectedId(null);
          setDetail(null);
        }
      }
      setOffline(false);
    } catch {
      setOffline(true);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    poll();
    const t = window.setInterval(() => {
      if (!document.hidden) poll();
    }, POLL_MS);
    return () => window.clearInterval(t);
  }, [poll]);

  const select = async (id: string) => {
    setSelectedId(id);
    setDetail((d) => (d?.id === id ? d : null));
    try {
      const d = await trackingApi.vehicle(id);
      if (selectedRef.current === id) setDetail(d);
    } catch {
      setOffline(true);
    }
  };

  const close = () => {
    setSelectedId(null);
    setDetail(null);
  };

  const items = fleet?.items ?? [];
  const visible = useMemo(() => {
    const term = q.trim().toLowerCase();
    return items.filter(
      (v) =>
        (filter === "all" || v.motion === filter) &&
        (!term || `${v.name} ${v.plateNumber} ${v.driverName ?? ""}`.toLowerCase().includes(term))
    );
  }, [items, q, filter]);
  const visibleIds = useMemo(() => new Set(visible.map((v) => v.id)), [visible]);
  const route = useMemo(
    () => (detail?.trip ? { path: detail.trip.path, progressPercent: detail.trip.progressPercent } : null),
    [detail?.trip?.path, detail?.trip?.progressPercent]
  );

  const counts = fleet?.counts;
  const countOf = (k: "all" | Motion) => (counts ? counts[k] : 0);

  return (
    <div>
      <div className="page-head aer-head">
        <div>
          <h1>Aerial view</h1>
          <p className="sub">Live position, route and condition of every vehicle.</p>
        </div>
        <div className="head-actions">
          {offline ? (
            <span className="live-pill off">
              <i />
              Connection lost · retrying
            </span>
          ) : (
            <span className="live-pill">
              <i />
              Live{fleet?.simulated ? " · simulated telemetry" : ""}
            </span>
          )}
        </div>
      </div>

      <div className="aer">
        <div className="aer-list">
          <div className="al-top">
            <div className="al-search">
              <Search />
              <input placeholder="Search vehicle, plate or driver" value={q} onChange={(e) => setQ(e.target.value)} aria-label="Search vehicles" />
            </div>
            <div className="al-chips" role="group" aria-label="Filter by status">
              {MOTION_FILTERS.map((f) => (
                <button key={f.key} className={filter === f.key ? "on" : ""} onClick={() => setFilter(f.key)} aria-pressed={filter === f.key}>
                  {f.label} <em>{countOf(f.key)}</em>
                </button>
              ))}
            </div>
          </div>
          <div className="al-body">
            {loading ? (
              <PageSpinner />
            ) : visible.length ? (
              visible.map((v) => {
                const m = MOTION[v.motion];
                return (
                  <button key={v.id} className={`al-i ${selectedId === v.id ? "on" : ""}`} onClick={() => select(v.id)}>
                    <span className="al-art">
                      <VehicleArt category={v.category} />
                    </span>
                    <span className="al-t">
                      <b>{v.name}</b>
                      <span>
                        {v.plateNumber} · {v.driverName ?? "Unassigned"}
                      </span>
                    </span>
                    <span className="al-s" style={{ "--c": m.color } as CSSProperties}>
                      <b>{v.motion === "moving" ? `${v.speedKmh} km/h` : ""}</b>
                      <small>
                        <i />
                        {m.label}
                      </small>
                    </span>
                  </button>
                );
              })
            ) : (
              <div className="empty" style={{ padding: "28px 0" }}>
                <h3>{items.length ? "No vehicles match" : "No vehicles to track"}</h3>
                <p>{items.length ? "Change the search or status filter." : "Add active vehicles to see them on the map."}</p>
              </div>
            )}
          </div>
        </div>

        <div className={`aer-map ${satellite ? "sat" : ""}`}>
          <FleetMap
            vehicles={items}
            visibleIds={visibleIds}
            selectedId={selectedId}
            route={route}
            satellite={satellite}
            onSatelliteChange={setSatellite}
            onSelect={select}
          >
            <VehicleStatusPanel detail={detail} open={!!selectedId} onClose={close} />
          </FleetMap>
        </div>
      </div>
    </div>
  );
}
