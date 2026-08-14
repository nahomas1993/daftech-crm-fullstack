using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <summary>
    /// Replaces the single set of Training* columns on agreements with a
    /// proper agreement_trainings child table (an agreement can now have
    /// multiple trainings), and makes SignDate nullable since it's now
    /// derived from the latest training's EndDate rather than admin-entered
    /// — it stays null until at least one training has an end date.
    /// </summary>
    public partial class AddAgreementTrainingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agreement_trainings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ScanStorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ScanFileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agreement_trainings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agreement_trainings_agreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "agreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agreement_trainings_AgreementId",
                table: "agreement_trainings",
                column: "AgreementId");

            // Migrate any existing single-training data on agreements into
            // the new child table before the old columns are dropped.
            migrationBuilder.Sql(@"
                INSERT INTO agreement_trainings (""Id"", ""AgreementId"", ""Description"", ""StartDate"", ""EndDate"", ""ScanStorageKey"", ""ScanFileName"")
                SELECT gen_random_uuid(), ""Id"", ""TrainingDescription"", ""TrainingStartDate"", ""TrainingEndDate"", ""TrainingScanStorageKey"", ""TrainingScanFileName""
                FROM agreements
                WHERE ""TrainingDescription"" IS NOT NULL
                   OR ""TrainingStartDate"" IS NOT NULL
                   OR ""TrainingEndDate"" IS NOT NULL
                   OR ""TrainingScanStorageKey"" IS NOT NULL;
            ");

            migrationBuilder.DropColumn(name: "TrainingScanStorageKey", table: "agreements");
            migrationBuilder.DropColumn(name: "TrainingScanFileName", table: "agreements");
            migrationBuilder.DropColumn(name: "TrainingDescription", table: "agreements");
            migrationBuilder.DropColumn(name: "TrainingStartDate", table: "agreements");
            migrationBuilder.DropColumn(name: "TrainingEndDate", table: "agreements");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "SignDate",
                table: "agreements",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            // Backfill SignDate for existing agreements from the migrated
            // training data (latest EndDate), matching Agreement.RecalculateSignDate.
            migrationBuilder.Sql(@"
                UPDATE agreements a
                SET ""SignDate"" = sub.max_end
                FROM (
                    SELECT ""AgreementId"", MAX(""EndDate"") AS max_end
                    FROM agreement_trainings
                    WHERE ""EndDate"" IS NOT NULL
                    GROUP BY ""AgreementId""
                ) sub
                WHERE a.""Id"" = sub.""AgreementId"";
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrainingScanStorageKey",
                table: "agreements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrainingScanFileName",
                table: "agreements",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrainingDescription",
                table: "agreements",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TrainingStartDate",
                table: "agreements",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TrainingEndDate",
                table: "agreements",
                type: "date",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE agreements a
                SET
                    ""TrainingDescription"" = t.""Description"",
                    ""TrainingStartDate"" = t.""StartDate"",
                    ""TrainingEndDate"" = t.""EndDate"",
                    ""TrainingScanStorageKey"" = t.""ScanStorageKey"",
                    ""TrainingScanFileName"" = t.""ScanFileName""
                FROM (
                    SELECT DISTINCT ON (""AgreementId"") *
                    FROM agreement_trainings
                    ORDER BY ""AgreementId"", ""EndDate"" DESC NULLS LAST
                ) t
                WHERE a.""Id"" = t.""AgreementId"";
            ");

            migrationBuilder.DropTable(name: "agreement_trainings");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "SignDate",
                table: "agreements",
                type: "date",
                nullable: false,
                defaultValue: default(DateOnly),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);
        }
    }
}
