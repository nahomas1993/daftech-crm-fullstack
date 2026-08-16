using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <summary>
    /// No-op migration (xmin is a Postgres system column already present
    /// on every row — no DDL needed). Marks Ticket.xmin as an EF Core
    /// concurrency token; the actual runtime configuration lives in
    /// TicketConfiguration.UseXminAsConcurrencyToken (see
    /// Persistence/Configurations/EntityConfigurations.cs), applied via
    /// AppDbContext.OnModelCreating's ApplyConfigurationsFromAssembly.
    /// This migration file exists only so the EF Core migration history
    /// stays consistent with CurrentModel.cs, which also reflects this
    /// token. TicketService.UpdateStatusAsync does treat
    /// DbUpdateConcurrencyException as a real conflict signal (409) — see
    /// its doc comment for what changed there.
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
