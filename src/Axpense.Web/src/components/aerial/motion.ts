import type { Motion } from "../../lib/types";

export const MOTION: Record<Motion, { label: string; panel: string; color: string }> = {
  moving: { label: "Moving", panel: "Moving", color: "#19B394" },
  idle: { label: "Idle", panel: "Idle · engine on", color: "#E8912D" },
  parked: { label: "Parked", panel: "Parked", color: "#6B7C8F" },
  workshop: { label: "In workshop", panel: "In workshop", color: "#1F7BD8" },
};

export const MOTION_FILTERS: { key: "all" | Motion; label: string }[] = [
  { key: "all", label: "All" },
  { key: "moving", label: "Moving" },
  { key: "idle", label: "Idle" },
  { key: "parked", label: "Parked" },
  { key: "workshop", label: "Workshop" },
];
