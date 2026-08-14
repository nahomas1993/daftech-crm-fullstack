using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Adds the permanent, human-readable account identifier ("DAF-ADMIN-####"
    /// / "DAF-EMP-####" / "DAF-CLI-####") to employees and clients. Added as
    /// nullable first, backfilled for any pre-existing rows using Postgres's
    /// own random-digit generation (so this is safe to run against a database
    /// that already has data, not just a fresh one), then made NOT NULL +
    /// unique — mirroring the same two-step pattern Postgres migrations use
    /// whenever a new required column needs a value for existing rows.
    /// </summary>
    public partial class AddAccountRefId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountRefId",
                table: "clients",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountRefId",
                table: "employees",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // Backfill any existing rows (e.g. a database seeded before this
            // migration existed) with a random 4-digit suffix. Collisions are
            // astronomically unlikely at this scale, but if the whole-table
            // update did produce one, the unique index below fails the
            // migration loudly instead of silently allowing a duplicate.
            migrationBuilder.Sql(@"
                UPDATE clients
                SET ""AccountRefId"" = 'DAF-CLI-' || LPAD(FLOOR(RANDOM() * 10000)::text, 4, '0')
                WHERE ""AccountRefId"" IS NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE employees
                SET ""AccountRefId"" = CASE
                        WHEN ""Roles"" LIKE '%Admin%' THEN 'DAF-ADMIN-' || LPAD(FLOOR(RANDOM() * 10000)::text, 4, '0')
                        ELSE 'DAF-EMP-' || LPAD(FLOOR(RANDOM() * 10000)::text, 4, '0')
                    END
                WHERE ""AccountRefId"" IS NULL;
            ");

            migrationBuilder.AlterColumn<string>(
                name: "AccountRefId",
                table: "clients",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AccountRefId",
                table: "employees",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_clients_AccountRefId",
                table: "clients",
                column: "AccountRefId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employees_AccountRefId",
                table: "employees",
                column: "AccountRefId",
                unique: true);

            // Account cleanup: on a database that already ran the old
            // 4-employee seed, trim down to the single Admin + single
            // Employee kept for testing. Scoped to these exact known seeded
            // ids only — never touches any employee created through normal
            // registration, and never touches clients, agreements, or any
            // other business data. A no-op on a fresh database (seeding
            // runs after migrations and only inserts the trimmed 2-employee
            // set from the start).
            //
            // Open tickets assigned to these employees are unassigned
            // (AssignedEmployeeId -> NULL, matching the app's own
            // OnDelete(SetNull) rule) rather than deleted, so no ticket or
            // business data is lost — an Admin can simply reassign them
            // afterward. Purely historical rows that only exist to record
            // this employee's own activity (their login history, device
            // sessions, time logs) are removed along with the account,
            // since they have no meaning once the account itself is gone.
            migrationBuilder.Sql(@"
                UPDATE tickets SET ""AssignedEmployeeId"" = NULL
                WHERE ""AssignedEmployeeId"" IN (
                    '11111111-0000-0000-0000-000000000003',
                    '11111111-0000-0000-0000-000000000004'
                );

                UPDATE tickets SET ""ForwardedByEmployeeId"" = NULL
                WHERE ""ForwardedByEmployeeId"" IN (
                    '11111111-0000-0000-0000-000000000003',
                    '11111111-0000-0000-0000-000000000004'
                );

                DELETE FROM device_sessions WHERE ""EmployeeId"" IN (
                    '11111111-0000-0000-0000-000000000003',
                    '11111111-0000-0000-0000-000000000004'
                );

                DELETE FROM login_records WHERE ""EmployeeId"" IN (
                    '11111111-0000-0000-0000-000000000003',
                    '11111111-0000-0000-0000-000000000004'
                );

                DELETE FROM time_logs WHERE ""EmployeeId"" IN (
                    '11111111-0000-0000-0000-000000000003',
                    '11111111-0000-0000-0000-000000000004'
                );

                UPDATE maintenance_records SET ""PerformedByEmployeeId"" = (
                    SELECT ""Id"" FROM employees WHERE ""Id"" NOT IN (
                        '11111111-0000-0000-0000-000000000003',
                        '11111111-0000-0000-0000-000000000004'
                    ) LIMIT 1
                )
                WHERE ""PerformedByEmployeeId"" IN (
                    '11111111-0000-0000-0000-000000000003',
                    '11111111-0000-0000-0000-000000000004'
                );

                DELETE FROM refresh_tokens WHERE ""AccountId"" IN (
                    '11111111-0000-0000-0000-000000000003',
                    '11111111-0000-0000-0000-000000000004'
                ) AND ""AccountType"" = 0;

                DELETE FROM employees
                WHERE ""Id"" IN (
                    '11111111-0000-0000-0000-000000000003',
                    '11111111-0000-0000-0000-000000000004'
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_clients_AccountRefId", table: "clients");
            migrationBuilder.DropIndex(name: "IX_employees_AccountRefId", table: "employees");
            migrationBuilder.DropColumn(name: "AccountRefId", table: "clients");
            migrationBuilder.DropColumn(name: "AccountRefId", table: "employees");
        }
    }
}
