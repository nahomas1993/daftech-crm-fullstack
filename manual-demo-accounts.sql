-- ============================================================================
-- Manual fix: adds AccountRefId (if missing) and creates hardcoded demo
-- accounts using the DAF-ADMIN/EMP/CLI-#### id style.
--
-- Run this directly against the Postgres database (Render's psql shell, or
-- any client pointed at the same DATABASE_URL the app uses). This does NOT
-- depend on the app's own EF Core migration having run — it's self-contained.
--
-- Safe to run even if AccountRefId already exists (the ADD COLUMN /
-- CREATE INDEX steps are guarded with IF NOT EXISTS).
-- ============================================================================

-- Step 1: make sure the column + unique indexes actually exist.
-- (If your app's migration DID already add these, these are no-ops.)
ALTER TABLE employees ADD COLUMN IF NOT EXISTS "AccountRefId" character varying(50);
ALTER TABLE clients   ADD COLUMN IF NOT EXISTS "AccountRefId" character varying(50);

-- Backfill any existing rows that don't have one yet, so the NOT NULL +
-- unique constraints below don't fail on old data.
UPDATE clients
SET "AccountRefId" = 'DAF-CLI-' || LPAD(FLOOR(RANDOM() * 10000)::text, 4, '0')
WHERE "AccountRefId" IS NULL;

UPDATE employees
SET "AccountRefId" = CASE
        WHEN "Roles" LIKE '%Admin%' THEN 'DAF-ADMIN-' || LPAD(FLOOR(RANDOM() * 10000)::text, 4, '0')
        ELSE 'DAF-EMP-' || LPAD(FLOOR(RANDOM() * 10000)::text, 4, '0')
    END
WHERE "AccountRefId" IS NULL;

ALTER TABLE clients   ALTER COLUMN "AccountRefId" SET NOT NULL;
ALTER TABLE employees ALTER COLUMN "AccountRefId" SET NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_clients_AccountRefId"   ON clients   ("AccountRefId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_employees_AccountRefId" ON employees ("AccountRefId");

-- ============================================================================
-- Step 2: hardcoded demo accounts.
--
-- Password for all four is:  DaftechDemo1!
-- PasswordHash values below are real PBKDF2-SHA256 hashes (100,000
-- iterations, matching api/src/DaftechCrm.Application/Services/PasswordHasher.cs)
-- for that exact password — not placeholders.
--
-- Roles is stored pipe-delimited (see ValueConverters.RoleListConverter),
-- e.g. "Admin" or "EmployeeTechnician" — not JSON.
--
-- ON CONFLICT clauses make this safe to re-run: if a row with that
-- Username/Email/AccountRefId already exists, this updates it in place
-- instead of erroring, so you can run this script again after any change
-- without needing to delete rows first.
-- ============================================================================

INSERT INTO employees (
    "Id", "AccountRefId", "FullName", "Email", "PhoneNumber", "Specialization",
    "Roles", "ExtraRoleLabels", "AccountStatus", "AllowedIpAddresses",
    "Username", "PasswordHash", "MustChangePassword"
) VALUES (
    gen_random_uuid(), 'DAF-ADMIN-1001', 'Nahom Alehegne', 'nahom@daftech.et', '+251911000001', 'Back-end',
    'Admin', '', 0, '',
    'na1001', '100000.8P1xpaI6ZKIyfzxEZBuwOg==.SsFQrddhVHgQoCQUf1w8RvzTOVRtCwWoKhNEDy6KuEg=', false
)
ON CONFLICT ("Username") DO UPDATE SET
    "AccountRefId" = EXCLUDED."AccountRefId",
    "PasswordHash" = EXCLUDED."PasswordHash",
    "MustChangePassword" = false;

INSERT INTO employees (
    "Id", "AccountRefId", "FullName", "Email", "PhoneNumber", "Specialization",
    "Roles", "ExtraRoleLabels", "AccountStatus", "AllowedIpAddresses",
    "Username", "PasswordHash", "MustChangePassword"
) VALUES (
    gen_random_uuid(), 'DAF-EMP-1002', 'Nebil Sherefa', 'nebil@daftech.et', '+251911000002', 'Front-end',
    'EmployeeTechnician', '', 0, '',
    'ns1002', '100000.UUoyqwcQOhhVgZslmBP3Qg==.15oGoDKyK83ZgQpqypqbBlhPtT+Cnx0W1a2IaxonNbw=', false
)
ON CONFLICT ("Username") DO UPDATE SET
    "AccountRefId" = EXCLUDED."AccountRefId",
    "PasswordHash" = EXCLUDED."PasswordHash",
    "MustChangePassword" = false;

INSERT INTO clients (
    "Id", "AccountRefId", "Name", "IdNumber", "PhoneNumber", "Email",
    "Office", "Location", "KycType", "KycContact", "AccountStatus", "OnboardingDate",
    "Username", "PasswordHash", "MustChangePassword"
) VALUES (
    gen_random_uuid(), 'DAF-CLI-2001', 'Abyssinia Traders PLC', 'ID-88213', '+251911223344', 'contact@abyssiniatraders.et',
    'Bole Head Office', 'Addis Ababa', 'Business License', 'Selam Tesfaye — +251911998877', 1, '2025-02-10',
    'at2001', '100000.hFtuwPlAyCZauwyXh3+ufQ==.0hq0vMuyN4vBVB3ASDGupDQkpbAhSwx3xnyQz1/kovk=', false
)
ON CONFLICT ("Username") DO UPDATE SET
    "AccountRefId" = EXCLUDED."AccountRefId",
    "PasswordHash" = EXCLUDED."PasswordHash",
    "MustChangePassword" = false;

INSERT INTO clients (
    "Id", "AccountRefId", "Name", "IdNumber", "PhoneNumber", "Email",
    "Office", "Location", "KycType", "KycContact", "AccountStatus", "OnboardingDate",
    "Username", "PasswordHash", "MustChangePassword"
) VALUES (
    gen_random_uuid(), 'DAF-CLI-2002', 'Merkato Micro-Finance', 'ID-77012', '+251922334455', 'info@merkatomf.et',
    'Merkato Branch', 'Addis Ababa', 'Financial Institution License', 'Dawit Alemu — +251922112233', 1, '2024-11-03',
    'mm2002', '100000.94ThpxCIrSIuzLSPHIR6fw==.72IIAZvWYugVNTy0Np2cBVulvh9744FfdZ/E0hWVA0k=', false
)
ON CONFLICT ("Username") DO UPDATE SET
    "AccountRefId" = EXCLUDED."AccountRefId",
    "PasswordHash" = EXCLUDED."PasswordHash",
    "MustChangePassword" = false;

-- Quick sanity check: should show all four accounts with a DAF- id.
SELECT "AccountRefId", "Username", 'employee' AS kind FROM employees WHERE "Username" IN ('na1001','ns1002')
UNION ALL
SELECT "AccountRefId", "Username", 'client' AS kind FROM clients WHERE "Username" IN ('at2001','mm2002');
