# Administration & Settings

## Navigation
The sidebar item **Administration** now expands into three sub-modules:
| Sub-module | Route | Content |
|---|---|---|
| Company | `/administration/company` | Company details, regional settings, notifications (the former Settings tab) |
| Roles & Users | `/administration/users` | Users + Audit log (the permission matrix comes with the Roles module) |
| Settings | `/administration/settings` | Tabs: Parts catalogue · Expense types · Maintenance (task categories + PM engine) · Budget |

`/administration` redirects to Company. All four tabs are implemented; every write is Owner/Admin only (403 otherwise), everyone in the organization can read.

## Parts catalogue
- Organization-scoped groups (`PartCategory`) and their preset parts (`PresetPart`), each with an English and an Arabic name.
- **Defaults:** every organization starts with 7 groups and 36 parts (Engine Group … Body & Consumables). They are seeded on registration, at API start-up for existing organizations, and on first read.
- **Codes are assigned automatically and never edited:**
  - category `C{n}`
  - part `P{n}{seq:00}`, e.g. P501 Brake Pads, and a new part in Braking System becomes P505.
- **Rules:**
  - names are required, max 100 characters
  - category names are unique per organization
  - part names are unique within their category, case-insensitive
- **Delete:** deleting a category also deletes its parts, after a confirmation that shows how many parts will go.
  - When vehicle parts, inspections or work orders start referencing preset parts, deletion of items in use will be blocked instead.
- **Permissions:** everyone in the organization can read the catalogue; only Owner/Admin can add, edit or delete (403 otherwise). The UI hides the actions for other roles.

### UX
Master / detail instead of stacked group cards:
- left: the groups, with code, English + Arabic name, part count and an actions menu (Add part / Edit / Delete)
- right: the selected group's parts, with row actions (Edit / Remove)
- one search box across all parts (by English name, Arabic name or code), with results that show each part's group
- both panes scroll inside a fixed-height frame, so the page itself doesn't grow

### API (`/api/settings/parts-catalogue`)
| Method | Route |
|---|---|
| GET | `/` — groups with their parts + total |
| POST / PUT / DELETE | `/categories`, `/categories/{id}` |
| POST | `/categories/{id}/parts` |
| PUT / DELETE | `/parts/{id}` |

### Database
New tables `PartCategories` and `PresetParts` (unique: org+number, org+name, org+code, org+category+name). The API still uses `EnsureCreated`, so the dev database must be recreated.

## Expense types (`/api/settings/expense-types`)
- **Defaults:** Fuel, Maintenance, Parts, Insurance, Tires, Other, each with a colour used in charts and the budget.
- **System types** can be renamed and recoloured but never deleted:
  - **Fuel** also counts fuel transactions.
  - **Maintenance** also counts completed maintenance cost.
  - **Other** collects any expense whose category matches no type.
- **Entries / Total** use the dashboard's spend definition: expenses + fuel transactions + completed maintenance.
- **Rules:**
  - names are unique per organization (case-insensitive, max 60 characters)
  - colours are hex values
  - a type with expenses can't be deleted (409 `EXPENSE_TYPE_IN_USE`)
  - deleting a type removes its budget shares
- **Renaming** a type also renames `Expense.Category` on its expenses, inside one transaction.
- **Tech debt:** `Expense.Category` is still a string. It becomes a foreign key to `ExpenseType` when the Expenses module is rebuilt.
- The Expenses page's category list now comes from these types.

## Maintenance
**Task categories** (`/api/settings/task-categories`):
- defaults: Mechanical, Electrical, Body, Safety, Performance, Other
- names are unique per organization
- task counts show 0 until the Work orders module links tasks to categories; categories in use will then be protected from deletion

**PM engine** (`/api/settings/pm-engine`), one row per organization:
- distance pre-alert (km), time pre-alert (days), engine-hour pre-alert
- auto work orders: generate automatically, or suggest only
- safety rule: block dispatch when critical PM is overdue, or warn only
- escalation chain: 1–6 steps, each with its days overdue and a role (Technician, Supervisor, Fleet manager, Admin); the days must be unique

Where the rules apply today and later:
- **Active now:** a vehicle service item is **due** when its km left ≤ the distance pre-alert, or its days left ≤ the time pre-alert. It is **overdue** when either leg reaches 0. This replaced the fixed 85% threshold and applies to the vehicle profile and the Aerial view panel.
- **Stored now, enforced later:** auto work orders, dispatch blocking and escalation are saved today and will be enforced by the Work orders module.

## Budget (`/api/settings/budget/{year}`)
- **One annual budget per calendar year** (`AnnualBudget`), with 12 month allocations and a share % per expense type.
- **No budget yet:** the tab offers a suggested amount (last year's spend + 5%, rounded to 10,000).
- **Creating a budget:** months split evenly (the last month absorbs rounding). Category shares start from last year's actual mix, or an even split when there's no history. Shares are nudged so they total exactly 100%.
- **Changing the annual amount** re-splits the months evenly.
- **Overriding a month** leaves the others as they are. Any gap between the monthly plan and the annual amount is shown as over/under.
- **Split evenly** and **Weight by last year** reset the monthly plan. Weight needs spend in the prior year.
- **Category shares** must be 0–100 each. When the total isn't 100% a warning appears, with "Distribute from last year" to fix it.
- **Actuals** per month and per category use the same spend definition as expense types.
- Every budget change is written to the audit log.
- **Existing Budgets page:** the sidebar's Budgets page (per-month limits by category) still uses the older `Budget` table. It will move to this annual budget when that module is reworked.

## Database
New tables: `ExpenseTypes`, `TaskCategories`, `PmEngineSettings`, `PmEscalationSteps`, `AnnualBudgets`, `BudgetMonthAllocations`, `BudgetCategoryShares`. Defaults are seeded per organization (registration, start-up, first read). Recreate the dev database (`EnsureCreated`).
