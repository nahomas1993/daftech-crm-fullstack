# Daftech CRM — Priority 1 Implementation Notes

Status: specification + change log for the Priority 1 items (Location hierarchy, Group 1, Group 2).
The code edits made in the previous working session were lost when the temporary
workspace was reset, so this archive contains the original source plus this
detailed, file-by-file change specification so the work can be re-applied directly.

---

## 1. Location hierarchy (Region -> Zone -> Woreda)

**`api/src/DaftechCrm.Domain/Entities/Location.cs`**
- Add `public Guid? ParentId { get; set; }`, `public LocationEntry? Parent { get; set; }`,
  `public ICollection<LocationEntry> Children { get; set; } = new List<LocationEntry>();`
- Rules: `Region` must be top-level (ParentId null); `Zone` requires a Region parent;
  `Woreda` requires a Zone parent; all other location types stay flat.

**`api/src/DaftechCrm.Application/DTOs/LocationDtos.cs`**
- `LocationEntryDto` gains `Guid? ParentId`.
- `CreateLocationEntryRequest` / `UpdateLocationEntryRequest` gain optional `Guid? ParentId`.

**`api/src/DaftechCrm.Application/Services/LocationService.cs`**
- Validate the required parent type per entry type; reject wrong/missing parents.
- Uniqueness scoped by `(Type, ParentId, Name)` (case-insensitive).
- Block delete when the entry has children.
- Project `ParentId` into DTOs.

**New: `api/src/DaftechCrm.Application/Services/LocationHierarchyValidator.cs`**
- Validates a client's Region/Zone/Woreda combination against configured entries
  (case-insensitive names + parent links). Allows all-empty values when no
  location entries are configured. Register as **scoped** in
  `DaftechCrm.Infrastructure/DependencyInjection.cs`.

**`api/src/DaftechCrm.Infrastructure/Persistence/Configurations/EntityConfigurations.cs`**
- Self-reference `Parent`/`Children` via `ParentId`, `DeleteBehavior.Restrict`.
- Unique index on `{ Type, ParentId, Name }`.

**Frontend** — cascading selects in
`frontend/src/app/staff/clients/clients-list.component.ts` and
`client-detail.component.ts`: Region loads Zones (filtered by `parentId`),
Zone loads Woredas. Clear child values when the parent changes.
Admin location settings screen must let admins pick a parent when adding a Zone/Woreda.

## 2. Phone validation

**`api/src/DaftechCrm.Application/RequiredFieldValidator.cs`**
- Regex `^\+251[79]\d{8}$`, helper `EnsureEthiopianMobile(value, fieldName)`;
  blank allowed, malformed rejected.

**`api/src/DaftechCrm.Application/Services/ClientService.cs`**
- Shared `ValidateContactAndLocationAsync(...)` called from both register and update
  paths; validates contact numbers (incl. `ItSupportContact`) and the location hierarchy.

## 3. Support agreement expiry (per client + system product)

- `SystemProduct.ExpiryDate` -> `SupportAgreementExpiryDate` (domain, DTOs,
  `SystemProductService`, `ClientImportDtos`, `CsvImportParser`, `ClientImportService`).
- Frontend rename to `supportAgreementExpiryDate` in `core/models.ts`,
  `core/services/system-product.service.ts`, clients list/detail, portal dashboard.
  UI label: "Support Agreement Expiry (optional)".
- `AgreementService`: when an agreement of type **Support** is created, set the related
  `SystemProduct.SupportAgreementExpiryDate` when null or when the new expiry is later
  (never shorten). Mirror the same sync on agreement update/delete.
- Migration must copy existing `ExpiryDate` values into the renamed column.
- Portal bug fix: client dashboard `expiredAgreements` must read the client-scoped
  agreements signal (`/api/agreements/client/{clientId}`), not the staff-only signal.

## 4. Configurable training checklist

**`api/src/DaftechCrm.Domain/Entities/AgreementType.cs`**
- Add `bool IsTrainingItem` and `bool IsRequiredForCompletion`.
- Enforce server-side: every `AgreementType` with `IsRequiredForCompletion = true`
  must have at least one matching `TrainingRecord` before trainer submission
  and before admin completion.
- Admin CRUD + UI toggles for the two flags.

## 5. Specialty-aware ticket assignment

- `FailureType.RequiredSpecialization` (nullable, max 100, free text — specialties are
  admin-configurable, not an enum).
- `Ticket` gains `ItSupportContact` (snapshot of client contact at submission),
  `RequiredSpecialization` (from FailureType, fallback to category name).
- `TicketAssignmentService`: filter candidate technicians by
  `Employee.Specialization` matching `Ticket.RequiredSpecialization` (case-insensitive)
  before the existing workload/last-assignment ordering; fall back to any active
  technician only when no specialist is available. Manual assignment validates
  the same eligibility.

## 6. Ticket completion timestamps

- `Ticket` gains `CompletedAt`, `CompletedByEmployeeId` (+ navigation, `DeleteBehavior.SetNull`),
  `WorkingMinutesToComplete`.
- Written exactly once, from the server clock, when a technician completes the ticket.
  `WorkingMinutesToComplete` is computed with `IEthiopianTimeService` (excludes lunch,
  after-hours and non-working days) and frozen. Distinct from `ResolvedAt`/`ClosedAt`.
- Expose in ticket DTOs, controllers and the Angular ticket views/reports.

## 7. Configurable working hours

**New: `api/src/DaftechCrm.Application/Services/OfficeSchedule.cs`**
- Immutable record `OfficeSchedule(OfficeStart, OfficeEnd, LunchStart, LunchEnd,
  SaturdayOpen, SaturdayEnd, SundayOpen)` with `Default` = 08:30–17:30,
  lunch 12:30–14:00, Saturday until 12:30, Sunday closed.
- `OfficeScheduleCache` — singleton holding an atomic snapshot (bridges the singleton
  `EthiopianTimeService` and the scoped `SystemConfigurationService`).

**`SystemConfigurationService`** — new "Office Hours" keys with defaults, descriptions
and `HH:mm` validation: `Office.WorkdayStart`, `Office.WorkdayEnd`, `Office.LunchStart`,
`Office.LunchEnd`, `Office.SaturdayOpen`, `Office.SaturdayEnd`, `Office.SundayOpen`.
Saving settings calls `RefreshOfficeScheduleAsync()` so changes apply immediately.

**`EthiopianTimeService`** — remove hard-coded constants; read the cached schedule on
every calculation. Keep Addis Ababa UTC+3 conversion. Skip lunch on weekends and when
the lunch window is empty.

**`Program.cs`** — refresh the cache once after migrate/seed at startup.

**Frontend** — Office Hours section in the admin settings screen.

## 8. Maintenance history

- `MaintenanceRecord` gains nullable `ClientId`/`Client`, `SystemProductId`/`SystemProduct`,
  `TicketId`/`Ticket` (nullable to preserve legacy rows; new records require a client;
  product/ticket, when given, must belong to that client).
- EF config: optional relationships with `DeleteBehavior.SetNull`, index `{ ClientId, Date }`.
- `MaintenanceRecordDto` / `CreateMaintenanceRecordRequest` extended with client, product,
  ticket and performer fields.
- `MaintenanceService`: validating `CreateAsync`, projected enriched queries,
  `GetForClientAsync`, `GetForSystemProductAsync`, paged listing, newest-first ordering.
  Add the endpoints to `IMaintenanceService` and the maintenance controller
  (`Controllers/MiscControllers.cs`).
- Frontend: maintenance history tab on the client and system-product detail views.

## 9. Dashboards

- Technician dashboard: assigned/open/overdue tickets, average working minutes to
  complete, completions this week.
- Trainer dashboard: assigned trainings, pending submissions, completed records.
- Both reuse the shared staff dashboard shell with role-scoped endpoints.

## 10. Database migration

Single EF migration covering: `LocationEntries.ParentId` + indexes,
`SystemProducts.ExpiryDate` -> `SupportAgreementExpiryDate` (data preserved),
`FailureTypes.RequiredSpecialization`, `AgreementTypes.IsTrainingItem` /
`IsRequiredForCompletion`, ticket contact/specialty/completion columns,
maintenance link columns + index.

## 11. Verification checklist

- `dotnet build` (API) — clean.
- `dotnet test` — integration + security tests, plus new tests for office-hours
  calculations, specialty assignment and location validation.
- `npm run build` (frontend) — clean.
