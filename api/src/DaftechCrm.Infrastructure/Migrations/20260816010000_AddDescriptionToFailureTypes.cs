using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <summary>
    /// Adds an optional free-text Description to failure_types — shown in
    /// the admin Settings → Failure Types &amp; SLA list alongside the name
    /// and expected resolution duration. Nullable with no default, so
    /// existing rows (Network Failure, Software Error, etc.) simply come
    /// back with a null description until an admin edits them; nothing
    /// else about the failure-type/ticket relationship changes.
    /// </summary>
    public partial class AddDescriptionToFailureTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "failure_types",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "failure_types");
        }
    }
}
