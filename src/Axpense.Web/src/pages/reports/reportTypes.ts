import type { ReportStat } from "../../lib/types";

/** Shapes returned by /api/reports/* (Axpense.Service/Reports/ReportDtos.cs). */
export interface FuelReport {
  days: number;
  from: string;
  to: string;
  stats: ReportStat[];
  byVehicle: { vehicleId: string; vehicle: string; plate: string; fuelType: string; unit: string; entries: number; quantity: number; cost: number;
    sharePct: number; perHundred: number | null; costPerKm: number | null }[];
  log: { id: string; date: string; vehicleId: string; vehicle: string; plate: string; station: string | null; fuelType: string | null; quantity: number;
    unit: string; unitPrice: number; total: number; odometer: number | null }[];
  logTotal: number;
}
export interface BudgetReport {
  year: number;
  hasAnnual: boolean;
  stats: ReportStat[];
  plan: { month: number; label: string; budget: number; actual: number; variance: number; variancePct: number | null; over: boolean; future: boolean }[];
  categories: { name: string; color: string; sharePct: number; budget: number; actual: number; remaining: number; utilizationPct: number | null }[];
  log: { id: string; name: string; category: string; period: string; month: number; limit: number; actual: number; remaining: number; utilizationPct: number; status: string }[];
}
export interface PmReport {
  stats: ReportStat[];
  rows: { vehicleId: string; vehicle: string; plate: string; trigger: string; interval: number; unit: string; consumedPct: number; reading: number | null;
    readingUnit: string; label: string; lastService: string | null; status: "ok" | "due" | "overdue"; preAlertPct: number | null; dispatchBlocked: boolean }[];
  tasks: { id: string; name: string; partCategory: string | null; trigger: string; interval: string; durationHours: number; estimatedCost: number; role: string }[];
  noSchedule: number;
}
export interface WorkOrderReport {
  stats: ReportStat[];
  technicians: { technician: string; assigned: number; closed: number; open: number; overdue: number; cost: number }[];
  types: { type: string; orders: number; tasks: number; sharePct: number; cost: number }[];
  register: { id: string; code: string; vehicle: string; plate: string; type: string; priority: string; technician: string | null; dueDate: string;
    ageDays: number | null; cost: number; status: string }[];
}
export interface CostReport {
  days: number;
  stats: ReportStat[];
  byVehicle: { vehicleId: string; vehicle: string; plate: string; expenses: number; fuel: number; workOrders: number; total: number; sharePct: number;
    distance: number | null; costPerKm: number | null }[];
  byCategory: { name: string; color: string; entries: number; total: number; sharePct: number; budget: number | null; variancePct: number | null }[];
}
export interface IssueReport {
  stats: ReportStat[];
  parts: { part: string; occurrences: number; vehicles: number; open: number; lastSeen: string; linkedCost: number; recurring: boolean }[];
  byVehicle: { vehicleId: string; vehicle: string; plate: string; issues: number; open: number }[];
  bySource: { source: string; issues: number; sharePct: number }[];
}
export interface UptimeReport {
  days: number;
  stats: ReportStat[];
  rows: { vehicleId: string; vehicle: string; plate: string; status: string; workOrders: number; downtimeDays: number; distance: number | null; uptimePct: number }[];
}
export interface DriverReport {
  days: number;
  stats: ReportStat[];
  rows: { driverId: string; driver: string; licenseClass: string; status: string; vehicle: string | null; plate: string | null; rating: number;
    licenseDays: number | null; expiredDocuments: number; assignments: number; vehicleSpend: number; score: number }[];
}
export interface OdometerReport {
  days: number;
  stats: ReportStat[];
  rows: { vehicleId: string; vehicle: string; plate: string; current: number | null; unit: string; readings: number; distance: number | null;
    avgPerDay: number | null; lastLogged: string | null; source: string | null; stale: boolean }[];
}
export interface ExpenseReport {
  stats: ReportStat[];
  months: { key: string; label: string; total: number; current: boolean }[];
  projection: number;
  byType: { name: string; color: string; entries: number; lifetime: number; last30: number; prior30: number; trendPct: number }[];
}
