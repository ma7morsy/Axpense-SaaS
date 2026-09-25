# Inspections module

Checklist templates built from the parts catalogue, inspection runs with photo evidence, and automatic follow-up (issues, grounding, corrective maintenance).

## Screens

| Screen | Where | What it does |
|---|---|---|
| Inspections | `/inspections` | Stats (completed, failed items, templates, vehicles never inspected), **Inspection log** (filters Vehicle / Template; row menu Open or Resume · Open vehicle · Delete or Discard) and **Templates** (cards with checks, Critical / Auto issue badges, Delete · Edit template · Run this, "New template" card). |
| New / edit template | modal | Name, Applies to (All or a vehicle category), State (Active / Draft); checklist rows: Label, **Linked part** (catalogue, grouped by category), Field type (Pass/Fail, Numeric/gauge, Scale 1–5, Photo only), Criticality, Unit + expected range (gauge), Trigger logic (create an issue / record only). |
| Run inspection | modal | Vehicle, Template (active and applicable to the vehicle's category), odometer, inspector. Also from the Vehicles list (header + row action) and the vehicle profile (Inspect, Inspections tab). |
| Checklist | modal | One card per check: reading / score, Pass · Fail · N/A, camera, failure comment; critical-failure banner; sticky summary bar (done, passed, failed, N/A, progress) with **Complete inspection**. Closing keeps the draft; *Discard draft* deletes it. |
| Inspection view | modal | Passed / Failed / N/A / Odometer, every check with reading, comment, evidence photo and whether an issue was raised. |

## Business rules (backend — `Axpense.Service/Inspections`)

**Templates**
- Name required, unique per organization; scope "All" or a vehicle category; 1–60 checks.
- Every check is linked to a part of the catalogue. A gauge check needs a unit and a min ≤ max range. The label defaults to the part name.
- Only **active** templates can be run. Editing replaces the checklist; runs already started keep their own copy.
- Deleting a template keeps past (and in-progress) inspections — they carry a snapshot of the checklist.
- A catalogue part or category used by a template can't be deleted from Settings (409 `PART_IN_USE` / `CATEGORY_IN_USE`).

**Running**
- Retired vehicles can't be inspected; the template must be active and its scope "All" or the vehicle's category.
- One run in progress per vehicle (409 `INSPECTION_IN_PROGRESS`; the UI resumes the existing draft).
- The odometer at inspection can't be below the vehicle's current reading.
- The run is a server-side draft: each answer and photo is saved immediately, so it survives closing the window or switching devices.
- A gauge reading outside the expected range is recorded as a **failure** automatically. Scale scores are whole numbers 1–5.
- Completion is blocked until every check is answered, gauge / scale values are entered (unless N/A), **every failed check has photo evidence**, and photo-only checks have their photo. The API returns the blocking checks in `error.details`.
- Evidence photos: JPG / PNG / WEBP ≤ 5 MB, checked by magic bytes, stored via `IFileStorage` (`inspection-photos`).

**On completion**
- The odometer reading is logged ("Inspection") when it moved forward.
- Every failed check with "create an issue" raises a vehicle issue — **High** priority when the check is critical, otherwise Medium; source "Inspection"; the note carries the inspection number, comment and reading.
- A failed **critical** check grounds the vehicle (**Inoperable / Down**) and schedules a corrective maintenance job for today listing the failed critical checks.
- A completed inspection can't be changed (409 `INSPECTION_COMPLETED`). Deleting it keeps the issues it raised.

## API

`/api/inspection-templates`: `GET options` · `GET` · `GET {id}` · `POST` · `PUT {id}` · `DELETE {id}`
`/api/inspections`: `GET ?vehicleId&templateId&status` (items + stats) · `GET {id}` · `POST` (start) · `PUT {id}/items/{itemId}` (answer) · `POST|DELETE|GET {id}/items/{itemId}/photo` · `POST {id}/complete` · `DELETE {id}`
Tenant from the JWT; standard error envelope.

## Data model

- New: `InspectionTemplate`, `InspectionTemplateItem` (FK to `PresetPart`, restrict).
- `Inspection` + Number (INS-00001), TemplateId (set null on delete), TemplateName, Odometer, InspectorName.
- `InspectionItem` + snapshot of the check (label, part, field type, critical, unit, range, issue-on-fail) and the answer (result, value, comment, photo, raised issue).
- `VehicleIssue.SourceInspectionId` (no FK).
- ⚠️ Still `EnsureCreated` — drop the development database once.

## Verification

Executed against PostgreSQL 16 with the compiled API:
- Template validation (empty, gauge without unit/range, missing part, duplicate name); scope mismatch, draft template, odometer below current, second draft on the same vehicle (409).
- Gauge 2.8 mm against 4–14 → auto-failed; scale 7 rejected; completion blocked while photo evidence missing; wrong photo type rejected.
- Completion: 1 High issue raised on Brake Pads, vehicle grounded (Inoperable / Down), corrective job scheduled, odometer reading logged; answering after completion → 409.
- Catalogue part used in a template can't be deleted; deleting a used template keeps the inspection history (snapshot, template link cleared).
- Tenant isolation: another organization gets 404 on get / delete / answer and empty lists.
- Frontend `tsc --noEmit` clean; Playwright run of the full flow (templates, new template, start, checklist, critical failure, photos, complete, vehicle tab, view).

NOT VERIFIED: `dotnet build` / `npm run build` on the developer machine; camera capture on a real phone (`capture="environment"`).
