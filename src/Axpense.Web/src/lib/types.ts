export interface AuthResponse {
  success: boolean;
  message: string;
  errors?: string[];
  userId?: string;
  userName?: string;
  email?: string;
  role?: string;
  organizationId?: string;
  organizationName?: string;
  token?: string;
  expiresOn?: string;
  resetToken?: string;
  code?: string;
  fieldErrors?: Record<string, string>;
}

export interface CurrentUser {
  id: string;
  userName: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string | null;
  role: string;
  organizationId: string;
  organizationName?: string;
  /** Owner/Admin of a new workspace that has not finished or skipped the onboarding wizard. */
  onboardingRequired?: boolean;
}

/** Row of the vehicle register (GET /api/vehicles). Other pages use it as the vehicle picker source. */
export interface Vehicle {
  id: string;
  name: string;
  make: string;
  model: string;
  modelYear: number;
  category: string;
  ownerType: string;
  ownerName?: string | null;
  vin?: string | null;
  plateNumber: string;
  status: string;
  fuelType: string;
  currentOdometer?: number | null;
  readingUnit: string;
  hasPhoto: boolean;
  pm: PmState;
  dispatchBlocked: boolean;
  currentDriver?: VehicleDriver | null;
  alertCount: number;
}

export interface VehicleListResponse {
  items: Vehicle[];
  total: number;
}

export interface PmState {
  status: "none" | "ok" | "due" | "overdue";
  percent: number;
  label: string;
  detail?: string | null;
  unit: string;
  preAlertPercent?: number | null;
  trigger: string;
  daysOverdue?: number | null;
}

export interface VehicleDriver {
  id: string;
  fullName: string;
  licenseClass: string;
  licenseNumber: string;
  rating: number;
  phone: string;
  since?: string | null;
}

export interface VehicleAlert {
  kind: "pm" | "insurance" | "registration" | "part" | "warranty";
  severity: "critical" | "warning" | "info";
  title: string;
  detail: string;
  action?: "schedule" | "renew" | "raise" | "replace" | "claim" | null;
  dueDate?: string | null;
  key: string;
}

export interface VehicleTabCounts {
  parts: number;
  issues: number;
  fuel: number;
  odometer: number;
  maintenance: number;
  expenses: number;
  inspections: number;
  drivers: number;
}

export interface VehicleDetail {
  id: string;
  name: string;
  make: string;
  model: string;
  modelYear: number;
  category: string;
  ownerType: string;
  ownerName?: string | null;
  vin?: string | null;
  plateNumber: string;
  status: string;
  currentOdometer?: number | null;
  readingUnit: string;
  engineType: string;
  fuelType: string;
  tankCapacity?: number | null;
  tankUnit: string;
  pmTrigger: string;
  pmIntervalKm?: number | null;
  pmIntervalDays?: number | null;
  pmIntervalHours?: number | null;
  pmLastServiceReading?: number | null;
  pmLastServiceDate?: string | null;
  purchaseDate?: string | null;
  purchasePrice?: number | null;
  supplier?: string | null;
  warrantyUntil?: string | null;
  expectedResidualValue?: number | null;
  insuranceProvider?: string | null;
  insurancePolicyNumber?: string | null;
  insuranceRenewalDate?: string | null;
  insuranceAnnualPremium?: number | null;
  registrationAuthority?: string | null;
  registrationNumber?: string | null;
  registrationRenewalDate?: string | null;
  notes?: string | null;
  hasPhoto: boolean;
  pm: PmState;
  dispatchBlocked: boolean;
  escalatedTo?: string | null;
  insuranceDaysLeft?: number | null;
  registrationDaysLeft?: number | null;
  warrantyDaysLeft?: number | null;
  lifetimeSpend: number;
  currentDriver?: VehicleDriver | null;
  alerts: VehicleAlert[];
  counts: VehicleTabCounts;
}

export interface VehicleOptions {
  statuses: string[];
  categories: string[];
  ownerTypes: string[];
  readingUnits: string[];
  engineTypes: string[];
  fuelTypes: string[];
  pmTriggers: string[];
  partStatuses: string[];
  issuePriorities: string[];
  issueStatuses: string[];
  issueSources: string[];
  readingSources: string[];
  maintenanceTypes: string[];
  renewalReminderDays: number;
}

export interface WarrantyState {
  covered: boolean;
  state: "covered" | "expired" | "none";
  label: string;
  endingSoon: boolean;
  daysLeft?: number | null;
  kmLeft?: number | null;
}

export interface PartWear {
  status: "ok" | "due" | "expired" | "retired" | "untracked";
  percent: number;
  label: string;
  detail?: string | null;
  distanceRun: number;
  daysRun: number;
  warranty: WarrantyState;
  claimWarranty: boolean;
}

export interface VehiclePart {
  id: string;
  code: string;
  presetPartId: string;
  partName: string;
  partNameAr?: string | null;
  partCode: string;
  categoryId: string;
  categoryName: string;
  serial?: string | null;
  unitCost?: number | null;
  lifespanKm?: number | null;
  lifespanMonths?: number | null;
  installedReading: number;
  installedDate: string;
  warrantyUntil?: string | null;
  warrantyKm?: number | null;
  status: string;
  retiredDate?: string | null;
  notes?: string | null;
  wear: PartWear;
}

export interface VehiclePartReplaceResult {
  retired: VehiclePart;
  fitted: VehiclePart;
  warrantyClaimed: boolean;
  maintenanceId?: string | null;
  expenseId?: string | null;
}

export interface VehicleIssue {
  id: string;
  code: string;
  title: string;
  note?: string | null;
  presetPartId?: string | null;
  partName?: string | null;
  priority: string;
  source: string;
  status: string;
  reportedDate: string;
  resolvedDate?: string | null;
}

export interface FuelRecord {
  id: string;
  date: string;
  station?: string | null;
  quantity: number;
  unit: string;
  unitPrice: number;
  totalAmount: number;
  odometer?: number | null;
  consumptionPer100?: number | null;
}

export interface OdometerReadingRow {
  id: string;
  date: string;
  value: number;
  distanceSincePrevious?: number | null;
  source: string;
  recordedBy?: string | null;
}


export interface VehicleExpense {
  id: string;
  date: string;
  title: string;
  expenseTypeId?: string | null;
  typeName: string;
  typeColor?: string | null;
  note?: string | null;
  amount: number;
}

export interface VehicleInspection {
  id: string;
  code: string;
  templateName: string;
  date: string;
  odometer?: number | null;
  inspectorName?: string | null;
  status: string;
  total: number;
  passed: number;
  failed: number;
  notApplicable: number;
}

export interface VehicleAssignment {
  id: string;
  driverId: string;
  driverName: string;
  licenseNumber?: string | null;
  from: string;
  to?: string | null;
  note?: string | null;
  current: boolean;
}

export type Motion = "moving" | "idle" | "parked" | "workshop";

export interface TrackedVehicle {
  id: string;
  name: string;
  plateNumber: string;
  category: string;
  fuelType: string;
  motion: Motion;
  speedKmh: number;
  latitude: number;
  longitude: number;
  headingDegrees: number;
  driverId?: string | null;
  driverName?: string | null;
}

export interface FleetTracking {
  items: TrackedVehicle[];
  counts: { all: number; moving: number; idle: number; parked: number; workshop: number };
  simulated: boolean;
  updatedAtUtc: string;
}

export interface MaintenanceDueItem {
  id: string;
  name: string;
  usedPercent: number;
  status: "ok" | "due" | "overdue" | "unknown";
  label: string;
}

export interface TrackedVehicleDetail {
  id: string;
  name: string;
  plateNumber: string;
  category: string;
  fuelType: string;
  motion: Motion;
  speedKmh: number;
  speedLimitKmh: number;
  engineOn: boolean;
  distanceTodayKm: number;
  fuelUsedToday: number;
  fuelUnit: "L" | "kWh";
  idleMinutesToday: number;
  odometerKm?: number | null;
  address: string;
  latitude: number;
  longitude: number;
  updatedAtUtc: string;
  trip?: {
    origin: string;
    destination: string;
    progressPercent: number;
    remainingKm: number;
    etaUtc: string;
    path: [number, number][];
  } | null;
  lastTrip?: string | null;
  driver?: { id: string; fullName: string; licenseClass: string; rating: number; phone: string } | null;
  maintenanceDue: MaintenanceDueItem[];
  simulated: boolean;
}

export const DRIVER_STATUSES = ["Active", "On leave", "Suspended"] as const;
export const LICENSE_CLASSES = ["B — Light vehicle", "C — Heavy goods", "D — Passenger transport", "Heavy equipment operator"] as const;

export interface VehicleRef {
  id: string;
  name: string;
  plateNumber: string;
}

export interface DriverListItem {
  id: string;
  fullName: string;
  employeeNumber: string;
  licenseClass: string;
  licenseNumber?: string | null;
  licenseExpiryDate?: string | null;
  licenseDaysLeft?: number | null;
  assignedVehicle?: VehicleRef | null;
  documentCount: number;
  expiringDocumentCount: number;
  rating: number;
  status: string;
}

export interface DriverListResponse {
  items: DriverListItem[];
  filteredCount: number;
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface DriverDocument {
  id: string;
  name: string;
  expiryDate: string;
  daysLeft: number;
  state: "Valid" | "Expiring" | "Expired";
  hasFile: boolean;
  fileName?: string | null;
  contentType?: string | null;
}

export interface DriverTimelineEvent {
  date: string;
  kind: "assignment" | "fuel";
  isCurrent: boolean;
  vehicleName?: string | null;
  note?: string | null;
  endDate?: string | null;
  station?: string | null;
  liters?: number | null;
  amount?: number | null;
}

export interface DriverProfile {
  id: string;
  fullName: string;
  employeeNumber: string;
  status: string;
  phone: string;
  email?: string | null;
  nationalId?: string | null;
  hireDate?: string | null;
  licenseNumber?: string | null;
  licenseClass: string;
  licenseIssuedDate?: string | null;
  licenseExpiryDate?: string | null;
  licenseDaysLeft?: number | null;
  rating: number;
  notes?: string | null;
  currentVehicle?: VehicleRef | null;
  stats: {
    rating: number;
    inspectionsRun: number;
    failedItemsFound: number;
    issuesReported: number;
    issuesOpen: number;
  };
  documents: DriverDocument[];
  timeline: DriverTimelineEvent[];
}

export interface DriverUpsert {
  fullName: string;
  employeeNumber: string;
  status: string;
  phone: string;
  email: string;
  nationalId: string;
  hireDate: string;
  licenseNumber: string;
  licenseClass: string;
  licenseIssuedDate: string;
  licenseExpiryDate: string;
  rating: string;
}

export interface MaintenanceRow {
  id: string;
  vehicleId: string;
  vehicle: string | null;
  type: string;
  status: string;
  description: string;
  dueDate: string;
  estimatedCost?: number | null;
  actualCost?: number | null;
  odometerAtService?: number | null;
  completedAtUtc?: string | null;
}

export interface Expense {
  id: string;
  organizationId: string;
  vehicleId?: string | null;
  category: string;
  description: string;
  amount: number;
  expenseDate: string;
  vendor?: string | null;
  referenceNumber?: string | null;
  receiptUrl?: string | null;
  notes?: string | null;
}

export interface FuelTransaction {
  id: string;
  organizationId: string;
  vehicleId: string;
  transactionDate: string;
  quantityLiters: number;
  unitPrice: number;
  totalAmount: number;
  odometer?: number | null;
  fuelType?: string | null;
  station?: string | null;
}

export interface Budget {
  id: string;
  organizationId: string;
  name: string;
  category: string;
  year: number;
  month: number;
  limitAmount: number;
  notes?: string | null;
}

export interface AssignmentRow {
  id: string;
  vehicleId: string;
  driverId: string;
  startDate: string;
  endDate?: string | null;
  assignmentType: string;
  notes?: string | null;
  vehicle: string | null;
  driver: string | null;
}

export interface TemplateItem {
  id: string;
  sort: number;
  label: string;
  presetPartId: string;
  partName: string;
  partNameAr?: string | null;
  categoryId: string;
  fieldType: "passfail" | "gauge" | "scale" | "photo";
  fieldTypeLabel: string;
  critical: boolean;
  unit?: string | null;
  min?: number | null;
  max?: number | null;
  issueOnFail: boolean;
}

export interface InspectionTemplate {
  id: string;
  code: string;
  name: string;
  scope: string;
  active: boolean;
  checkCount: number;
  criticalCount: number;
  timesRun: number;
  isSystem: boolean;
  items: TemplateItem[];
}

export interface InspectionOptions {
  scopes: string[];
  fieldTypes: { value: string; label: string }[];
}

export interface InspectionSummary {
  total: number;
  answered: number;
  passed: number;
  failed: number;
  notApplicable: number;
}

export interface InspectionRow {
  id: string;
  code: string;
  vehicleId: string;
  vehicleName: string;
  plateNumber: string;
  templateId?: string | null;
  templateName: string;
  date: string;
  odometer?: number | null;
  inspectorName?: string | null;
  status: string;
  summary: InspectionSummary;
}

export interface InspectionStats {
  completed: number;
  failedItems: number;
  templates: number;
  activeTemplates: number;
  vehiclesNeverInspected: number;
  fleetSize: number;
  inProgress: number;
}

export interface InspectionListResponse {
  items: InspectionRow[];
  stats: InspectionStats;
}

export interface InspectionItem {
  id: string;
  sort: number;
  label: string;
  presetPartId?: string | null;
  partName?: string | null;
  fieldType: "passfail" | "gauge" | "scale" | "photo";
  fieldTypeLabel: string;
  critical: boolean;
  unit?: string | null;
  min?: number | null;
  max?: number | null;
  issueOnFail: boolean;
  result?: "pass" | "fail" | "na" | null;
  value?: number | null;
  comment?: string | null;
  hasPhoto: boolean;
  issueId?: string | null;
  problem?: string | null;
}

export interface InspectionDetail {
  id: string;
  code: string;
  vehicleId: string;
  vehicleName: string;
  plateNumber: string;
  readingUnit: string;
  templateId?: string | null;
  templateName: string;
  date: string;
  odometer?: number | null;
  inspectorName?: string | null;
  status: string;
  completedAtUtc?: string | null;
  summary: InspectionSummary;
  criticalFailed: boolean;
  canComplete: boolean;
  items: InspectionItem[];
}

export interface InspectionCompleteResult {
  inspection: InspectionDetail;
  issuesRaised: number;
  vehicleGrounded: boolean;
  maintenanceId?: string | null;
}

export interface NotificationItem {
  id: string;
  organizationId: string;
  type: string;
  title: string;
  message: string;
  severity: string;
  isRead: boolean;
  dueDate?: string | null;
  createdAt: string;
}

export interface UserSummary {
  id: string;
  userName: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string | null;
  role: string;
  isRootSuperAdmin: boolean;
  createdAt: string;
}

export interface OrganizationSettings {
  id?: string;
  organizationId?: string;
  currency: string;
  timeZone: string;
  dateFormat: string;
  companyName: string;
  logoUrl?: string | null;
  emailNotifications: boolean;
  maintenanceReminders: boolean;
  licenseReminders: boolean;
}

export interface AuditLogEntry {
  id: string;
  organizationId: string;
  userId?: string | null;
  action: string;
  entityType: string;
  entityId?: string | null;
  details?: string | null;
  createdAt: string;
}

export interface CostTrendPoint {
  month: string;
  fuel: number;
  maintenance: number;
  expenses: number;
}

export interface DashboardStats {
  totalVehicles: number;
  activeVehicles: number;
  maintenanceDueWithin30Days: number;
  openWorkOrders: number;
  monthlyExpenses: number;
  monthlyFuel: number;
  monthlyMaintenance: number;
  monthlyTotalCost: number;
  trend: {
    expensesPct: number | null;
    fuelPct: number | null;
    maintenancePct: number | null;
    totalPct: number | null;
  };
  costTrend: CostTrendPoint[];
}

export interface PresetPart {
  id: string;
  categoryId: string;
  code: string;
  name: string;
  nameAr?: string | null;
}

export interface PartCategory {
  id: string;
  code: string;
  number: number;
  name: string;
  nameAr?: string | null;
  parts: PresetPart[];
}

export interface PartsCatalog {
  categories: PartCategory[];
  totalParts: number;
}

export interface ExpenseType {
  id: string;
  name: string;
  color: string;
  isSystem: boolean;
  entries: number;
  total: number;
}

export interface TaskCategory {
  id: string;
  name: string;
  taskCount: number;
}

export interface EscalationStep {
  afterDaysOverdue: number;
  role: string;
}

export interface PmEngine {
  distancePreAlertKm: number;
  timePreAlertDays: number;
  engineHourPreAlert: number;
  autoGenerateWorkOrders: boolean;
  blockDispatchOnCriticalOverdue: boolean;
  escalationSteps: EscalationStep[];
  roles: string[];
}

export interface BudgetMonth {
  month: number;
  amount: number;
  sharePercent: number;
  actual: number;
  utilizationPercent?: number | null;
}

export interface BudgetCategory {
  expenseTypeId: string;
  name: string;
  color: string;
  sharePercent: number;
  budget: number;
  actual: number;
  remaining: number;
  utilizationPercent?: number | null;
}

export interface AnnualBudget {
  year: number;
  exists: boolean;
  amount: number;
  suggestedAmount: number;
  allocatedTotal: number;
  unallocated: number;
  actualTotal: number;
  sharePercentTotal: number;
  currentMonth?: number | null;
  hasPriorYearSpend: boolean;
  months: BudgetMonth[];
  categories: BudgetCategory[];
}

// ---------------------------------------------------------------- work orders (maintenance)

export interface WorkOrderTask {
  id: string;
  sort: number;
  description: string;
  partCategoryId?: string | null;
  partCategoryName?: string | null;
  taskCategoryId?: string | null;
  taskCategoryName?: string | null;
  cost: number;
}

export interface WorkOrderRow {
  id: string;
  code: string;
  vehicleId: string;
  vehicleName: string;
  plateNumber: string;
  type: string;
  priority: string;
  status: string;
  displayStatus: string;
  daysOverdue?: number | null;
  scheduledDate: string;
  technicianName?: string | null;
  total: number;
  taskCount: number;
  description: string;
  source: string;
  sourceRef?: string | null;
}

export interface WorkOrderDetail extends Omit<WorkOrderRow, "taskCount"> {
  readingUnit: string;
  nextStatus?: string | null;
  technicianUserId?: string | null;
  notes?: string | null;
  issueId?: string | null;
  inspectionId?: string | null;
  odometerAtRaise?: number | null;
  odometerAtService?: number | null;
  actualCost?: number | null;
  startedAtUtc?: string | null;
  completedAtUtc?: string | null;
  tasks: WorkOrderTask[];
}

export interface WorkOrderStats {
  scheduledNext30Days: number;
  inProgress: number;
  overdue: number;
  overdueEscalatedTo?: string | null;
  committedCost: number;
  openIssues: number;
}

export interface WorkOrderListResponse {
  items: WorkOrderRow[];
  total: number;
  stats: WorkOrderStats;
}

export interface NamedOption {
  id: string;
  name: string;
}

export interface WorkOrderOptions {
  types: string[];
  priorities: string[];
  statuses: string[];
  filterStatuses: string[];
  technicians: NamedOption[];
  partCategories: NamedOption[];
  taskCategories: NamedOption[];
}

export interface FleetIssue {
  id: string;
  code: string;
  vehicleId: string;
  vehicleName: string;
  plateNumber: string;
  title: string;
  note?: string | null;
  partName?: string | null;
  priority: string;
  source: string;
  status: string;
  reportedDate: string;
  workOrderId?: string | null;
  workOrderCode?: string | null;
}

export interface VehicleHandOffResult {
  vehicle: VehicleDetail;
  inspectionId?: string | null;
  inspectionMessage?: string | null;
}

// ---------------------------------------------------------------- Company
export interface ValueLabel {
  value: string;
  label: string;
}
export interface CompanyOptions {
  industries: string[];
  sizes: string[];
  timeZones: ValueLabel[];
  currencies: ValueLabel[];
  dateFormats: string[];
  distanceUnits: ValueLabel[];
  months: ValueLabel[];
}
export interface CompanyProfile {
  companyName: string;
  legalName: string | null;
  industry: string | null;
  companySize: string | null;
  commercialRegistrationNo: string | null;
  taxId: string | null;
  foundedYear: number | null;
  hasLogo: boolean;
  logoVersion: string | null;
  address: string | null;
  city: string | null;
  country: string | null;
  phone: string | null;
  email: string | null;
  website: string | null;
  timeZone: string;
  currency: string;
  fiscalYearStartMonth: number;
  dateFormat: string;
  distanceUnit: string;
  updatedAt: string | null;
}
export interface PlanInfo {
  key: string;
  name: string;
  monthly: number;
  annual: number;
  vehicles: number | null;
  seats: number | null;
  storageGb: number;
  popular: boolean;
  features: string[];
}
export interface SubscriptionInfo {
  planKey: string;
  planName: string;
  billingCycle: "monthly" | "annual";
  status: "Active" | "Cancelling" | "Cancelled";
  autoRenew: boolean;
  price: number;
  currency: string;
  customerSince: string;
  currentPeriodStart: string;
  currentPeriodEnd: string;
  daysLeft: number;
  usage: { vehicles: number; vehicleLimit: number | null; seats: number; seatLimit: number | null; storageBytes: number; storageLimitBytes: number };
  plans: PlanInfo[];
}
export interface InvoiceRow {
  id: string;
  code: string;
  issueDate: string;
  description: string;
  amount: number;
  currency: string;
  status: string;
  periodStart: string;
  periodEnd: string;
}
export interface BillingInfo {
  cardBrand: string | null;
  cardLast4: string | null;
  cardExpiry: string | null;
  cardHolder: string | null;
  billingEmail: string | null;
  invoices: InvoiceRow[];
}

// ---------------------------------------------------------------- Dashboard
export interface Kpi {
  value: number;
  previous: number;
  changePct: number | null;
}
export interface CostGroup {
  key: "fuel" | "maintenance" | "expenses" | "insurance";
  name: string;
  amount: number;
  previous: number;
  percent: number;
}
export interface UpcomingMaintenance {
  id: string | null;
  code: string | null;
  vehicleId: string;
  plate: string;
  vehicle: string;
  task: string;
  type: string;
  dueDate: string | null;
  days: number | null;
  label: string | null;
  late: boolean;
  status: string;
  priority: string;
}
export interface RecentExpense {
  kind: "expense" | "fuel" | "maintenance";
  category: string;
  vehicleId: string | null;
  plate: string | null;
  vendor: string | null;
  amount: number;
  date: string;
}
export interface DashboardSummary {
  from: string;
  to: string;
  previousFrom: string;
  previousTo: string;
  currency: string;
  kpis: {
    vehicles: Kpi;
    maintenanceDue: { due: number; overdue: number; inProgress: number };
    fuel: Kpi;
    maintenance: Kpi;
    expenses: Kpi;
    insurance: Kpi;
    total: Kpi;
  };
  groups: CostGroup[];
  upcoming: UpcomingMaintenance[];
  recentExpenses: RecentExpense[];
}
export interface DashTrendPoint {
  key: string;
  label: string;
  full: string;
  fuel: number;
  maintenance: number;
  expenses: number;
}
export interface OperatingBucket {
  key: string;
  label: string;
  full: string;
  start: string;
  end: string;
  values: Record<string, number>;
  total: number;
  previousTotal: number;
}
export interface OperatingTrend {
  granularity: "month" | "week" | "day";
  series: { id: string; name: string; color: string }[];
  buckets: OperatingBucket[];
  total: number;
  previousTotal: number;
  changePct: number | null;
}
export interface BudgetRow {
  key: string;
  label: string;
  budget: number;
  actual: number;
  remaining: number;
  variancePct: number | null;
  over: boolean;
}
export interface BudgetVsActual {
  groupBy: "month" | "category";
  period: string;
  hasBudget: boolean;
  budget: number;
  actual: number;
  remaining: number;
  utilizationPct: number | null;
  rows: BudgetRow[];
}
export interface RecurrenceRow {
  partId: string | null;
  part: string;
  category: string | null;
  times: number;
  vehicles: number;
  open: number;
  last: string;
  recurring: boolean;
}

// ---------------------------------------------------------------- PM task library / engine preview
export interface PmTask {
  id: string;
  name: string;
  partCategoryId: string | null;
  partCategoryName: string | null;
  taskCategoryId: string | null;
  taskCategoryName: string | null;
  trigger: "usage" | "time" | "hours" | "hybrid";
  intervalKm: number | null;
  intervalDays: number | null;
  intervalHours: number | null;
  durationHours: number;
  estimatedCost: number;
  role: string;
  isActive: boolean;
  sortOrder: number;
}
export interface PmPreviewRow {
  vehicleId: string;
  vehicleName: string;
  plateNumber: string;
  trigger: string;
  leadKind: string;
  interval: number;
  unit: string;
  status: "due" | "overdue";
  label: string;
  taskId: string | null;
  taskName: string;
  role: string;
  durationHours: number | null;
  estimatedCost: number;
  openOrderId: string | null;
  openOrderCode: string | null;
  willCreate: boolean;
  dispatchBlocked: boolean;
}
export interface PmEnginePreview {
  distancePreAlertKm: number;
  timePreAlertDays: number;
  engineHourPreAlert: number;
  autoWorkOrders: boolean;
  blockDispatch: boolean;
  rows: PmPreviewRow[];
  toCreate: number;
  dispatchBlocked: number;
  estimatedTotal: number;
}

// ---------------------------------------------------------------- Budgets (monthly limits)
export interface BudgetRow {
  id: string;
  name: string;
  category: string;
  expenseTypeId: string | null;
  color: string | null;
  year: number;
  month: number;
  period: string;
  limitAmount: number;
  actual: number;
  remaining: number;
  utilizationPct: number;
  status: "On track" | "At risk" | "Over";
  projected: number | null;
  notes: string | null;
}
export interface BudgetList {
  year: number;
  month: number | null;
  totals: { budgeted: number; actual: number; remaining: number; utilizationPct: number | null; count: number; over: number; atRisk: number; basis: string };
  rows: BudgetRow[];
}
export interface BudgetDetail {
  budget: BudgetRow;
  breakdown: { name: string; color: string; amount: number; percent: number }[];
  daily: { date: string; amount: number; cumulative: number }[];
  topEntries: { date: string; kind: string; description: string; plate: string | null; amount: number }[];
  daysInMonth: number;
  daysElapsed: number;
  dailyAllowance: number;
}
export interface BudgetOptions {
  categories: { value: string; label: string; color: string | null }[];
  minYear: number;
  maxYear: number;
}

// ---------------------------------------------------------------- Reports
export interface ReportStat {
  label: string;
  value: string;
  sub: string;
  tone: "" | "ok" | "warn" | "bad" | "neutral";
}
