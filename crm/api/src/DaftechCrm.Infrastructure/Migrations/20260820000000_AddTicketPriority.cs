using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <summary>
    /// Adds Ticket.Priority (Low/Medium/High, default Medium — see
    /// TicketPriority) — introduced for the workload-aware Trainer
    /// assignment feature's "high-priority tickets" workload dimension
    /// (see TrainerWorkloadService), but usable as a general ticket field
    /// going forward. Existing tickets all default to Medium (1) since
    /// there was no prior priority concept to backfill from.
    /// </summary>
    public partial class AddTicketPriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "tickets",
                type: "integer",
                nullable: false,
                defaultValue: 1); // TicketPriority.Medium

            migrationBuilder.CreateIndex(
                name: "IX_tickets_Priority",
                table: "tickets",
                column: "Priority");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_tickets_Priority", table: "tickets");
            migrationBuilder.DropColumn(name: "Priority", table: "tickets");
        }
    }
}
