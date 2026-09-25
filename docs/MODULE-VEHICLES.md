# Vehicles module

Fleet register, vehicle profile with one tab per record type, and renewal / maintenance notifications.

## Screens

| Screen | Route | What it does |
|---|---|---|
| Vehicle register | `/vehicles` | Table: vehicle (photo or category art), VIN / plate, status (+ *Dispatch blocked*, alert count), odometer + PM gauge, assigned driver, row actions. Filters: text, Status, Category, Owner, PM state. |
| Add / edit vehicle | modal | Sections: Identity · Usage & power · Preventive maintenance criteria · Purchase · Insurance · Licence & registration · Photo. |
| Vehicle profile | `/vehicles/:id?tab=` | Header actions (Hand off, Inspect, Schedule maintenance, Edit), banners, vehicle card (photo, plate, status select, driver + Reassign, maintenance runway, details, purchase, insurance, registration, lifetime spend), tabs: Parts, Issues, Fuel records, Kilometer records, Maintenance, Expenses, Inspections, Drivers. |
| Hand off / assign | modal | New driver (optional on hand-off), effective date, note. |

Every screen has loading, empty, error (with retry) and not-found / no-access states. Allowed values (statuses, categories, fuel types, triggers…) come from `GET /api/vehicles/options` — nothing is hard-coded in the UI.

## Business rules (backend — `Axpense.Service/Vehicles`)

**Register**
- VIN: 17 characters, letters and digits without I/O/Q, stored upper-case, unique per organization (409 `VIN_EXISTS`).
- Plate: normalised (upper-case, single spaces), unique per organization (409 `PLATE_EXISTS`).
- Year 1980 … next year. Owner name is required for *Client* / *Provider* vehicles. An electric engine must use the Electric fuel type.
- PM trigger decides the required intervals: usage → distance (or engine hours when the unit is hours); time → days; hours → engine hours and reading unit = hours; hybrid → days + distance/hours.
- Last-service reading ≤ current odometer, dates not in the future, residual ≤ purchase cost, warranty not before purchase.
- Last-service reading/date default to the current odometer / today, so a new vehicle starts a full runway.

**Odometer**
- Every change is a reading (`OdometerReading`). Registering a vehicle logs "Vehicle registered"; editing the odometer logs "Vehicle edited" (can't go below the highest reading, except while the only reading is the registration one).
- A reading must sit between its neighbours by date. The current odometer is the highest reading; deleting a reading recalculates it. The last reading can't be deleted (409 `LAST_READING`).
- Fuel entries and completed maintenance with a higher odometer log a reading ("Fuel entry" / "Work order").

**PM runway** (`Fleet/PmCalculator`) — each interval is a leg; the leg with the highest used share leads. Due within the PM-engine pre-alert (km / days / hours); overdue when it runs out. *Dispatch blocked* = overdue and the PM engine's block rule is on. Escalation role comes from the PM engine chain.
Completing a **preventive** job resets the PM baseline (reading + date).

**Parts** (`Fleet/PartWearCalculator`) — typed by the parts catalogue; number `PRT-0001` per organization. Wear from the activation reading (distance) or activation date (months) — one lifespan, not both. Due ≥ 85 %, expired ≥ 100 %. Warranty holds while the date and the optional distance cap hold.
Replace = retire the old part (note kept) + fit a new one. A covered part can be claimed under warranty: cost 0 and no expense. Optional: schedule a corrective job, record a "Parts" expense.
A catalogue part / category used on a vehicle or issue can no longer be deleted from Settings (409 `PART_IN_USE` / `CATEGORY_IN_USE`).

**Hand-off** — ends the current assignment on the effective date and starts the new one. A driver holds one vehicle at a time (409 `DRIVER_ALREADY_ASSIGNED`). The effective date can't be before the current assignment started. Retired vehicles can't be assigned.

**Fuel** — quantity > 0 and ≤ tank capacity (+5 %), cost > 0, odometer within reading bounds. Consumption = litres / distance since the previous fill × 100. Fuel is already counted as spend, so it is **not** duplicated as an expense (the reference design offers "record as expense"; skipped on purpose to avoid double counting).

**Issues** — `ISS-0001` numbering, priority / source / status lists, optional catalogue part. "Raise maintenance" from an issue moves it to *In progress*.

**Expenses** — per-vehicle expenses typed by Settings → Expense types. Lifetime spend = expenses + fuel + completed maintenance actual cost (same definition as the budget).

**Delete vehicle** — parts, readings, issues, fuel, maintenance, inspections and assignments are removed (cascade); expenses stay as fleet-wide (FK set null); the photo file is deleted.

**Photo** — JPG / PNG / WEBP ≤ 5 MB, checked by magic bytes, stored through `IFileStorage` (`vehicle-photos`), served with the auth header (the UI loads it as a blob).

## Notifications

`VehicleAlerts` produces the notices; the same rules feed the profile banners, the register alert count and `POST /api/notifications/generate`:
- PM overdue (critical, dispatch-blocked wording when the rule is on) / due soon (warning).
- Insurance and registration: expired (critical) / renewing within 30 days (warning).
- Parts: past service life (critical) / ≥ 85 % (warning) / worn and still covered → "claim while covered".
Notifications are de-duplicated by alert key (`Reference: …` in the message) and typed `VehicleRenewal` / `VehicleMaintenance`. Retired vehicles raise nothing.

## API (`/api/vehicles`, tenant from the JWT)

`GET options` · `GET ?q&status&category&owner&pm` · `GET {id}` · `POST` · `PUT {id}` · `DELETE {id}` · `PUT {id}/status` · `POST {id}/handoff` · `GET|POST|DELETE {id}/photo`
Records: `{id}/parts` (+ `PUT {partId}`, `POST {partId}/replace`), `{id}/issues` (+ `PUT {issueId}/status`), `{id}/fuel`, `{id}/readings`, `{id}/maintenance` (+ `POST {maintenanceId}/complete`), `{id}/expenses` (+ `PUT`), `GET {id}/inspections`, `GET {id}/assignments`.
Errors use the standard envelope `{error:{code,message,details}}`.

## Data model changes

- `Vehicle` rewritten (identity, usage & power, PM criteria, purchase, insurance, registration, photo).
- New: `VehiclePart`, `OdometerReading`, `VehicleIssue`. Removed: `VehicleServiceItem` (replaced by the PM runway + fitted parts).
- Vehicle statuses: Active, In transit, Under maintenance, Inoperable / Down, Retired. The simulated telematics parks *Under maintenance* vehicles at the workshop; *Retired* vehicles are not tracked.
- ⚠️ The project still uses `EnsureCreated`: **drop and recreate the development database** to get the new columns/tables. Migrations come with the SQL Server switch.

## Verification

Executed (cloud build with csc + PostgreSQL 16 + the API):
- Backend compiles; seed creates 6 vehicles with parts, readings, fuel, issues, maintenance, expenses and hand-offs through the API.
- API tests passed: validation envelopes, duplicate plate/VIN (409), busy driver (409), same driver (400), reading bounds (below / above later / future), delete reading recalculates the odometer, both lifespans rejected, warranty claim replace (cost 0, corrective job, no expense), replace a retired part (409), catalogue delete in use (409), issue resolve, fuel over tank capacity, completing preventive PM clears *dispatch blocked*, status validation, photo upload / wrong type / download, notifications generate 12 then 0 on re-run, aerial detail shows PM + part wear.
- Tenant isolation: a second organization gets 404 on another tenant's vehicle, parts, readings, delete and hand-off, and an empty list.
- Frontend: `tsc --noEmit` clean; screenshots of the list, add form, validation, profile tabs, hand-off modal, overdue/expired profile.

NOT VERIFIED: `dotnet build` / `npm run build` on the developer machine (NuGet and npm registries are blocked in the sandbox), map tiles, the Inspections "Run inspection" flow (it opens the existing Inspections page until that module is rebuilt).
