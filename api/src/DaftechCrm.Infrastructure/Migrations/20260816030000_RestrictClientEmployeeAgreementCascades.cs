using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <summary>
    /// Changes ON DELETE CASCADE to ON DELETE RESTRICT on every foreign key
    /// rooted at Client, Employee, or Agreement — none of which is ever
    /// hard-deleted by any application code path (Client/Employee are
    /// soft-delete only, see ClientService/EmployeeService.DeleteAsync;
    /// Agreement is never deleted at all). Cascade on these edges was
    /// inert today, but misleading — a future hard-delete added without
    /// noticing this would have silently destroyed an entire client's
    /// agreements, tickets, trainings, and survey history, or an
    /// employee's full audit trail (login records, device sessions, time
    /// logs). Restrict makes that fail loudly with a real FK violation
    /// instead. Ticket->TicketAuditEntry and Ticket->SatisfactionSurvey
    /// are left as genuine Cascade — Ticket itself is never soft-deleted,
    /// and a ticket's audit trail/survey response is meaningless without
    /// the ticket, so cascading those two really is the correct behavior
    /// if a ticket is ever hard-deleted.
    ///
    /// IMPORTANT — the constraint names below follow EF Core's default
    /// naming convention (FK_{table}_{principalTable}_{column}), confirmed
    /// against every FK name that appears explicitly elsewhere in this
    /// migration history (e.g. FK_tickets_failure_types_FailureTypeId,
    /// FK_agreement_trainings_clients_ClientId). The FKs touched by *this*
    /// migration, however, were originally created by the very first
    /// migration (20260801000000_InitialCreate), whose real Up() body was
    /// lost and is now an intentional no-op placeholder (see that file's
    /// comments) — so these specific names could not be cross-checked
    /// against another migration file the way the others were. Before
    /// running this against the real production database, verify the
    /// actual constraint names with:
    ///   SELECT conname FROM pg_constraint WHERE contype = 'f'
    ///     AND conrelid::regclass::text IN
    ///     ('agreements','agreement_trainings','device_sessions',
    ///      'login_records','maintenance_records','satisfaction_surveys',
    ///      'tickets','time_logs');
    /// and adjust the names below if they differ.
    /// </summary>
    public partial class RestrictClientEmployeeAgreementCascades : Migration
    {
        private static readonly (string Table, string OldName, string Column, string PrincipalTable)[] Fks = new[]
        {
            ("agreements", "FK_agreements_clients_ClientId", "ClientId", "clients"),
            ("agreement_trainings", "FK_agreement_trainings_clients_ClientId", "ClientId", "clients"),
            ("device_sessions", "FK_device_sessions_employees_EmployeeId", "EmployeeId", "employees"),
            ("login_records", "FK_login_records_employees_EmployeeId", "EmployeeId", "employees"),
            ("maintenance_records", "FK_maintenance_records_employees_PerformedByEmployeeId", "PerformedByEmployeeId", "employees"),
            ("satisfaction_surveys", "FK_satisfaction_surveys_clients_ClientId", "ClientId", "clients"),
            ("tickets", "FK_tickets_agreements_AgreementId", "AgreementId", "agreements"),
            ("tickets", "FK_tickets_clients_ClientId", "ClientId", "clients"),
            ("time_logs", "FK_time_logs_employees_EmployeeId", "EmployeeId", "employees"),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var fk in Fks)
            {
                migrationBuilder.DropForeignKey(name: fk.OldName, table: fk.Table);
                migrationBuilder.AddForeignKey(
                    name: fk.OldName,
                    table: fk.Table,
                    column: fk.Column,
                    principalTable: fk.PrincipalTable,
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var fk in Fks)
            {
                migrationBuilder.DropForeignKey(name: fk.OldName, table: fk.Table);
                migrationBuilder.AddForeignKey(
                    name: fk.OldName,
                    table: fk.Table,
                    column: fk.Column,
                    principalTable: fk.PrincipalTable,
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            }
        }
    }
}
