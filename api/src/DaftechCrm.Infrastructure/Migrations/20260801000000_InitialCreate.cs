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
            // If you ever rebuild a fresh database from scratch (new env,
            // new dev DB, etc.), this Up() will NOT create any tables,
            // because it's empty. In that case, either:
            //   (a) run `dotnet ef database update` starting from the
            //       "AddPasswordResetRequests" migration against a DB that
            //       already has the base schema, or
            //   (b) replace this placeholder by regenerating it against
            //       the live schema (see chat notes on `dbcontext scaffold`)
            //       to get the real CreateTable calls back.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty — mirrors Up(). See comment above.
        }
    }
}
