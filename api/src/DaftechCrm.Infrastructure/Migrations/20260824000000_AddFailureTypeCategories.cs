using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations;

/// <summary>
/// Adds FailureType.Category (TicketCategory: Frontend/Backend/Database).
///
/// Two things this migration originally got wrong, both fixed here:
///  1. It shipped without a .Designer.cs file, so it carried no
///     [DbContext(typeof(AppDbContext))] attribute. EF only discovers
///     migrations that have BOTH [Migration] and [DbContext] for the
///     context being migrated, so this migration was silently skipped by
///     MigrateAsync() on startup — no error, no column. Every query that
///     touches failure_types (the Dashboard's charts, all six Reports
///     tables, the tickets list, the failure-type settings page) then
///     failed with 'column f."Category" does not exist' -> HTTP 500.
///  2. It rewrote every existing tickets."Category" value with a
///     remap (0->2, 1->0, 2->1). tickets.Category is an unrelated,
///     already-correct column; that UPDATE would have scrambled the
///     category of every historical ticket. Removed.
///
/// Written with IF NOT EXISTS so it is safe on databases where the column
/// was already added by hand while the app was broken.
/// </summary>
public partial class AddFailureTypeCategories : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            ALTER TABLE ""failure_types""
            ADD COLUMN IF NOT EXISTS ""Category"" integer NOT NULL DEFAULT 0;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"ALTER TABLE ""failure_types"" DROP COLUMN IF EXISTS ""Category"";");
    }
}
