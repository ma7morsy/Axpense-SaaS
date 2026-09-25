# Administration → Company and Dashboard graphs

## Company (`/administration/company`)

Three tabs, matching the reference design.

| Tab | What it does |
|---|---|
| Company info | Identity (logo, name, legal name, industry, size, CR no., tax ID, founded), contact & address, regional settings (time zone, currency, fiscal year start, date format, distance unit). **Save changes** in the header. |
| Subscription | Current plan hero (status, price, renewal date and days left, auto-renew, customer since), Change plan / auto-renew / Cancel, usage meters (vehicles, seats, storage), plan cards with Monthly/Annual (−20 %) toggle and Upgrade / Downgrade / Current plan. |
| Billing | Payment card (brand, last 4, holder, expiry) with **Update card**, billing contact with **Edit**, invoices table with **Download** (printable HTML). Owners/Admins only. |

### API (`CompanyController`, `/api/company`, writes need Owner or Admin)

| Method | Route | Notes |
|---|---|---|
| GET | `options` | Industries, sizes, time zones, currencies, date formats, units, months |
| GET / PUT | `profile` | Validation errors come back per field in the standard envelope |
| GET / POST / DELETE | `logo` | PNG, JPG or SVG, max 2 MB |
| GET | `subscription` | Plan, status, period, usage, plan catalogue |
| POST | `subscription/plan` | `{ planKey, billingCycle }` |
| PUT | `subscription/auto-renew` | `{ autoRenew }` |
| POST | `subscription/cancel`, `subscription/resume` | |
| GET | `billing` | Card, billing email, invoices (latest 100) |
| PUT | `billing/payment-method` | `{ brand, last4, expiry, holder }` only |
| PUT | `billing/email` | |
| GET | `billing/invoices/{id}/document` | Printable HTML invoice |

### Rules (decisions to confirm are marked ⚑)

- Saving the company name also renames the organization.
- The logo is checked by content (PNG/JPEG signatures). An SVG is refused if it contains scripts, `on…=` handlers, `javascript:`, `foreignObject`, `iframe`, `embed` or `object`. The logo is served with a restrictive CSP and `nosniff`.
- ⚑ **No payment gateway.** The card number never leaves the browser: it is checked with Luhn and used only to derive the brand and last 4 digits. The API stores brand, last 4, expiry and holder. There is no CVC field. Nothing is charged.
- ⚑ New invoices are **Issued** (awaiting the provider). Only a future provider callback should mark them **Paid**. The seeded history is Paid.
- ⚑ Plans are Starter EGP 1,500, Business EGP 4,500 and Enterprise EGP 12,000 per month. Annual = 12 × monthly − 20 %. Limits are 10/3/5 GB, 50/15/50 GB, and unlimited/unlimited/500 GB.
- ⚑ A plan change takes effect immediately, starts a new period and issues a full-price invoice. There is **no proration or credit**.
- A change is refused (409 `PLAN_LIMIT_EXCEEDED`) when current vehicles (non-retired), active users or storage exceed the new plan.
- Plan limits are enforced when creating a vehicle and when creating or reactivating a user (409 `PLAN_LIMIT`).
- ⚑ Renewal is processed when the subscription is read (no scheduler). Each elapsed period with auto-renew on rolls forward and issues an invoice. A period that ends with auto-renew off, or while Cancelling, ends the subscription (Cancelled).
- Cancel sets status **Cancelling** until the period end, and **Keep subscription** undoes it. After it ends, choosing a plan reactivates it. ⚑ A cancelled account is **not** made read-only yet.
- ⚑ An organization with no subscription row gets **Business / monthly** on first read (placeholder until sign-up chooses a plan).
- Storage usage is the size of the organization's folder in file storage.
- The legacy notification switches from the old Company settings form are no longer shown. The fields remain in the table. The old `GET/PUT /api/saas/settings` endpoints were removed.

## Dashboard (`/`)

The layout follows the reference design:

1. **Header:** date-range picker (the last 1, 3, 6 or 12 complete months).
2. **KPI cards:** Total Vehicles, Maintenance Due (overdue count, "open or upcoming"), Total Fuel Cost, Total Expenses, Total Fleet Cost. Each shows the % change against the previous period.
3. **Total Costs Trend:** Fuel, Maintenance and Expenses, last 3/6/12 months.
4. **Cost Breakdown:** donut of Fuel, Maintenance, Expenses and Insurance.
5. **Fleet Overview:** stylised map with vehicle pins and an On Route / Available / Maintenance donut, from live tracking.
6. **Notifications.**
7. **Upcoming Maintenance:** days overdue or due in N days, with a priority badge.
8. **Recent Expenses.**
9. **Operating Cost Trend:** stacked by expense type with a previous-period ghost bar. Monthly (12), Weekly (12) or Daily (30) buckets.
10. **Budget vs Actual:** budget, actual, remaining and utilization; an over-budget banner; tracks with a budget marker; grouped by Month or Category.
11. **Recurrence by part:** part, times, frequency bar, vehicles, and a Recurring / One-off flag.
12. **Value strip:** Improve Safety / Reduce Costs / Increase Efficiency / Drive Sustainability, plus the Axpense logo.

Each panel loads separately and has its own skeleton and error/retry state.

### API (`DashboardController`, `/api/dashboard`)

| Route | Notes |
|---|---|
| `GET ?from&to` | KPIs, cost groups, upcoming maintenance, recent expenses. Default range is last month. |
| `GET cost-trend?months=3\|6\|12` | Last N complete months |
| `GET operating-trend?granularity=month\|week\|day` | |
| `GET budget?groupBy=month\|category` | |
| `GET recurrence?take=7` | |

### Definitions (⚑ = decision)

- **Spend** is the same everywhere (`SpendQueries`): expenses + fuel transactions + the actual cost of completed work orders, each mapped to an expense type.
- ⚑ **Cost groups:**
  - Fuel = the fuel type.
  - Maintenance = the maintenance type.
  - Insurance = the type named "Insurance".
  - Expenses = every other type (Parts, Tires, Other, custom types).
  - Total fleet cost = all four.
- **Changes** compare with the previous period: the same number of whole months, or the same number of days for a custom range.
- ⚑ **Total Vehicles** is today's non-retired fleet. The % compares it with the fleet at the start of the range (purchase date, otherwise date added).
- **Maintenance due** = open work orders + vehicles whose PM is due or overdue and have no open work order. Overdue = Scheduled past its date, or PM overdue.
- **Operating trend ghost bar** = the same slot one whole window earlier (12 months / 12 weeks / 30 days back).
- ⚑ **Budget vs actual:**
  - By month: the last 6 months' allocations from Settings → Budget (or annual ÷ 12).
  - By category: this year's annual budget × category share, against the last 12 months of spend.
- ⚑ **Recurrence:** vehicle issues grouped by catalogue part (issues without a part show as "Unclassified"). A part reported more than once is Recurring, as in the reference.
- ⚑ **Fleet overview** uses live tracking motion: moving = On Route, workshop = Maintenance, idle/parked = Available. Pins are placed on a stylised map by relative position; it is not a geographic map (Aerial view has the real map).

## Verification

- **Executed:**
  - API tests `ctest.py`, 57/57: validation, logo type/SVG checks, CSP header, plan pricing, upgrade/downgrade/limit refusals, auto-renew/cancel/resume, card and email validation, invoice document, seat limit on user create, all dashboard endpoints and validations, and tenant isolation for profile, billing, invoice, logo, dashboard, recurrence and budget.
  - `rtest.py`: lazy renewal over 2 periods, Cancelling → Cancelled at period end, reactivation.
  - Regression suites `vtest`, `itest` and `wtest` ran without failures.
  - `tsc --noEmit` is clean.
  - Playwright screenshots of every tab and of the dashboard.
- **NOT VERIFIED:**
  - Build with the real `dotnet build` / MSBuild project files (compiled here with csc against the same DLLs).
  - SQL Server (tested on PostgreSQL via `EnsureCreated`).
  - Printing the invoice to PDF from a browser.
  - Mobile layouts below 1,000 px.
