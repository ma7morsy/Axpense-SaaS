# Axpense

B2B SaaS foundation for fleet, asset, equipment, maintenance, expenses and budgeting.

## Stack
- ASP.NET Core / C#
- Entity Framework Core
- PostgreSQL
- React + TypeScript
- REST API
- Docker-ready

## Phase 1
Foundation, tenant-aware domain model, authentication-ready structure, dashboard shell, and development Docker setup.

## Run
Prerequisites: .NET 10 SDK, Node.js 20+, Docker Desktop. Step-by-step: see `RUN-LOCAL.md`.

See `docs/PHASE-1.md`.

## Phase 2
Organization/user model, role field, tenant-scoped uniqueness, vehicle CRUD API, and Vehicles UI.

## Phase 3
Maintenance scheduling, completion workflow, maintenance API, and dashboard KPIs.

## Phase 4 — Expenses, Fuel & Budgets
- Expense tracking with categories, dates, vendors and vehicle links
- Fuel transaction tracking with quantity, unit price and calculated total
- Monthly budgets by category
- Dashboard monthly expense, fuel, maintenance and total-cost KPIs
- Tenant-scoped cost indexes and validation

## Phase 5 — Drivers & Assignments
- Driver management with license/status data
- Vehicle-to-driver assignments with start/end dates
- Assignment types: Primary, Temporary, Pool
- Tenant-scoped APIs and indexes
- Drivers and Assignments UI

Equipment & Assets intentionally skipped per product direction.

## Phase 7
Notifications & reminders: maintenance due alerts, driver license expiry reminders, unread/read notifications, and notification generation endpoint.

## Phase 8 — Authentication & SaaS Security
- JWT authentication with 12-hour sessions
- Secure PBKDF2 password hashing
- Organization registration and login
- Role claims and active-user validation
- Tenant authorization middleware
- Cross-organization query/body protection
- Protected application APIs
- React login/register experience
- Demo administrator: admin@axpense.local / Axpense123!

## Phase 9
Advanced maintenance and inspections: inspection records, checklist items, pass/fail results, failure reasons, image references, completion workflow, and maintenance workflow API foundations.


## Phase 10 — Production SaaS Layer
- Organization settings
- Audit logs
- Admin-only settings updates
- Tenant-scoped CSV vehicle export
- Production health endpoint
- Saas configuration APIs

## Phase 11 — Final SaaS UX & Administration
- Users & roles administration UI
- Admin user activation/deactivation
- Organization settings UI
- Audit log viewer
- Vehicle CSV export from settings
- Responsive administration experience
- Server-side tenant-scoped user administration


## Phase 12 — Final Integration & Production Hardening

- Global RFC 7807-style problem details for unhandled API errors
- API rate limiting (120 requests/minute per application instance)
- Response compression
- Dedicated readiness endpoint at `/ready`
- Health endpoint retained at `/health`
- Controllers protected by the API rate limiter
- Consolidated deployment/readiness notes

### Production checklist

1. Set a strong `Jwt:Key` through environment variables or a secret manager.
2. Set a production PostgreSQL connection string through environment configuration.
3. Disable Swagger outside controlled environments.
4. Put the API behind HTTPS and a reverse proxy/load balancer.
5. Configure backups and PostgreSQL point-in-time recovery.
6. Replace the demo admin credentials before production use.
7. Configure a real email/notification provider before enabling external notifications.
8. Run database migrations instead of relying on `EnsureCreatedAsync` for a production database.

## Drivers module (rebuilt)
Driver register, licence tracking, driver documents with scans, profile page and performance history. Assignments tab removed from the Drivers screen. See `docs/MODULE-DRIVERS.md`.

## Aerial view (live fleet map)
Live map under Vehicles with a per-vehicle status panel (speed, route, driver, today's distance/fuel/idle, maintenance due). Simulated telemetry behind `ITelematicsProvider`. Adds vehicle category/fuel type, service items and a first vehicle profile. See `docs/MODULE-AERIAL-VIEW.md`.

## Administration & Settings
Administration split into Company, Roles & Users and Settings. Settings → Parts catalogue (7 groups / 36 parts, admin CRUD, master-detail UI). See `docs/MODULE-SETTINGS.md`.

## Vehicles module

Fleet register (filters, PM gauge, dispatch-blocked flag), add/edit vehicle form, vehicle profile with Parts, Issues, Fuel, Kilometer records, Maintenance, Expenses, Inspections and Drivers tabs, hand-off, and renewal / maintenance alerts (profile banners + generated notifications). Details, rules and verification: [docs/MODULE-VEHICLES.md](docs/MODULE-VEHICLES.md).

> Schema changed (new vehicle columns, `VehiclePart`, `OdometerReading`, `VehicleIssue`; `VehicleServiceItem` removed). The app still uses `EnsureCreated`, so drop the development database once.

## Inspections module

Inspection templates built from the parts catalogue (Pass/Fail, gauge with expected range, scale 1–5, photo-only; critical checks; auto issue), runs with server-side drafts and photo evidence, and automatic follow-up: issues for failed checks, critical failure grounds the vehicle and schedules corrective maintenance. See [docs/MODULE-INSPECTIONS.md](docs/MODULE-INSPECTIONS.md).

> Schema changed again (templates, inspection snapshots) — drop the development database once.

## Maintenance module (work orders)

Work orders with tasks (part / task category, cost), technician, priority and status pipeline (Scheduled → In progress → Completed; Overdue derived), fleet-wide issues tab, PM engine run, and vehicle actions Assign / Hand off (with optional hand-off inspection) / Schedule maintenance. See [docs/MODULE-MAINTENANCE.md](docs/MODULE-MAINTENANCE.md).

> Schema changed (work-order fields, `MaintenanceTask`, built-in hand-off template) — drop the development database once.

## Company & Dashboard graphs

- **Administration → Company**: Company info (logo upload, identity, contact, regional settings), Subscription (plan, usage meters, plan cards with monthly/annual), Billing (card on file, billing contact, invoices with printable download). API: `/api/company/*`.
- **Dashboard**: date range, KPI cards with period comparison, cost trend, cost breakdown, fleet overview, notifications, upcoming maintenance, recent expenses, operating cost trend (stacked, previous-period ghost bars), budget vs actual, recurrence by part. API: `/api/dashboard/*`.
- Plan limits are enforced on vehicle and user creation. No payment gateway is integrated yet — see `docs/MODULE-COMPANY-DASHBOARD.md` for the rules and decisions to confirm.
- New tables: `Subscriptions`, `SubscriptionInvoices`; `OrganizationSettings` has new columns (recreate the dev database: `docker compose down -v && docker compose up -d db`).

## Sign in, register, onboarding & password reset

- New split-screen auth design with an English / العربية (RTL) toggle: `/login`, `/register`, `/forgot-password`, `/reset-password`.
- New workspaces go through a short onboarding wizard (`/onboarding`) for the company profile; it can be skipped.
- "Keep me signed in" on the login screen; clear, translated error messages (wrong password, account locked, email taken, expired link).
- No email service yet: the reset link is only shown in the Development environment. See `docs/MODULE-AUTH.md`.
- New column `OrganizationSettings.OnboardingCompletedAt` — recreate the dev database (`docker compose down -v && docker compose up -d db`).

## Reports, Budgets & PM engine

- Side menu: Fuel and Notifications removed (fuel log is in Reports; notifications stay on the top-bar bell). Arabic font is now Cairo.
- **Maintenance → Run PM engine** opens a preview (vehicle, trigger, state, task to generate, est. cost, result, dispatch-blocked banner) and generates the work orders from the **PM task library** (Settings → Maintenance).
- **Budgets**: add / edit / delete monthly limits (Overall or per expense type), period totals, and a per-budget summary (spend vs limit by day, breakdown, largest entries).
- **Reports**: Fuel log, Budget log, PM compliance, Work orders, Cost, Expenses & forecast, Issues, Vehicle uptime, Driver performance, Odometer — every table exports to CSV.
- New table `PmTasks`, new column `Budgets.ExpenseTypeId` — recreate the dev database (`docker compose down -v && docker compose up -d db`). See `docs/MODULE-REPORTS-BUDGETS-PM.md`.
