using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <summary>
    /// Marks Ticket.xmin (Postgres's built-in per-row system column, already
    /// present on every table) as an EF Core concurrency token. This is a
    /// model-only change — xmin is maintained by Postgres itself on every
    /// UPDATE, so no column needs to be added and there is no data to
    /// backfill. From this point on, SaveChangesAsync compares the xmin
    /// value it read against the current one in the database on every
    /// UPDATE/DELETE of a ticket, and throws DbUpdateConcurrencyException
    /// only when the row was genuinely changed by someone else since it was
    /// read — fixing the previous false-positive "updated by someone else"
    /// message, which had no real concurrency detection behind it at all.
    /// </summary>
    public partial class AddXminConcurrencyTokenToTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No DDL: xmin already exists on every Postgres row. This
            // migration exists solely so the EF Core migration history and
            // model snapshot stay consistent with CurrentModel.cs.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No DDL to reverse — see Up().
        }
    }
}
