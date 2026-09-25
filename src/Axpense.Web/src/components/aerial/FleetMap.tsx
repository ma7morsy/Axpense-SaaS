import { useEffect, useRef, type CSSProperties, type ReactNode } from "react";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import { Crosshair, Minus, Plus } from "lucide-react";
import type { Motion, TrackedVehicle } from "../../lib/types";
import { MOTION } from "./motion";

/** Base layers. Override with VITE_MAP_TILE_URL / VITE_MAP_SATELLITE_URL (e.g. a paid provider in production). */
const TILES = {
  map: {
    url: import.meta.env.VITE_MAP_TILE_URL ?? "https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png",
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> &copy; <a href="https://carto.com/attributions">CARTO</a>',
  },
  satellite: {
    url:
      import.meta.env.VITE_MAP_SATELLITE_URL ??
      "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
    attribution: "Imagery &copy; Esri, Maxar, Earthstar Geographics",
  },
};

const CAIRO: L.LatLngTuple = [30.0444, 31.2357];
const PANEL_PADDING: L.PointTuple = [410, 40];
const CAR_ICON =
  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M5 17h14M5 17a2 2 0 1 0 4 0M15 17a2 2 0 1 0 4 0M3 13l2-5a2 2 0 0 1 1.9-1.4h10.2A2 2 0 0 1 19 8l2 5v4H3z"/></svg>';

function markerHtml(v: TrackedVehicle, selected: boolean, dimmed: boolean) {
  const moving = v.motion === "moving";
  const cls = ["amk", moving && "mv", selected && "sel", dimmed && "dim"].filter(Boolean).join(" ");
  const plate = v.plateNumber.replace(/[&<>"]/g, "");
  return `<div class="${cls}" style="--c:${MOTION[v.motion].color}">
    <span class="pulse"></span>
    <span class="dir" style="transform:rotate(${Math.round(v.headingDegrees)}deg)"><i></i></span>
    <span class="dot">${CAR_ICON}</span>
    <span class="plate">${plate}</span></div>`;
}

/** Splits a route at the given progress (0–100) into [done, full] polylines. */
function splitRoute(path: [number, number][], progressPercent: number): L.LatLngTuple[] {
  if (path.length < 2) return path;
  const seg = path.slice(1).map((p, i) => L.latLng(path[i]).distanceTo(L.latLng(p)));
  let remaining = (seg.reduce((a, b) => a + b, 0) * progressPercent) / 100;
  const done: L.LatLngTuple[] = [path[0]];
  for (let i = 0; i < seg.length; i++) {
    if (remaining >= seg[i]) {
      done.push(path[i + 1]);
      remaining -= seg[i];
      continue;
    }
    const t = seg[i] ? remaining / seg[i] : 0;
    done.push([path[i][0] + (path[i + 1][0] - path[i][0]) * t, path[i][1] + (path[i + 1][1] - path[i][1]) * t]);
    break;
  }
  return done;
}

export function FleetMap({
  vehicles,
  visibleIds,
  selectedId,
  route,
  satellite,
  onSatelliteChange,
  onSelect,
  children,
}: {
  vehicles: TrackedVehicle[];
  /** Vehicles matching the list filters; the rest are dimmed. */
  visibleIds: Set<string>;
  selectedId: string | null;
  route: { path: [number, number][]; progressPercent: number } | null;
  satellite: boolean;
  onSatelliteChange: (sat: boolean) => void;
  onSelect: (id: string) => void;
  children?: ReactNode;
}) {
  const el = useRef<HTMLDivElement>(null);
  const map = useRef<L.Map | null>(null);
  const layers = useRef<{ map: L.TileLayer; satellite: L.TileLayer } | null>(null);
  const markers = useRef(new Map<string, { marker: L.Marker; html: string }>());
  const routeLayer = useRef<L.LayerGroup | null>(null);
  const fitted = useRef(false);
  const onSelectRef = useRef(onSelect);
  onSelectRef.current = onSelect;

  // ---- create the map once
  useEffect(() => {
    if (!el.current) return;
    const m = L.map(el.current, { zoomControl: false, attributionControl: true }).setView(CAIRO, 10);
    const base = {
      map: L.tileLayer(TILES.map.url, { attribution: TILES.map.attribution, maxZoom: 19, subdomains: "abcd" }),
      satellite: L.tileLayer(TILES.satellite.url, { attribution: TILES.satellite.attribution, maxZoom: 19 }),
    };
    base.map.addTo(m);
    routeLayer.current = L.layerGroup().addTo(m);
    // Marker positions animate between polls; disable that while zooming so they don't drift.
    m.on("zoomstart", () => el.current?.classList.add("no-anim"));
    m.on("zoomend", () => requestAnimationFrame(() => requestAnimationFrame(() => el.current?.classList.remove("no-anim"))));
    map.current = m;
    layers.current = base;
    const markerStore = markers.current;
    return () => {
      m.remove();
      map.current = null;
      markerStore.clear();
      fitted.current = false;
    };
  }, []);

  // ---- base layer
  useEffect(() => {
    const m = map.current;
    const l = layers.current;
    if (!m || !l) return;
    const [on, off] = satellite ? [l.satellite, l.map] : [l.map, l.satellite];
    if (m.hasLayer(off)) m.removeLayer(off);
    if (!m.hasLayer(on)) on.addTo(m);
  }, [satellite]);

  // ---- markers
  useEffect(() => {
    const m = map.current;
    if (!m) return;
    const seen = new Set<string>();
    for (const v of vehicles) {
      seen.add(v.id);
      const html = markerHtml(v, v.id === selectedId, !visibleIds.has(v.id));
      const existing = markers.current.get(v.id);
      if (!existing) {
        const marker = L.marker([v.latitude, v.longitude], {
          icon: L.divIcon({ html, className: "amk-wrap", iconSize: [40, 40], iconAnchor: [20, 20] }),
          keyboard: true,
          title: `${v.name} · ${v.plateNumber}`,
          zIndexOffset: v.id === selectedId ? 1000 : 0,
        })
          .on("click", () => onSelectRef.current(v.id))
          .addTo(m);
        markers.current.set(v.id, { marker, html });
      } else {
        existing.marker.setLatLng([v.latitude, v.longitude]);
        existing.marker.setZIndexOffset(v.id === selectedId ? 1000 : 0);
        if (existing.html !== html) {
          existing.marker.setIcon(L.divIcon({ html, className: "amk-wrap", iconSize: [40, 40], iconAnchor: [20, 20] }));
          existing.html = html;
        }
      }
    }
    for (const [id, { marker }] of markers.current) {
      if (!seen.has(id)) {
        marker.remove();
        markers.current.delete(id);
      }
    }
    if (!fitted.current && vehicles.length) {
      fitted.current = true;
      fitAll();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [vehicles, selectedId, visibleIds]);

  // ---- focus the selected vehicle (once per selection)
  useEffect(() => {
    const m = map.current;
    const v = vehicles.find((x) => x.id === selectedId);
    if (!m || !v) return;
    const p = L.latLng(v.latitude, v.longitude);
    m.flyToBounds(L.latLngBounds(p, p), { paddingBottomRight: PANEL_PADDING, maxZoom: Math.max(m.getZoom(), 13), duration: 0.8 });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedId]);

  // ---- selected route
  useEffect(() => {
    const group = routeLayer.current;
    if (!group) return;
    group.clearLayers();
    if (!route || route.path.length < 2) return;
    L.polyline(route.path, { color: "#8FA8BF", weight: 5, dashArray: "1 11", lineCap: "round", interactive: false }).addTo(group);
    L.polyline(splitRoute(route.path, route.progressPercent), { color: "#1F7BD8", weight: 5, lineCap: "round", interactive: false }).addTo(group);
  }, [route]);

  function fitAll() {
    const m = map.current;
    if (!m || !vehicles.length) return;
    const bounds = L.latLngBounds(vehicles.map((v) => [v.latitude, v.longitude] as L.LatLngTuple));
    m.fitBounds(bounds.pad(0.15), {
      paddingTopLeft: [60, 60],
      paddingBottomRight: selectedId ? PANEL_PADDING : [60, 60],
      maxZoom: 13,
    });
  }

  const keys: Motion[] = ["moving", "idle", "parked", "workshop"];

  return (
    <>
      <div ref={el} className="aer-canvas" role="application" aria-label="Fleet map" />
      <div className="aer-ctl">
        <div className="tog" role="group" aria-label="Map style">
          <button className={!satellite ? "on" : ""} onClick={() => onSatelliteChange(false)}>
            Map
          </button>
          <button className={satellite ? "on" : ""} onClick={() => onSatelliteChange(true)}>
            Satellite
          </button>
        </div>
        <div className="zc">
          <button aria-label="Zoom in" onClick={() => map.current?.zoomIn()}>
            <Plus />
          </button>
          <button aria-label="Zoom out" onClick={() => map.current?.zoomOut()}>
            <Minus />
          </button>
          <button aria-label="Fit all vehicles" title="Fit all vehicles" onClick={fitAll}>
            <Crosshair />
          </button>
        </div>
      </div>
      <div className="aer-key" aria-hidden="true">
        {keys.map((k) => (
          <span key={k} style={{ "--c": MOTION[k].color } as CSSProperties}>
            <i />
            {MOTION[k].label}
          </span>
        ))}
      </div>
      {children}
    </>
  );
}
