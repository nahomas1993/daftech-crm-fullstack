using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations;

/// <summary>
/// Adds FailureType.DefaultPriority (Low/Medium/High, stored as text —
/// same string-conversion style as FailureType.DurationUnit). Every ticket
/// submitted with a given failure type now takes its Priority from this
/// value at submission time (see TicketService.SubmitAsync) instead of a
/// technician or admin setting Priority by hand afterwards on the Tickets
/// page. Existing failure types default to "Medium", matching
/// Ticket.Priority's own existing default, so no ticket's priority meaning
/// changes for rows that predate this column.
///
/// Written with IF NOT EXISTS, matching the existing
/// AddFailureTypeCategories migration's style, so it's safe to run against
/// a database where this column was already added by hand.
/// </summary>
public partial class AddFailureTypeDefaultPriority : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            ALTER TABLE ""failure_types""
            ADD COLUMN IF NOT EXISTS ""DefaultPriority"" character varying(20) NOT NULL DEFAULT 'Medium';
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"ALTER TABLE ""failure_types"" DROP COLUMN IF EXISTS ""DefaultPriority"";");
    }
}
