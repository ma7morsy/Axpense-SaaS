# Reports, Budgets and the PM engine

## Navigation

- **Fuel** and **Notifications** are removed from the side menu.
  - Notifications stay on the top-bar bell (`/notifications` still works).
  - `/fuel` now opens **Reports → Fuel log**.
- **Arabic font:** Cairo, on the auth screens and in RTL layouts.

## PM engine (Maintenance → Run PM engine)

- **Run PM engine** (on Maintenance, and on Reports → PM compliance) opens the **Preventive maintenance engine** preview.
  - Subtitle: the pre-alerts and whether auto work orders are on.
  - Table columns: Vehicle, Trigger (distance / time / engine hours · every N), State (e.g. "Overdue by 15 days", "380 km to service"), Task to generate (task · role · duration), Est. cost, Result ("Will be created" or "WO-00003 already open").
  - A **Dispatch is blocked on N vehicle(s)** banner appears when the safety rule applies.
- **Generate N work order(s)** raises one preventive order per due or overdue vehicle that has no open preventive order.

| API | |
|---|---|
| `GET /api/work-orders/pm-engine/preview` | Rows, count to create, blocked count, estimated total |
| `POST /api/work-orders/run-pm-engine` | Optional body `{ vehicleIds }`. An empty body runs every vehicle. |

### PM task library (Settings → Maintenance)

- **Table columns:** Task, part category, task category, trigger (usage / time / hours / hybrid), interval, duration, estimated cost, required role.
- **Defaults:** six tasks are seeded per organization, taken from the reference design (oil & filter, brake pads, tyre rotation, timing belt, hydraulic service, battery test).
- **API:** `GET/POST/PUT/DELETE /api/settings/pm-tasks`. Writes need Owner or Admin.
- **Validation:**
  - Name is required, at most 120 characters, and unique.
  - The trigger decides which interval is required: usage → km, time → days, hours → engine hours, hybrid → km and days. Intervals the trigger does not use are dropped.
  - Duration: 0.25–100 h.
  - Cost: 0–10,000,000.
  - Part and task categories must belong to the organization.

### Rules ⚑

- ⚑ **Which task is raised:** the first active task, in library order, whose interval is in the unit of the vehicle's leading PM leg (km / days / hours). If none matches, the first active task is used; if the library is empty, a generic "Preventive maintenance service" at cost 0. The same rule as the reference; reorder the library to change the pick.
- ⚑ **Priority and date:**
  - Overdue → High priority, scheduled today.
  - Due → Medium priority, scheduled in 3 days.
- **What the order copies from the task:** the task name, part and task category, and estimated cost. The role and duration go into the order notes.
- **Existing orders:** editing or deleting a task never changes orders that were already raised.

## Budgets (`/budgets`)

- **Add budget** opens the modal from your screenshot: Budget name, Category (Overall or an expense type), Limit amount, Year, Month.
- **Filters:** year and month (or all months).
- **Tiles:** Budgeted, Spent, Remaining, Over / at risk.
- **Budget lines:** utilization bar and status.
- **Row menu:** View summary, Edit, Delete.
- **Summary modal:**
  - Limit, spent, remaining and status, with a projection for the current month.
  - Cumulative daily spend against the limit, with an even-pace line.
  - Breakdown: by category for Overall; by source (expenses / fuel / work orders) for a single category.
  - Largest entries.

| API (writes need Owner or Admin) | |
|---|---|
| `GET /api/budgets/options` | Categories |
| `GET /api/budgets?year=&month=` | Rows with actual spend, and totals |
| `GET /api/budgets/{id}` | Summary |
| `POST` / `PUT /{id}` / `DELETE /{id}` | Create, edit, delete |

### Rules ⚑

- **Validation:**
  - Name: 1–100 characters.
  - Limit: greater than 0 and at most 1,000,000,000.
  - Year: 2000–2100. Month: 1–12.
- ⚑ **One limit per category per month.** A duplicate returns 409 `BUDGET_EXISTS`.
- **Actual spend** uses the dashboard definition (expenses + fuel + completed work orders) within the calendar month, filtered to the expense type (all spend for Overall).
- ⚑ **Status:**
  - Over: more than 100 % used.
  - At risk: at least 80 % used, or the current month's run-rate projection is above the limit.
  - On track: otherwise.
- ⚑ **Period totals:** when a month has an Overall limit, that limit is the month's budget; otherwise its category limits are added up. The same spend is never counted twice.
- **Security fix:** the old `api/budgets` controller trusted an `organizationId` sent by the client. It now uses the signed-in tenant.
- **New column:** `Budget.ExpenseTypeId`.
- **Relationship to the annual budget:** the annual budget (Settings → Budget) is unchanged. It feeds Reports → Budget log and the dashboard's Budget vs Actual card.

## Reports (`/reports`)

- **Tabs:** Fuel log · Budget log · PM compliance · Work orders · Cost reports · Expenses & forecast · Issue analysis · Vehicle uptime · Driver performance · Odometer.
- Windowed tabs have a **Last 30 / 90 / 180 / 365 days** picker; Budget log has a year picker.
- Every table has **Export CSV**.
  - Built in the browser from the rows shown.
  - UTF-8 with a BOM, so Excel opens Arabic text correctly.
  - Cells that could run as spreadsheet formulas are neutralised.

| Tab | Content |
|---|---|
| Fuel log | Spend, average L/100 km, heaviest consumer, vehicles without data; consumption by vehicle; full fuel log with search and vehicle filter (latest 500 entries in the window) |
| Budget log | Annual budget, spent this year, months over plan, monthly limits; annual plan against actual (12 tracks with budget markers); log of monthly limits; categories |
| PM compliance | Compliance rate, overdue, inside pre-alert, auto-generated orders; compliance by vehicle with runway gauges and Run PM engine; PM task library |
| Work orders | On-time closure, open orders with committed cost, average job cost, totals; by technician; by type; order register |
| Cost reports | Spend in the window against the previous window, committed maintenance, most expensive vehicle; cost by vehicle, including cost per km; cost by category against the annual share prorated to the window |
| Expenses & forecast | Month to date, run-rate projection, 3-month average, last full month; 6-month trend with forecast bar; category trend (last 30 days against the 30 before) |
| Issue analysis | Totals and recurrence by part (with linked repair cost), by vehicle, by source |
| Vehicle uptime | Fleet uptime, downtime days, off the road now, best performer; per-vehicle uptime |
| Driver performance | Score, licence and documents, assigned vehicle, spend on that vehicle |
| Odometer | Distance, readings, stale readings (no reading in 14 days), highest reading |

**API:** `GET /api/reports/{fuel|budget|pm|work-orders|cost|issues|uptime|drivers|odometer|expenses}`. Windowed reports accept `days=30|90|180|365`.

### Definitions ⚑

- **Distance in a window:** highest minus lowest odometer reading inside it (needs two readings).
- **Fuel consumption:** fuel after the first fill-up divided by the distance between the first and last fill-up odometers.
- **Downtime:** workshop days of each work order (start → completion, or → today while in progress), plus every day a scheduled order is past its date. **Uptime** = 1 − downtime ÷ window.
- **Driver score:** rating (70 %) + valid licence (20 %) + no expired documents (10 %). This replaces the reference's use of inspection and issue counts per driver, because issues are not linked to drivers yet.
- **On-time closure:** completed on or before the scheduled date.
- **Forecast:** month to date ÷ day of month × days in month.

## Verification

- **Executed:**
  - `rtest2.py`, 46/46: PM library CRUD and validation; preview matches tasks; run creates the orders and is idempotent (also with an empty body); generated orders carry the task and cost; budget options, list, totals basis, projection, duplicate 409, validation, create / edit / delete, summary (daily cumulative equals actual; category entries filtered); all ten reports and window validation; fuel log equals consumption total; tenant isolation for budgets, PM tasks, preview and reports.
  - Regressions: `ctest` 57/57; `wtest` expected outputs.
  - `tsc --noEmit` is clean.
  - Screenshots of every new screen with no page errors.
- **NOT VERIFIED:**
  - The engine-hours match (the demo excavator was not due during the test run).
  - Opening the exported CSVs in Excel.
  - SQL Server.
