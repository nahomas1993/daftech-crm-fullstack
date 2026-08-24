using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations;

[Migration("20260824000000_AddFailureTypeCategories")]
public partial class AddFailureTypeCategories : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Category",
            table: "failure_types",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        // Preserve the meaning of the existing categories where possible:
        // old SqlDatabaseError -> Database, Bug -> Frontend, Other -> Backend.
        migrationBuilder.Sql(@"UPDATE ""failure_types"" SET ""Category"" = 0 WHERE ""Category"" = 0;");

        migrationBuilder.Sql(@"UPDATE ""tickets"" SET ""Category"" = CASE ""Category"" WHEN 0 THEN 2 WHEN 1 THEN 0 WHEN 2 THEN 1 ELSE 0 END;");

        migrationBuilder.AlterColumn<int>(
            name: "Category",
            table: "failure_types",
            type: "integer",
            nullable: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Category", table: "failure_types");
    }
}
