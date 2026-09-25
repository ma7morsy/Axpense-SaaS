# Maintenance module (work orders) + vehicle hand-off inspection

## Screens

| Screen | Where | What it does |
|---|---|---|
| Maintenance | `/maintenance` | Stats (Scheduled next 30 days, In progress, Overdue + escalation role, Committed cost of open orders); tabs **Work orders** (filters Status / Type / Vehicle) and **Issues** (fleet-wide); **Run PM engine**, **New work order**. |
| Work order row menu | list, vehicle Maintenance tab | View · Edit · Mark scheduled · Start work (in progress) · Mark completed · Open vehicle · Delete. |
| New / edit work order | modal | Vehicle*, Type* (Preventive / Corrective / Inspection), Priority (Low–Critical), Status, Scheduled date*, Assigned technician (organization users), Description*; **Tasks** (task, part category, task category, cost — total adds up as you type); Internal notes. |
| Work order view | modal | Status / Type + priority / Scheduled + technician / Total + task count, description, source (issue, inspection, PM engine, part) and odometer at raise, task table with total, notes; Move to next status · Edit. |
| Complete | modal | Optional reading at service. |
| Vehicles | list row menu | Open profile · **Assign driver** · **Hand off** (when a driver holds the vehicle) · **Schedule maintenance** (work order form, vehicle preselected) · Run inspection · Edit · Delete. |
| Vehicle profile | header | **Assign** · **Hand off** (when assigned) · Inspect · **Schedule maintenance** · Edit; Maintenance tab lists the vehicle's work orders with the same actions. |
| Hand off vehicle | modal | Current holder note, **New driver (optional)** — "No new driver — leave unassigned", effective date, note, **Perform a handoff inspection** checkbox → opens the "Handoff inspection" checklist right after. |
| Assign driver | modal | Driver* (required), effective date, note. |

## Rules (backend — `Axpense.Service/WorkOrders`)

- Numbering WO-00001 per organization. One active, non-retired vehicle; 1–40 tasks; task description required, cost ≥ 0, categories from Settings (part categories = catalogue groups, task categories = Settings → Maintenance). Estimated total = sum of task costs.
- Stored statuses: **Scheduled → In progress → Completed**. **Overdue is derived**: a work order still *Scheduled* after its date shows as Overdue (it can't be set by hand — 400). Overdue orders escalate by days late through the PM engine chain (shown on the Overdue card).
- **Completed is final**: the order is locked (edit / status change → 409). On completion the actual cost is fixed from the tasks, a higher reading at service is logged ("Work order"), a **preventive** order restarts the vehicle's PM runway (reading + date), and a linked issue is **resolved**.
- Raising an order from an issue links it and moves the issue to *In progress*.
- **Run PM engine** raises one preventive order (priority High when overdue, Medium when due; scheduled today; source "PM engine") for each vehicle whose PM is due or overdue and has no open preventive order. Running it again creates nothing new.
- Work orders are also created automatically by a failed critical inspection check (Critical, source Inspection, one "Rectify …" task per failed critical check) and by part replacement (Corrective, source Part replacement).
- Settings → Maintenance task categories now show how many tasks use them.

**Hand-off inspection**
- A built-in template "Handoff inspection" (8 checks linked to catalogue parts: tyres, brakes, lights/wipers, coolant, engine oil, battery/warning lights, walk-around photo, steering) is created per organization on first use. It can be edited, can't be deleted, is always active and applies to every vehicle.
- `POST /api/vehicles/{id}/handoff` with `performInspection: true` saves the hand-off first, then starts the checklist (odometer = current reading, inspector = the signed-in user) and returns its id; if a run is already in progress on the vehicle, that one is reopened.

## API

`/api/work-orders`: `GET options` · `GET ?status&type&vehicleId` (items + stats) · `GET {id}` · `POST` · `PUT {id}` · `PUT {id}/status` · `DELETE {id}` · `POST run-pm-engine`
`/api/issues?status=` (fleet-wide issues with their linked work order)
`/api/vehicles/{id}/handoff` now returns `{ vehicle, inspectionId, inspectionMessage }`. The vehicle-level `/maintenance` endpoints were removed (the vehicle tab uses `/api/work-orders?vehicleId=`); the legacy `/api/maintenance` controller was replaced.

## Data model

- `Maintenance` (work order) + Number, Priority, TechnicianUserId / TechnicianName, Notes, Source / SourceRef, IssueId, InspectionId, OdometerAtRaise, StartedAtUtc.
- New `MaintenanceTask` (description, part category, task category — names snapshotted — cost).
- `InspectionTemplate.SystemKey` ("handoff").
- ⚠️ Still `EnsureCreated` — drop the development database once.

## Verification

Executed against PostgreSQL 16 with the compiled API: validation envelopes (empty form, bad task, "Overdue" status rejected); create with 2 tasks (total 1,250.50) → edit → in progress → complete blocked below current odometer → complete (actual cost fixed, reading logged, PM runway restarted 215 → 365 days); edit / reopen completed → 409; completing an issue-linked order resolved ISS-0003; PM engine created 1 order then 0 on re-run; fleet issues list with work-order codes; hand-off with inspection started the 8-check "Handoff inspection"; built-in template delete → 409; another organization gets 404 / empty lists. Frontend `tsc --noEmit` clean; Playwright screenshots of every screen above.

NOT VERIFIED: `dotnet build` / `npm run build` on the developer machine.
