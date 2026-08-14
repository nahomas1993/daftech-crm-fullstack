using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFailureTypeIdToTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FailureTypeId",
                table: "tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tickets_FailureTypeId",
                table: "tickets",
                column: "FailureTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_tickets_failure_types_FailureTypeId",
                table: "tickets",
                column: "FailureTypeId",
                principalTable: "failure_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tickets_failure_types_FailureTypeId",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "IX_tickets_FailureTypeId",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "FailureTypeId",
                table: "tickets");
        }
    }
}
