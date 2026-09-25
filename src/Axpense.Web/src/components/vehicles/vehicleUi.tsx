import { useEffect, useState } from "react";
import { vehiclesApi } from "../../lib/api";
import type { PmState, VehicleAlert, VehicleOptions } from "../../lib/types";
import { VehicleArt } from "./VehicleArt";

/** Presentation helpers for the Vehicles module. Every number and state comes from the API. */

export type BadgeClass = "ok" | "warn" | "bad" | "plain" | "info";

export function vehicleStatusTone(status: string): BadgeClass {
  switch (status) {
    case "Active":
      return "ok";
    case "In transit":
      return "info";
    case "Under maintenance":
      return "warn";
    case "Inoperable / Down":
      return "bad";
    default:
      return "plain";
  }
}

export function pmTone(status: PmState["status"]): "" | "warn" | "bad" {
  return status === "overdue" ? "bad" : status === "due" ? "warn" : "";
}

export const PM_FILTERS = [
  { value: "ok", label: "On schedule" },
  { value: "due", label: "Due soon" },
  { value: "overdue", label: "Overdue" },
];

export const PM_TRIGGER_LABELS: Record<string, string> = {
  usage: "Usage — distance",
  time: "Time — days",
  hours: "Engine hours",
  hybrid: "Hybrid — whichever comes first",
};

export function alertTone(a: VehicleAlert): "bad" | "warn" | "info" {
  return a.severity === "critical" ? "bad" : a.severity === "warning" ? "warn" : "info";
}

export function renewalTone(daysLeft: number | null | undefined, reminderDays = 30): BadgeClass {
  if (daysLeft == null) return "plain";
  if (daysLeft < 0) return "bad";
  return daysLeft <= reminderDays ? "warn" : "ok";
}

export const num = (v: number | null | undefined, digits = 0) =>
  v == null ? "—" : Number(v).toLocaleString("en-US", { maximumFractionDigits: digits });

export const money = (v: number | null | undefined, digits = 2) =>
  v == null ? "—" : `EGP ${Number(v).toLocaleString("en-US", { minimumFractionDigits: digits, maximumFractionDigits: digits })}`;

let optionsCache: Promise<VehicleOptions> | null = null;
/** Allowed values (statuses, categories, fuel types…) served by the API — never hard-coded in the UI. */
export function useVehicleOptions() {
  const [options, setOptions] = useState<VehicleOptions | null>(null);
  useEffect(() => {
    optionsCache ??= vehiclesApi.options().catch((e) => {
      optionsCache = null;
      throw e;
    });
    let alive = true;
    optionsCache.then((o) => alive && setOptions(o)).catch(() => undefined);
    return () => {
      alive = false;
    };
  }, []);
  return options;
}

/** Loads the vehicle photo through the authenticated API (img tags can't send the bearer token). */
export function useVehiclePhoto(id: string | undefined, hasPhoto: boolean, version = 0) {
  const [url, setUrl] = useState<string | null>(null);
  useEffect(() => {
    if (!id || !hasPhoto) {
      setUrl(null);
      return;
    }
    let alive = true;
    let objectUrl: string | null = null;
    vehiclesApi
      .photoUrl(id)
      .then((u) => {
        objectUrl = u;
        if (alive) setUrl(u);
        else URL.revokeObjectURL(u);
      })
      .catch(() => alive && setUrl(null));
    return () => {
      alive = false;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [id, hasPhoto, version]);
  return url;
}

/** List thumbnail: the uploaded photo, or the category illustration. */
export function VehicleThumb({ id, category, hasPhoto }: { id: string; category: string; hasPhoto: boolean }) {
  const url = useVehiclePhoto(id, hasPhoto);
  return url ? (
    <img className="veh-photo" src={url} alt="" />
  ) : (
    <div className="veh-photo veh-art">
      <VehicleArt category={category} />
    </div>
  );
}

/** Odometer + PM runway bar with the pre-alert tick. `wide` adds the interval detail line. */
export function PmGauge({ odometer, unit, pm, wide }: { odometer?: number | null; unit: string; pm: PmState; wide?: boolean }) {
  if (pm.status === "none")
    return (
      <div className="gauge">
        <div className="gauge-top">
          <span className="gauge-val">
            {num(odometer)} <span style={{ color: "var(--muted)", fontSize: 11 }}>{unit}</span>
          </span>
        </div>
        <div className="gauge-rem">No PM schedule</div>
      </div>
    );
  return (
    <div className={`gauge ${pmTone(pm.status)}`} style={wide ? { minWidth: 230 } : undefined}>
      <div className="gauge-top">
        <span className="gauge-val">
          {num(odometer)} <span style={{ color: "var(--muted)", fontSize: 11 }}>{unit}</span>
        </span>
        <span className="gauge-rem">{pm.label}</span>
      </div>
      <div className="gauge-bar">
        <div className="gauge-fill" style={{ width: `${Math.min(100, pm.percent)}%` }} />
        {pm.preAlertPercent != null && <span className="gauge-tick" style={{ left: `${pm.preAlertPercent}%` }} title="Pre-alert threshold" />}
      </div>
      {wide && pm.detail && (
        <div className="gauge-rem" style={{ marginTop: 5 }}>
          {pm.detail}
        </div>
      )}
    </div>
  );
}
