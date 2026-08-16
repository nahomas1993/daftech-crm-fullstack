using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty.
            // This migration was already applied to the production database
            // before this file was corrupted (it had accidentally been
            // overwritten with a duplicate copy of InitialCreateModel.cs,
            // causing CS0101/CS0111/CS0115 build errors).
            //
            // EF Core's __EFMigrationsHistory table on the live DB already
            // has a row for "20260801000000_InitialCreate", so this Up()
            // will never be re-run against that database. This placeholder
            // exists only to restore a valid Migration class so the project
            // compiles and the migration chain stays consistent.
            //
            // RELEVANT TO THE "AccountRefId not applying consistently on
            // Render deploys" issue: this empty Up() means any genuinely
            // fresh database (a new Render Postgres instance, a new
            // developer's local DB, a CI test database) gets ZERO tables
            // from this migration, so `dotnet ef database update` against
            // it will fail as soon as it reaches a later migration that
            // ALTERs a table this one was supposed to create (e.g.
            // 20260813000000_AddAccountRefId, which ALTERs "clients" and
            // "employees"). If the AccountRefId column is ever missing on
            // a live deploy, checking whether that deploy's database was
            // ever actually bootstrapped from a complete schema — versus
            // partially migrated against an empty/mismatched one — should
            // be the first thing checked, before assuming the AddAccountRefId
            // migration's own SQL is at fault (it isn't — see that file).
            //
            // If you ever need to provision a genuinely fresh database:
            //   (a) Preferred — restore a pg_dump of the real production
            //       schema (schema-only, `pg_dump --schema-only`) rather
            //       than trying to recreate it from this migration chain,
            //       then mark all migrations up to the dump's point as
            //       already-applied with `dotnet ef migrations script
            //       --idempotent` inspection or manual __EFMigrationsHistory
            //       inserts — this guarantees exact column
            //       types/defaults/precision match production, which
            //       hand-written CreateTable calls reconstructed from
            //       InitialCreateModel.cs cannot fully guarantee.
            //   (b) If (a) isn't available, replace this placeholder's Up()
            //       with real CreateTable/AddForeignKey/CreateIndex calls
            //       built from InitialCreateModel.cs in this same file
            //       (it has the full declarative shape of every table this
            //       migration was meant to create) — but verify every
            //       column's exact type/nullability/default against the
            //       live database first with
            //       `\d+ <table>` in psql for each of the 13 tables it
            //       covers, since a subtly wrong reconstruction (e.g. a
            //       default value or precision that doesn't match
            //       production) would only surface later, in ways that are
            //       hard to trace back to this migration.
            //   (c) Either way, do NOT run this against a database that
            //       already has these tables — it will conflict.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty — mirrors Up(). See comment above.
        }
    }
}
