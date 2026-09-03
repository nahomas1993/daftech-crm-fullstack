using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations;

/// <summary>
/// Makes training_records.TrainerEmployeeId nullable and adds
/// TrainerNameFreeText, so the CSV bulk import (see ClientImportService)
/// can log a historical training session transcribed from a paper record
/// that either left the trainer blank or named someone who isn't a
/// matching Trainer employee — every other creation path
/// (TrainingRecordService.CreateAsync/AdminCreateAsync) still always sets
/// TrainerEmployeeId, this only relaxes the column itself.
///
/// The existing FK constraint (ON DELETE RESTRICT) is left as-is — a
/// nullable FK column still enforces referential integrity for rows where
/// it IS set, Postgres just skips the check when it's NULL, which is
/// exactly the intended behavior here.
///
/// Written with IF EXISTS / IF NOT EXISTS so it's safe to re-run against a
/// database that already has some of these changes applied by hand.
/// </summary>
public partial class MakeTrainingRecordTrainerOptional : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            ALTER TABLE ""training_records"" ALTER COLUMN ""TrainerEmployeeId"" DROP NOT NULL;
        ");

        migrationBuilder.Sql(@"
            ALTER TABLE ""training_records""
            ADD COLUMN IF NOT EXISTS ""TrainerNameFreeText"" character varying(200) NULL;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"ALTER TABLE ""training_records"" DROP COLUMN IF EXISTS ""TrainerNameFreeText"";");

        // Reinstating NOT NULL would fail if any row was left with a null
        // TrainerEmployeeId by the import feature this migration enables —
        // those rows would need a real employee assigned (or deleting)
        // before this Down migration could run.
        migrationBuilder.Sql(@"
            ALTER TABLE ""training_records"" ALTER COLUMN ""TrainerEmployeeId"" SET NOT NULL;
        ");
    }
}
