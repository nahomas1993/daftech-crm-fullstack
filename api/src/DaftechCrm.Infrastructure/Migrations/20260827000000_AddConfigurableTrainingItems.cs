using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <summary>
    /// Makes the training checklist admin-configurable. TrainingRecord now
    /// points at an AgreementType (the same admin-managed lookup table
    /// already used for Support agreements — e.g. Admin adds "Attendance")
    /// so the set of named items a Trainer works through is no longer
    /// hardcoded. Also adds optional StartDateTime/EndDateTime so a
    /// same-day training that runs a couple of hours can record the exact
    /// start/end time instead of just a date, alongside SystemProduct's
    /// TrainingSubmittedAt for the Trainer's own "done, ready for Admin"
    /// signal.
    ///
    /// Existing training_records rows have no AgreementTypeId to backfill
    /// from — this migration points every existing row at the seeded
    /// "Training" AgreementType (see AgreementTypeNames.Training) as a
    /// reasonable default before making the column non-nullable, so no
    /// history is lost or orphaned.
    /// </summary>
    public partial class AddConfigurableTrainingItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TrainingSubmittedAt",
                table: "system_products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AgreementTypeId",
                table: "training_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartDateTime",
                table: "training_records",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndDateTime",
                table: "training_records",
                type: "timestamp with time zone",
                nullable: true);

            // Backfill existing rows to the seeded "Training" agreement
            // type before the column is made required.
            migrationBuilder.Sql(
                "UPDATE training_records SET \"AgreementTypeId\" = " +
                "(SELECT \"Id\" FROM agreement_types WHERE \"Name\" = 'Training' LIMIT 1) " +
                "WHERE \"AgreementTypeId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "AgreementTypeId",
                table: "training_records",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_training_records_AgreementTypeId",
                table: "training_records",
                column: "AgreementTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_training_records_agreement_types_AgreementTypeId",
                table: "training_records",
                column: "AgreementTypeId",
                principalTable: "agreement_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_training_records_agreement_types_AgreementTypeId",
                table: "training_records");

            migrationBuilder.DropIndex(
                name: "IX_training_records_AgreementTypeId",
                table: "training_records");

            migrationBuilder.DropColumn(
                name: "AgreementTypeId",
                table: "training_records");

            migrationBuilder.DropColumn(
                name: "StartDateTime",
                table: "training_records");

            migrationBuilder.DropColumn(
                name: "EndDateTime",
                table: "training_records");

            migrationBuilder.DropColumn(
                name: "TrainingSubmittedAt",
                table: "system_products");
        }
    }
}
