using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <summary>
    /// Replaces TrainingSession's single TrainerEmployeeId with a proper
    /// training_assignments table, one row per Trainer on a session (see
    /// TrainingAssignment) — the workflow now supports several Trainers per
    /// training, each independently submitting their own work for Admin
    /// review before the session as a whole can be marked complete.
    ///
    /// Any existing training_sessions row that already had a
    /// TrainerEmployeeId gets exactly one training_assignments row carrying
    /// that trainer over, so no existing assignment is lost. Its status is
    /// backfilled from the old session-level CompletionStatus: Completed
    /// sessions become an Approved assignment (their EndDate already stood
    /// in as "training is done", so this preserves that); anything else
    /// becomes Assigned, since there's no reliable old data distinguishing
    /// "not yet submitted" from "submitted but never reviewed" under the
    /// old single-status-field model. TrainingSession.CompletionStatus
    /// itself is left as-is by this migration — AgreementService now
    /// derives it going forward from assignment approvals, but a
    /// previously-Completed session stays Completed rather than being
    /// reset to InProgress just because the migration can't fully replay
    /// the old approval history.
    /// </summary>
    public partial class AddTrainingAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "training_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainingSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainerEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WorkDescription = table.Column<string>(type: "text", nullable: true),
                    FileStorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewNotes = table.Column<string>(type: "text", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_training_assignments_training_sessions_TrainingSessionId",
                        column: x => x.TrainingSessionId,
                        principalTable: "training_sessions",
                        principalColumn: "AgreementId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_training_assignments_employees_TrainerEmployeeId",
                        column: x => x.TrainerEmployeeId,
                        principalTable: "employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_training_assignments_TrainingSessionId",
                table: "training_assignments",
                column: "TrainingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_training_assignments_TrainerEmployeeId",
                table: "training_assignments",
                column: "TrainerEmployeeId");

            // Backfill: one training_assignments row per existing
            // training_sessions.TrainerEmployeeId, before that column is
            // dropped below. gen_random_uuid() matches how every other
            // client-assigned Guid key in this schema is generated at the
            // application layer — here at the DB layer since this is a
            // one-off backfill, not a normal insert path.
            migrationBuilder.Sql(@"
                INSERT INTO training_assignments (
                    ""Id"", ""TrainingSessionId"", ""TrainerEmployeeId"", ""AssignedAt"", ""Status""
                )
                SELECT
                    gen_random_uuid(),
                    ts.""AgreementId"",
                    ts.""TrainerEmployeeId"",
                    COALESCE(a.""SignDate""::timestamptz, now()),
                    CASE WHEN ts.""CompletionStatus"" = 2 THEN 2 ELSE 0 END
                FROM training_sessions ts
                JOIN agreements a ON a.""Id"" = ts.""AgreementId""
                WHERE ts.""TrainerEmployeeId"" IS NOT NULL;
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_training_sessions_employees_TrainerEmployeeId",
                table: "training_sessions");

            migrationBuilder.DropIndex(
                name: "IX_training_sessions_TrainerEmployeeId",
                table: "training_sessions");

            migrationBuilder.DropColumn(
                name: "TrainerEmployeeId",
                table: "training_sessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TrainerEmployeeId",
                table: "training_sessions",
                type: "uuid",
                nullable: true);

            // Best-effort restore: pick one trainer per session (the
            // earliest-assigned) back onto the old single-trainer column.
            // Any session with more than one trainer loses everyone but
            // that first pick — reverting this migration is a deliberate
            // feature removal, not just a schema change, so losing the
            // "which extra trainers were on it" detail here is expected.
            migrationBuilder.Sql(@"
                UPDATE training_sessions ts
                SET ""TrainerEmployeeId"" = sub.""TrainerEmployeeId""
                FROM (
                    SELECT DISTINCT ON (""TrainingSessionId"") ""TrainingSessionId"", ""TrainerEmployeeId""
                    FROM training_assignments
                    ORDER BY ""TrainingSessionId"", ""AssignedAt"" ASC
                ) sub
                WHERE ts.""AgreementId"" = sub.""TrainingSessionId"";
            ");

            migrationBuilder.CreateIndex(
                name: "IX_training_sessions_TrainerEmployeeId",
                table: "training_sessions",
                column: "TrainerEmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_training_sessions_employees_TrainerEmployeeId",
                table: "training_sessions",
                column: "TrainerEmployeeId",
                principalTable: "employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.DropTable(
                name: "training_assignments");
        }
    }
}
