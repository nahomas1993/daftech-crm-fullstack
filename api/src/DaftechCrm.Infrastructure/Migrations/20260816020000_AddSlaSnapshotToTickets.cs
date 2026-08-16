using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <summary>
    /// Adds ExpectedResolutionMinutes and ExpectedResolutionBy to tickets —
    /// a snapshot of the assigned FailureType's expected duration taken at
    /// the moment the ticket was assigned, instead of recalculating the SLA
    /// deadline live off FailureType's *current* duration on every read.
    /// Without this, an Admin changing "Network Failure" from 4 hours to 8
    /// hours would retroactively change the deadline of tickets already
    /// assigned under the old duration. Both nullable with no backfill:
    /// existing tickets simply show no SLA deadline until reassigned, which
    /// is safer than fabricating one from today's settings.
    /// </summary>
    public partial class AddSlaSnapshotToTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExpectedResolutionMinutes",
                table: "tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpectedResolutionBy",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedResolutionMinutes",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "ExpectedResolutionBy",
                table: "tickets");
        }
    }
}
