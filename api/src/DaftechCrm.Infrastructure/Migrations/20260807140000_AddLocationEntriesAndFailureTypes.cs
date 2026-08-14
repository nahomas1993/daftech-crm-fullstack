using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationEntriesAndFailureTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "location_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_location_entries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_location_entries_Type_Name",
                table: "location_entries",
                columns: new[] { "Type", "Name" },
                unique: true);

            migrationBuilder.CreateTable(
                name: "failure_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DurationValue = table.Column<int>(type: "integer", nullable: false),
                    DurationUnit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_failure_types", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_failure_types_Name",
                table: "failure_types",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "location_entries");
            migrationBuilder.DropTable(name: "failure_types");
        }
    }
}
