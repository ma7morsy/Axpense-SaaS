# Drivers module

## Scope
- Driver register: details (name, employee no., status, phone, email, national ID, joined date) and licence (number, class, issued, expiry) plus a 0–5 performance rating.
- Driver documents: name, expiry date and an optional scan (JPG / PNG / PDF, max 5 MB).
- Driver profile: licence banner, KPIs, documents, current vehicle and performance history.
- The Assignments tab was removed from the Drivers screen. Assignment data (`VehicleAssignment`) and `/api/assignments` are kept: they drive "Assigned vehicle", "Current vehicle" and the history. Assigning will move to the vehicle profile.

## Business rules (Axpense.Service/Drivers/DriverService.cs)
| Rule | Value |
|---|---|
| Status | Active · On leave · Suspended |
| Licence class | B — Light vehicle · C — Heavy goods · D — Passenger transport · Heavy equipment operator |
| Required | Full name, licence number, licence class, licence expiry |
| Employee number | Auto-generated `EMP-{max+1}` (seed 1000) when blank; unique per organization |
| Licence number | Unique per organization |
| Expiry vs issue | Expiry must be after the issue date |
| Rating | 0.0 – 5.0, one decimal, default 4.0 |
| Reminder window | 30 days (`DriverConstants.ExpiryReminderDays`) for the licence and every document |
| Document state | Expired (< 0 days) · Expiring (≤ 30 days) · Valid |
| Current vehicle | Assignment where start ≤ today and (no end or end ≥ today); latest start wins |
| History | Assignments + fuel entries on the assigned vehicle inside the assignment window (last 10) |
| Delete driver | Hard delete; assignments and documents cascade; stored scans are removed |
| Upload check | Extension whitelist + file signature (magic bytes) check |

Inspections run and Issues reported show 0 for now: inspections are not linked to drivers and there is no Issues module yet.

## API (tenant taken from the JWT)
| Method | Route |
|---|---|
| GET | `/api/drivers?q=&status=&page=&pageSize=` |
| GET | `/api/drivers/next-employee-number` |
| GET | `/api/drivers/{id}` (profile) |
| POST / PUT / DELETE | `/api/drivers`, `/api/drivers/{id}` |
| POST (multipart) | `/api/drivers/{id}/documents` — `name`, `expiryDate`, `file?` |
| DELETE | `/api/drivers/{id}/documents/{documentId}` |
| GET | `/api/drivers/{id}/documents/{documentId}/file` |

Errors use `{ "error": { "code", "message", "details" } }` (400 validation, 404 not found, 409 duplicate).

## Storage
`IFileStorage` (Infrastructure) with `LocalFileStorage`: files live under `FileStorage:RootPath/{orgId}/driver-documents/`. In production that is the `axpense_files` volume at `/app/storage`.

## Database
New table `DriverDocuments`; new columns on `Drivers`: EmployeeNumber, Email, NationalId, LicenseClass, LicenseIssuedDate, Rating.
The API still creates the schema with `EnsureCreated`, which does **not** alter an existing database, so the dev database must be recreated (drop the volume) before running. Proper migrations will be added with the SQL Server switch.
