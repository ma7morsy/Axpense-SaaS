import { useEffect, useState } from "react";
import { workOrdersApi } from "../../lib/api";
import type { WorkOrderOptions } from "../../lib/types";

export type Tone = "ok" | "warn" | "bad" | "plain" | "info";

export function woStatusTone(displayStatus: string): Tone {
  switch (displayStatus) {
    case "Completed":
      return "ok";
    case "In progress":
      return "warn";
    case "Overdue":
      return "bad";
    default:
      return "info";
  }
}

export function priorityTone(p: string): Tone {
  return p === "Critical" || p === "High" ? "bad" : p === "Medium" ? "warn" : "plain";
}

export const egp = (v: number | null | undefined, digits = 0) =>
  `EGP ${Number(v ?? 0).toLocaleString("en-US", { minimumFractionDigits: digits, maximumFractionDigits: digits })}`;

let cache: Promise<WorkOrderOptions> | null = null;
/** Allowed values + technicians + part / task categories, from the API. */
export function useWorkOrderOptions() {
  const [options, setOptions] = useState<WorkOrderOptions | null>(null);
  useEffect(() => {
    cache ??= workOrdersApi.options().catch((e) => {
      cache = null;
      throw e;
    });
    let alive = true;
    cache.then((o) => alive && setOptions(o)).catch(() => undefined);
    return () => {
      alive = false;
    };
  }, []);
  return options;
}
/** Forget cached options (e.g. after categories change in Settings). */
export const resetWorkOrderOptions = () => {
  cache = null;
};
