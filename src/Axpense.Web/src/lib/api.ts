import type {
  AssignmentRow,
  AuditLogEntry,
  AuthResponse,
  Budget,
  CurrentUser,
  DashboardSummary,
  PmTask,
  PmEnginePreview,
  BudgetList,
  BudgetDetail,
  BudgetOptions,
  BudgetRow,
  DashTrendPoint,
  OperatingTrend,
  BudgetVsActual,
  RecurrenceRow,
  CompanyOptions,
  CompanyProfile,
  SubscriptionInfo,
  BillingInfo,
  DriverDocument,
  DriverListResponse,
  DriverProfile,
  Expense,
  FleetTracking,
  FuelTransaction,
  InspectionListResponse,
  InspectionDetail,
  InspectionItem,
  InspectionCompleteResult,
  InspectionTemplate,
  InspectionOptions,
  WorkOrderOptions,
  WorkOrderListResponse,
  WorkOrderDetail,
  FleetIssue,
  VehicleHandOffResult,
  TrackedVehicleDetail,
  MaintenanceRow,
  NotificationItem,
  AnnualBudget,
  ExpenseType,
  PmEngine,
  TaskCategory,
  PartCategory,
  PartsCatalog,
  PresetPart,
  UserSummary,
  Vehicle,
  VehicleListResponse,
  VehicleDetail,
  VehicleOptions,
  VehiclePart,
  VehiclePartReplaceResult,
  VehicleIssue,
  FuelRecord,
  OdometerReadingRow,
  VehicleExpense,
  VehicleInspection,
  VehicleAssignment,
} from "./types";

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "";
const TOKEN_KEY = "axpense_token";
const ORG_KEY = "axpense_org";

/** "Keep me signed in" stores the token in localStorage; otherwise it lives only for this browser session. */
export function getToken() {
  return localStorage.getItem(TOKEN_KEY) ?? sessionStorage.getItem(TOKEN_KEY);
}
export function setToken(token: string, remember = true) {
  localStorage.removeItem(TOKEN_KEY);
  sessionStorage.removeItem(TOKEN_KEY);
  (remember ? localStorage : sessionStorage).setItem(TOKEN_KEY, token);
}
export function clearToken() {
  localStorage.removeItem(TOKEN_KEY);
  sessionStorage.removeItem(TOKEN_KEY);
}
export function getOrgId() {
  return localStorage.getItem(ORG_KEY) ?? "";
}
export function setOrgId(id: string) {
  localStorage.setItem(ORG_KEY, id);
}

export class ApiError extends Error {
  status: number;
  code?: string;
  /** Field-level messages from the standard error envelope, keyed by camelCase field name. */
  details: Record<string, string>;
  constructor(status: number, message: string, code?: string, details: Record<string, string> = {}) {
    super(message);
    this.status = status;
    this.code = code;
    this.details = details;
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers);
  const token = getToken();
  if (token) headers.set("Authorization", `Bearer ${token}`);
  if (init?.body && !(init.body instanceof FormData) && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");

  const res = await fetch(`${BASE_URL}${path}`, { ...init, headers });

  if (res.status === 401) {
    clearToken();
  }

  if (!res.ok) {
    let message = `Request failed (${res.status})`;
    let code: string | undefined;
    let details: Record<string, string> = {};
    try {
      const data = await res.clone().json();
      if (data?.error) {
        code = data.error.code;
        details = data.error.details ?? {};
      } else if (data && typeof data === "object") {
        // Auth endpoints answer with { success, code, message, fieldErrors }.
        code = data.code ?? undefined;
        details = data.fieldErrors ?? {};
      }
      message = data?.error?.message || data?.message || data?.title || (typeof data === "string" ? data : message);
    } catch {
      try {
        const text = await res.text();
        if (text) message = text;
      } catch {
        /* ignore */
      }
    }
    throw new ApiError(res.status, message, code, details);
  }

  if (res.status === 204) return undefined as T;
  const contentType = res.headers.get("Content-Type") || "";
  if (!contentType.includes("application/json")) return undefined as T;
  return (await res.json()) as T;
}

const get = <T>(path: string) => request<T>(path, { method: "GET" });
const post = <T>(path: string, body?: unknown) => request<T>(path, { method: "POST", body: body !== undefined ? JSON.stringify(body) : undefined });
const put = <T>(path: string, body?: unknown) => request<T>(path, { method: "PUT", body: body !== undefined ? JSON.stringify(body) : undefined });
const del = <T>(path: string) => request<T>(path, { method: "DELETE" });

function withOrg(path: string, params: Record<string, string | number | boolean | undefined> = {}) {
  const search = new URLSearchParams();
  search.set("organizationId", getOrgId());
  for (const [k, v] of Object.entries(params)) {
    if (v !== undefined && v !== "") search.set(k, String(v));
  }
  return `${path}?${search.toString()}`;
}

export const authApi = {
  login: (body: { userNameOrEmail: string; password: string }) => post<AuthResponse>("/api/auth/login", body),
  register: (body: {
    organizationName: string;
    firstName: string;
    lastName: string;
    email: string;
    userName?: string;
    password: string;
    confirmPassword: string;
    phoneNumber?: string;
    role?: string;
  }) => post<AuthResponse>("/api/auth/register", body),
  me: () => get<CurrentUser>("/api/auth/me"),
  forgotPassword: (email: string) => post<AuthResponse>("/api/auth/forgot-password", { email }),
  resetPassword: (body: { email: string; token: string; newPassword: string; confirmNewPassword: string }) =>
    post<AuthResponse>("/api/auth/reset-password", body),
};

export const dashboardApi = {
  summary: (from: string, to: string) => get<DashboardSummary>(`/api/dashboard?from=${from}&to=${to}`),
  costTrend: (months: number) => get<DashTrendPoint[]>(`/api/dashboard/cost-trend?months=${months}`),
  operatingTrend: (granularity: string) => get<OperatingTrend>(`/api/dashboard/operating-trend?granularity=${granularity}`),
  budget: (groupBy: string) => get<BudgetVsActual>(`/api/dashboard/budget?groupBy=${groupBy}`),
  recurrence: (take = 7) => get<{ rows: RecurrenceRow[]; totalParts: number }>(`/api/dashboard/recurrence?take=${take}`),
};

export const companyApi = {
  options: () => get<CompanyOptions>("/api/company/options"),
  profile: () => get<CompanyProfile>("/api/company/profile"),
  saveProfile: (body: Record<string, unknown>) => put<CompanyProfile>("/api/company/profile", body),
  uploadLogo: (file: File) => {
    const form = new FormData();
    form.append("file", file);
    return request<CompanyProfile>("/api/company/logo", { method: "POST", body: form });
  },
  removeLogo: () => del<CompanyProfile>("/api/company/logo"),
  /** Fetches the logo with the auth header; returns an object URL (caller revokes it). */
  logoUrl: async () => {
    const res = await fetch(`${BASE_URL}/api/company/logo`, { headers: { Authorization: `Bearer ${getToken() ?? ""}` } });
    if (!res.ok) throw new ApiError(res.status, "Logo unavailable");
    return URL.createObjectURL(await res.blob());
  },
  completeOnboarding: (body: Record<string, unknown>) => post<CompanyProfile>("/api/company/onboarding", body),
  skipOnboarding: () => post<CompanyProfile>("/api/company/onboarding/skip"),
  subscription: () => get<SubscriptionInfo>("/api/company/subscription"),
  changePlan: (planKey: string, billingCycle: string) => post<SubscriptionInfo>("/api/company/subscription/plan", { planKey, billingCycle }),
  setAutoRenew: (autoRenew: boolean) => put<SubscriptionInfo>("/api/company/subscription/auto-renew", { autoRenew }),
  cancel: () => post<SubscriptionInfo>("/api/company/subscription/cancel"),
  resume: () => post<SubscriptionInfo>("/api/company/subscription/resume"),
  billing: () => get<BillingInfo>("/api/company/billing"),
  /** Only brand, last 4 digits, expiry and holder are sent — never the card number or CVC. */
  setPaymentMethod: (body: { brand: string; last4: string; expiry: string; holder: string }) => put<BillingInfo>("/api/company/billing/payment-method", body),
  setBillingEmail: (email: string) => put<BillingInfo>("/api/company/billing/email", { email }),
  /** Opens the printable invoice in a new tab (fetched with the auth header). */
  openInvoice: async (id: string) => {
    const res = await fetch(`${BASE_URL}/api/company/billing/invoices/${id}/document`, { headers: { Authorization: `Bearer ${getToken() ?? ""}` } });
    if (!res.ok) throw new ApiError(res.status, "The invoice could not be opened.");
    const url = URL.createObjectURL(new Blob([await res.text()], { type: "text/html" }));
    window.open(url, "_blank", "noopener");
    setTimeout(() => URL.revokeObjectURL(url), 60_000);
  },
};

export interface VehicleListParams {
  q?: string;
  status?: string;
  category?: string;
  owner?: string;
  pm?: string;
}

type Body = Record<string, unknown>;
const vehicleRecords = <T, R = T>(name: string) => ({
  list: (id: string) => get<T[]>(`/api/vehicles/${id}/${name}`),
  add: (id: string, body: Body) => post<R>(`/api/vehicles/${id}/${name}`, body),
  remove: (id: string, rowId: string) => del<void>(`/api/vehicles/${id}/${name}/${rowId}`),
});

export const vehiclesApi = {
  options: () => get<VehicleOptions>("/api/vehicles/options"),
  search: (params: VehicleListParams = {}) => {
    const search = new URLSearchParams();
    for (const [k, v] of Object.entries(params)) if (v) search.set(k, v);
    const qs = search.toString();
    return get<VehicleListResponse>(`/api/vehicles${qs ? `?${qs}` : ""}`);
  },
  /** Every vehicle of the tenant — the picker source for other modules. */
  list: () => get<VehicleListResponse>("/api/vehicles").then((r) => r.items),
  get: (id: string) => get<VehicleDetail>(`/api/vehicles/${id}`),
  create: (body: Body) => post<VehicleDetail>("/api/vehicles", body),
  update: (id: string, body: Body) => put<VehicleDetail>(`/api/vehicles/${id}`, body),
  remove: (id: string) => del<void>(`/api/vehicles/${id}`),
  setStatus: (id: string, status: string) => put<VehicleDetail>(`/api/vehicles/${id}/status`, { status }),
  handOff: (id: string, body: { driverId: string | null; effectiveDate: string; note?: string | null; performInspection?: boolean; inspectorName?: string }) =>
    post<VehicleHandOffResult>(`/api/vehicles/${id}/handoff`, body),
  uploadPhoto: (id: string, file: File) => {
    const form = new FormData();
    form.set("file", file);
    return request<void>(`/api/vehicles/${id}/photo`, { method: "POST", body: form });
  },
  removePhoto: (id: string) => del<void>(`/api/vehicles/${id}/photo`),
  /** Fetches the photo with the auth header; returns an object URL (caller revokes it). */
  photoUrl: async (id: string) => {
    const res = await fetch(`${BASE_URL}/api/vehicles/${id}/photo`, { headers: { Authorization: `Bearer ${getToken() ?? ""}` } });
    if (!res.ok) throw new ApiError(res.status, "Photo unavailable");
    return URL.createObjectURL(await res.blob());
  },

  parts: {
    ...vehicleRecords<VehiclePart>("parts"),
    update: (id: string, partId: string, body: Body) => put<VehiclePart>(`/api/vehicles/${id}/parts/${partId}`, body),
    replace: (id: string, partId: string, body: Body) => post<VehiclePartReplaceResult>(`/api/vehicles/${id}/parts/${partId}/replace`, body),
  },
  issues: {
    ...vehicleRecords<VehicleIssue>("issues"),
    setStatus: (id: string, issueId: string, status: string) => put<VehicleIssue>(`/api/vehicles/${id}/issues/${issueId}/status`, { status }),
  },
  fuel: vehicleRecords<FuelRecord>("fuel"),
  readings: vehicleRecords<OdometerReadingRow>("readings"),
  expenses: {
    ...vehicleRecords<VehicleExpense>("expenses"),
    update: (id: string, expenseId: string, body: Body) => put<VehicleExpense>(`/api/vehicles/${id}/expenses/${expenseId}`, body),
  },
  inspections: (id: string) => get<VehicleInspection[]>(`/api/vehicles/${id}/inspections`),
  assignments: (id: string) => get<VehicleAssignment[]>(`/api/vehicles/${id}/assignments`),
};

export const trackingApi = {
  fleet: () => get<FleetTracking>("/api/tracking/vehicles"),
  vehicle: (id: string) => get<TrackedVehicleDetail>(`/api/tracking/vehicles/${id}`),
};

export const driversApi = {
  list: (params?: { q?: string; status?: string; page?: number; pageSize?: number }) =>
    get<DriverListResponse>(withOrg("/api/drivers", params)),
  get: (id: string) => get<DriverProfile>(`/api/drivers/${id}`),
  nextEmployeeNumber: () => get<{ employeeNumber: string }>("/api/drivers/next-employee-number"),
  create: (body: Record<string, unknown>) => post<DriverProfile>("/api/drivers", body),
  update: (id: string, body: Record<string, unknown>) => put<DriverProfile>(`/api/drivers/${id}`, body),
  remove: (id: string) => del<void>(`/api/drivers/${id}`),
  addDocument: (id: string, data: { name: string; expiryDate: string; file?: File | null }) => {
    const form = new FormData();
    form.set("name", data.name);
    form.set("expiryDate", data.expiryDate);
    if (data.file) form.set("file", data.file);
    return request<DriverDocument>(`/api/drivers/${id}/documents`, { method: "POST", body: form });
  },
  removeDocument: (id: string, documentId: string) => del<void>(`/api/drivers/${id}/documents/${documentId}`),
  /** Fetches the attached scan with the auth header and returns an object URL for viewing. */
  documentFileUrl: async (id: string, documentId: string) => {
    const res = await fetch(`${BASE_URL}/api/drivers/${id}/documents/${documentId}/file`, {
      headers: { Authorization: `Bearer ${getToken() ?? ""}` },
    });
    if (!res.ok) throw new ApiError(res.status, "The attached file could not be opened.");
    return URL.createObjectURL(await res.blob());
  },
};

export interface WorkOrderListParams {
  status?: string;
  type?: string;
  vehicleId?: string;
}

export const workOrdersApi = {
  options: () => get<WorkOrderOptions>("/api/work-orders/options"),
  list: (params: WorkOrderListParams = {}) => {
    const search = new URLSearchParams();
    for (const [k, v] of Object.entries(params)) if (v) search.set(k, v);
    const qs = search.toString();
    return get<WorkOrderListResponse>(`/api/work-orders${qs ? `?${qs}` : ""}`);
  },
  get: (id: string) => get<WorkOrderDetail>(`/api/work-orders/${id}`),
  create: (body: Record<string, unknown>) => post<WorkOrderDetail>("/api/work-orders", body),
  update: (id: string, body: Record<string, unknown>) => put<WorkOrderDetail>(`/api/work-orders/${id}`, body),
  setStatus: (id: string, status: string, odometer?: number | null) => put<WorkOrderDetail>(`/api/work-orders/${id}/status`, { status, odometer: odometer ?? null }),
  remove: (id: string) => del<void>(`/api/work-orders/${id}`),
  runPmEngine: (vehicleIds?: string[]) =>
    post<{ created: number; alreadyOpen: number; codes: string[] }>("/api/work-orders/run-pm-engine", { vehicleIds: vehicleIds ?? null }),
  pmPreview: () => get<PmEnginePreview>("/api/work-orders/pm-engine/preview"),
  issues: (status?: string) => get<FleetIssue[]>(`/api/issues${status ? `?status=${encodeURIComponent(status)}` : ""}`),
};

/** Dashboard feed: open work orders in the legacy row shape. */
export const maintenanceApi = {
  list: () =>
    workOrdersApi.list().then((r) =>
      r.items.map<MaintenanceRow>((w) => ({
        id: w.id,
        vehicleId: w.vehicleId,
        vehicle: `${w.vehicleName} · ${w.plateNumber}`,
        type: w.type,
        status: w.status,
        description: w.description,
        dueDate: w.scheduledDate,
        estimatedCost: w.total,
      }))
    ),
};

export const expensesApi = {
  list: (params?: { from?: string; to?: string }) => get<Expense[]>(withOrg("/api/expenses", params)),
  create: (body: Record<string, unknown>) => post<Expense>("/api/expenses", { ...body, organizationId: getOrgId() }),
  remove: (id: string) => del<void>(withOrg(`/api/expenses/${id}`)),
};

export const fuelApi = {
  list: () => get<FuelTransaction[]>(withOrg("/api/fuel")),
  create: (body: Record<string, unknown>) => post<FuelTransaction>("/api/fuel", { ...body, organizationId: getOrgId() }),
};

export const budgetsApi = {
  options: () => get<BudgetOptions>("/api/budgets/options"),
  list: (year: number, month?: number | null) => get<BudgetList>(`/api/budgets?year=${year}${month ? `&month=${month}` : ""}`),
  get: (id: string) => get<BudgetDetail>(`/api/budgets/${id}`),
  create: (body: Record<string, unknown>) => post<BudgetRow>("/api/budgets", body),
  update: (id: string, body: Record<string, unknown>) => put<BudgetRow>(`/api/budgets/${id}`, body),
  remove: (id: string) => del<void>(`/api/budgets/${id}`),
};

export const pmTasksApi = {
  list: () => get<PmTask[]>("/api/settings/pm-tasks"),
  create: (body: Record<string, unknown>) => post<PmTask>("/api/settings/pm-tasks", body),
  update: (id: string, body: Record<string, unknown>) => put<PmTask>(`/api/settings/pm-tasks/${id}`, body),
  remove: (id: string) => del<void>(`/api/settings/pm-tasks/${id}`),
};

/** Reports module — each report returns its own shape; see pages/reports. */
export const reportsApi = {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  get: <T = any>(report: string, params: Record<string, string | number | undefined> = {}) => {
    const q = Object.entries(params).filter(([, v]) => v !== undefined && v !== "").map(([k, v]) => `${k}=${encodeURIComponent(String(v))}`).join("&");
    return get<T>(`/api/reports/${report}${q ? `?${q}` : ""}`);
  },
};

export interface InspectionListParams {
  vehicleId?: string;
  templateId?: string;
  status?: string;
}

export const inspectionsApi = {
  list: (params: InspectionListParams = {}) => {
    const search = new URLSearchParams();
    for (const [k, v] of Object.entries(params)) if (v) search.set(k, v);
    const qs = search.toString();
    return get<InspectionListResponse>(`/api/inspections${qs ? `?${qs}` : ""}`);
  },
  get: (id: string) => get<InspectionDetail>(`/api/inspections/${id}`),
  start: (body: { vehicleId: string; templateId: string; odometer: number; inspectorName: string }) => post<InspectionDetail>("/api/inspections", body),
  answer: (id: string, itemId: string, body: { result: string | null; value: number | null; comment: string | null }) =>
    put<InspectionItem>(`/api/inspections/${id}/items/${itemId}`, body),
  uploadPhoto: (id: string, itemId: string, file: File) => {
    const form = new FormData();
    form.set("file", file);
    return request<InspectionItem>(`/api/inspections/${id}/items/${itemId}/photo`, { method: "POST", body: form });
  },
  removePhoto: (id: string, itemId: string) => del<InspectionItem>(`/api/inspections/${id}/items/${itemId}/photo`),
  /** Evidence photo through the authenticated API; returns an object URL (caller revokes it). */
  photoUrl: async (id: string, itemId: string) => {
    const res = await fetch(`${BASE_URL}/api/inspections/${id}/items/${itemId}/photo`, { headers: { Authorization: `Bearer ${getToken() ?? ""}` } });
    if (!res.ok) throw new ApiError(res.status, "Photo unavailable");
    return URL.createObjectURL(await res.blob());
  },
  complete: (id: string) => post<InspectionCompleteResult>(`/api/inspections/${id}/complete`, undefined),
  remove: (id: string) => del<void>(`/api/inspections/${id}`),
};

export const inspectionTemplatesApi = {
  options: () => get<InspectionOptions>("/api/inspection-templates/options"),
  list: () => get<InspectionTemplate[]>("/api/inspection-templates"),
  get: (id: string) => get<InspectionTemplate>(`/api/inspection-templates/${id}`),
  create: (body: Record<string, unknown>) => post<InspectionTemplate>("/api/inspection-templates", body),
  update: (id: string, body: Record<string, unknown>) => put<InspectionTemplate>(`/api/inspection-templates/${id}`, body),
  remove: (id: string) => del<void>(`/api/inspection-templates/${id}`),
};

export const assignmentsApi = {
  list: () => get<AssignmentRow[]>(withOrg("/api/assignments")),
  create: (body: Record<string, unknown>) => post<AssignmentRow>("/api/assignments", { ...body, organizationId: getOrgId() }),
  remove: (id: string) => del<void>(withOrg(`/api/assignments/${id}`)),
};

export const notificationsApi = {
  list: (unreadOnly?: boolean) => get<NotificationItem[]>(withOrg("/api/notifications", { unreadOnly })),
  markRead: (id: string) => post<NotificationItem>(`/api/notifications/${id}/read`, undefined),
  generate: () => post<{ created: number }>(withOrg("/api/notifications/generate"), undefined),
};

export const usersApi = {
  list: () => get<UserSummary[]>("/api/users"),
  create: (body: Record<string, unknown>) => post<{ id: string }>("/api/users", { ...body, organizationId: getOrgId() }),
  setStatus: (id: string, isActive: boolean) => put<{ id: string; isActive: boolean }>(`/api/users/${id}/status`, { isActive }),
};

export const settingsApi = {
  auditLog: (take = 100) => get<AuditLogEntry[]>(`/api/saas/audit?take=${take}`),
  exportVehiclesCsvUrl: () => `${BASE_URL}/api/saas/export/vehicles.csv`,
};

export const catalogApi = {
  get: () => get<PartsCatalog>("/api/settings/parts-catalogue"),
  createCategory: (body: { name: string; nameAr?: string | null }) =>
    post<PartCategory>("/api/settings/parts-catalogue/categories", body),
  updateCategory: (id: string, body: { name: string; nameAr?: string | null }) =>
    put<PartCategory>(`/api/settings/parts-catalogue/categories/${id}`, body),
  removeCategory: (id: string) => del<void>(`/api/settings/parts-catalogue/categories/${id}`),
  createPart: (categoryId: string, body: { name: string; nameAr?: string | null }) =>
    post<PresetPart>(`/api/settings/parts-catalogue/categories/${categoryId}/parts`, body),
  updatePart: (id: string, body: { name: string; nameAr?: string | null }) =>
    put<PresetPart>(`/api/settings/parts-catalogue/parts/${id}`, body),
  removePart: (id: string) => del<void>(`/api/settings/parts-catalogue/parts/${id}`),
};

export const expenseTypesApi = {
  list: () => get<ExpenseType[]>("/api/settings/expense-types"),
  create: (body: { name: string; color?: string | null }) => post<ExpenseType>("/api/settings/expense-types", body),
  update: (id: string, body: { name: string; color?: string | null }) => put<ExpenseType>(`/api/settings/expense-types/${id}`, body),
  remove: (id: string) => del<void>(`/api/settings/expense-types/${id}`),
};

export const taskCategoriesApi = {
  list: () => get<TaskCategory[]>("/api/settings/task-categories"),
  create: (body: { name: string }) => post<TaskCategory>("/api/settings/task-categories", body),
  update: (id: string, body: { name: string }) => put<TaskCategory>(`/api/settings/task-categories/${id}`, body),
  remove: (id: string) => del<void>(`/api/settings/task-categories/${id}`),
};

export const pmEngineApi = {
  get: () => get<PmEngine>("/api/settings/pm-engine"),
  save: (body: Omit<PmEngine, "roles">) => put<PmEngine>("/api/settings/pm-engine", body),
};

export const budgetApi = {
  get: (year: number) => get<AnnualBudget>(`/api/settings/budget/${year}`),
  setAnnual: (year: number, amount: number) => put<AnnualBudget>(`/api/settings/budget/${year}/annual`, { amount }),
  setMonth: (year: number, month: number, amount: number) => put<AnnualBudget>(`/api/settings/budget/${year}/months/${month}`, { amount }),
  splitEvenly: (year: number) => post<AnnualBudget>(`/api/settings/budget/${year}/months/split-evenly`),
  weightByLastYear: (year: number) => post<AnnualBudget>(`/api/settings/budget/${year}/months/weight-by-last-year`),
  setShare: (year: number, expenseTypeId: string, sharePercent: number) =>
    put<AnnualBudget>(`/api/settings/budget/${year}/categories/${expenseTypeId}`, { sharePercent }),
  distributeFromLastYear: (year: number) => post<AnnualBudget>(`/api/settings/budget/${year}/categories/distribute-from-last-year`),
};
