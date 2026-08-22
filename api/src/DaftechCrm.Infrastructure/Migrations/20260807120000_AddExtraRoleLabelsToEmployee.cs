using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExtraRoleLabelsToEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtraRoleLabels",
                table: "employees",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtraRoleLabels",
                table: "employees");
        }
    }
}
