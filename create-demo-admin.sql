-- Creates a new demo Admin account you can log in with immediately,
-- without touching your existing (disabled) admin.
--
-- Login after running this:
--   Username: demoadmin
--   Password: DemoAdmin1!
--
-- The password hash below was generated with PBKDF2-SHA256, 100,000
-- iterations, 16-byte salt, 32-byte hash — the exact algorithm and format
-- used by the app's PasswordHasher.Hash(), verified round-trip against
-- the same Verify() logic before being included here.
--
-- Table/column note: the table is lowercase "employees" (set explicitly
-- via ToTable("employees")), but its columns keep EF's default PascalCase
-- names, so they must stay double-quoted exactly as below or Postgres
-- will fold them to lowercase and the insert will fail.

INSERT INTO "employees" (
    "Id",
    "AccountRefId",
    "FullName",
    "Email",
    "PhoneNumber",
    "Specialization",
    "Roles",
    "ExtraRoleLabels",
    "AccountStatus",
    "Username",
    "PasswordHash",
    "MustChangePassword",
    "OtpExpiresAt",
    "AllowedIpAddresses",
    "DisabledAt",
    "DisabledReason",
    "IsDeleted",
    "DeletedAt"
) VALUES (
    '4f929804-cabf-487c-90d8-97ab1ae22411',   -- Id (fixed UUID for this account)
    'DAF-ADMIN-9999',                          -- AccountRefId (display label only, not a permission)
    'Demo Admin',                              -- FullName
    'demoadmin@daftech.et',                    -- Email (must be unique — change if this collides)
    '+251900000000',                           -- PhoneNumber
    'Admin',                                   -- Specialization (free text)
    'Admin',                                   -- Roles (pipe-separated EmployeeRole values)
    '',                                        -- ExtraRoleLabels (empty list)
    0,                                         -- AccountStatus (0 = Active)
    'demoadmin',                               -- Username (must be unique — change if this collides)
    '100000.tiawiNXUUFyMxMq/e9uP6w==.fme+Goh+QVp+jaLst+dxIkJhfKAh4qr8PKqg2NPyOJY=', -- PasswordHash for "DemoAdmin1!"
    false,                                     -- MustChangePassword
    NULL,                                      -- OtpExpiresAt
    '',                                        -- AllowedIpAddresses (empty = no IP restriction)
    NULL,                                      -- DisabledAt
    NULL,                                      -- DisabledReason
    false,                                     -- IsDeleted
    NULL                                       -- DeletedAt
);

-- Verify it landed correctly:
-- SELECT "Username", "AccountStatus", "Roles" FROM "employees" WHERE "Username" = 'demoadmin';
