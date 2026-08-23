using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <summary>
    /// Moves training off Agreement/TrainingSession entirely and onto
    /// SystemProduct directly — see SystemProduct.TrainingAssignments/
    /// TrainingRecords/TrainingCompletionStatus. Training is no longer a
    /// signed document with a single reviewable submission per trainer;
    /// it's a roster of assigned trainers plus an open-ended log of
    /// sessions actually conducted, with a one-click Admin "mark
    /// Completed" decision on the SystemProduct as a whole.
    ///
    /// Data migration: for each training_sessions row (the prior model —
    /// one per Training-type Agreement, one-to-one), the owning
    /// SystemProduct's TrainingCompletionStatus is backfilled from that
    /// session's old CompletionStatus (FollowUpRequired collapses to
    /// InProgress, since the new model has no equivalent state — Admin
    /// simply hasn't marked it Completed yet). Every training_assignments
    /// row under that session becomes a roster entry on the SystemProduct
    /// (deduplicated — the old model didn't allow the same trainer twice
    /// on one session, so this only matters if a system/product had more
    /// than one Training agreement historically). Any prior work
    /// description/file on an old assignment becomes a single
    /// training_records row, dated from the old SubmittedAt (or AssignedAt
    /// if never submitted) — so no prior trainer write-up is silently
    /// lost, it just becomes a log entry rather than a review-pending
    /// item. Training-type agreements themselves are left in place (still
    /// visible in Agreement history) since Agreement rows are never
    /// deleted by this migration — only training_sessions is dropped.
    /// </summary>
    public partial class MoveTrainingToSystemProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TrainingCompletionStatus",
                table: "system_products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "training_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainerEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    FileStorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_training_records_system_products_SystemProductId",
                        column: x => x.SystemProductId,
                        principalTable: "system_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_training_records_employees_TrainerEmployeeId",
                        column: x => x.TrainerEmployeeId,
                        principalTable: "employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(name: "IX_training_records_SystemProductId", table: "training_records", column: "SystemProductId");
            migrationBuilder.CreateIndex(name: "IX_training_records_TrainerEmployeeId", table: "training_records", column: "TrainerEmployeeId");

            // Backfill SystemProduct.TrainingCompletionStatus from the old
            // per-session status, via the Training agreement that owned
            // each training_sessions row. CompletionStatus values in the
            // old model: 0=NotStarted, 1=InProgress, 2=Completed,
            // 3=FollowUpRequired -> collapses to 1 (InProgress) here, since
            // the new model tracks "needs a follow-up" informally through
            // more TrainingRecords rather than as a status value.
            migrationBuilder.Sql(@"
                UPDATE system_products sp
                SET ""TrainingCompletionStatus"" = CASE
                    WHEN ts.""CompletionStatus"" = 2 THEN 2
                    WHEN ts.""CompletionStatus"" IN (1, 3) THEN 1
                    ELSE 0
                END
                FROM training_sessions ts
                JOIN agreements a ON a.""Id"" = ts.""AgreementId""
                WHERE a.""SystemProductId"" = sp.""Id"";
            ");

            // Fold the old per-item work description/file into a
            // one-off training_records row, before training_assignments is
            // restructured below — so nothing a trainer already wrote up is
            // lost, it's just no longer "pending review", it's history.
            migrationBuilder.Sql(@"
                INSERT INTO training_records (
                    ""Id"", ""SystemProductId"", ""TrainerEmployeeId"", ""TrainingDate"", ""Description"",
                    ""FileStorageKey"", ""FileName"", ""CreatedAt""
                )
                SELECT
                    gen_random_uuid(),
                    a.""SystemProductId"",
                    ta.""TrainerEmployeeId"",
                    COALESCE(ta.""SubmittedAt""::date, ta.""AssignedAt""::date),
                    ta.""WorkDescription"",
                    ta.""FileStorageKey"",
                    ta.""FileName"",
                    COALESCE(ta.""SubmittedAt"", ta.""AssignedAt"")
                FROM training_assignments ta
                JOIN training_sessions ts ON ts.""AgreementId"" = ta.""TrainingSessionId""
                JOIN agreements a ON a.""Id"" = ts.""AgreementId""
                WHERE ta.""WorkDescription"" IS NOT NULL AND ta.""WorkDescription"" != '';
            ");

            // Deliberately created BEFORE dropping the old
            // training_assignments table below — the same table name is
            // reused for the new roster shape, so the old tables are
            // renamed out of the way (not dropped yet) until after the new
            // training_assignments table exists and the roster-restore
            // insert below has read from them.
            migrationBuilder.DropForeignKey(
                name: "FK_training_assignments_employees_TrainerEmployeeId",
                table: "training_assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_training_assignments_training_sessions_TrainingSessionId",
                table: "training_assignments");

            migrationBuilder.RenameTable(name: "training_assignments", newName: "training_assignments_old");
            migrationBuilder.RenameTable(name: "training_sessions", newName: "training_sessions_old");

            // Postgres does not rename constraints along with their table —
            // the old primary keys keep their original names
            // (PK_training_assignments / PK_training_sessions) even after
            // the RenameTable calls above, which would collide with the
            // same names used below for the freshly created
            // training_assignments table. Renamed explicitly to avoid that.
            migrationBuilder.Sql(@"ALTER TABLE training_assignments_old RENAME CONSTRAINT ""PK_training_assignments"" TO ""PK_training_assignments_old"";");
            migrationBuilder.Sql(@"ALTER TABLE training_sessions_old RENAME CONSTRAINT ""PK_training_sessions"" TO ""PK_training_sessions_old"";");
            // Same story for indexes — RenameTable doesn't rename them either.
            migrationBuilder.Sql(@"ALTER INDEX ""IX_training_assignments_TrainerEmployeeId"" RENAME TO ""IX_training_assignments_TrainerEmployeeId_old"";");

            migrationBuilder.CreateTable(
                name: "training_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainerEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_training_assignments_system_products_SystemProductId",
                        column: x => x.SystemProductId,
                        principalTable: "system_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_training_assignments_employees_TrainerEmployeeId",
                        column: x => x.TrainerEmployeeId,
                        principalTable: "employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(name: "IX_training_assignments_SystemProductId", table: "training_assignments", column: "SystemProductId");
            migrationBuilder.CreateIndex(name: "IX_training_assignments_TrainerEmployeeId", table: "training_assignments", column: "TrainerEmployeeId");
            migrationBuilder.CreateIndex(
                name: "IX_training_assignments_SystemProductId_TrainerEmployeeId",
                table: "training_assignments",
                columns: new[] { "SystemProductId", "TrainerEmployeeId" },
                unique: true);

            // Restore the training roster from the old assignments, now
            // against SystemProductId directly. ON CONFLICT DO NOTHING
            // guards the new unique (SystemProductId, TrainerEmployeeId)
            // constraint in the (unlikely) case a system/product had the
            // same trainer on more than one historical Training agreement.
            migrationBuilder.Sql(@"
                INSERT INTO training_assignments (""Id"", ""SystemProductId"", ""TrainerEmployeeId"", ""AssignedAt"")
                SELECT gen_random_uuid(), a.""SystemProductId"", ta_old.""TrainerEmployeeId"", ta_old.""AssignedAt""
                FROM (
                    SELECT DISTINCT ON (a.""SystemProductId"", ta.""TrainerEmployeeId"")
                        a.""SystemProductId"", ta.""TrainerEmployeeId"", ta.""AssignedAt"", a.""Id"" AS agreement_id
                    FROM training_assignments_old ta
                    JOIN training_sessions_old ts ON ts.""AgreementId"" = ta.""TrainingSessionId""
                    JOIN agreements a ON a.""Id"" = ts.""AgreementId""
                    ORDER BY a.""SystemProductId"", ta.""TrainerEmployeeId"", ta.""AssignedAt"" ASC
                ) ta_old
                JOIN agreements a ON a.""Id"" = ta_old.agreement_id
                ON CONFLICT DO NOTHING;
            ");

            // The _old tables have now been fully read from (both Sql()
            // fold-ins above ran before the rename, against their original
            // names, and the roster-restore just above read the renamed
            // copies) — safe to drop for good.
            migrationBuilder.DropTable(name: "training_assignments_old");
            migrationBuilder.DropTable(name: "training_sessions_old");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort reversal: recreates the old shape (empty), but
            // does not attempt to reconstruct training_sessions/old
            // training_assignments rows from the new SystemProduct-level
            // data — that mapping isn't invertible (many TrainingRecords
            // and a roster don't map back to one submission/review per
            // trainer). Reverting this migration is a deliberate feature
            // rollback, not a lossless undo.
            migrationBuilder.DropForeignKey(
                name: "FK_training_assignments_system_products_SystemProductId",
                table: "training_assignments");
            migrationBuilder.DropForeignKey(
                name: "FK_training_assignments_employees_TrainerEmployeeId",
                table: "training_assignments");
            migrationBuilder.DropTable(name: "training_assignments");
            migrationBuilder.DropTable(name: "training_records");

            migrationBuilder.DropColumn(name: "TrainingCompletionStatus", table: "system_products");

            migrationBuilder.CreateTable(
                name: "training_sessions",
                columns: table => new
                {
                    AgreementId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Participants = table.Column<string>(type: "text", nullable: true),
                    Attendance = table.Column<string>(type: "text", nullable: true),
                    TopicsCovered = table.Column<string>(type: "text", nullable: true),
                    IssuesOrQuestions = table.Column<string>(type: "text", nullable: true),
                    TrainerComments = table.Column<string>(type: "text", nullable: true),
                    ClientRepresentativeConfirmation = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ClientRepresentativeComments = table.Column<string>(type: "text", nullable: true),
                    CompletionStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    FollowUpRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    FollowUpNotes = table.Column<string>(type: "text", nullable: true),
                    ScanStorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ScanFileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_sessions", x => x.AgreementId);
                    table.ForeignKey(
                        name: "FK_training_sessions_agreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "agreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateIndex(name: "IX_training_assignments_TrainingSessionId", table: "training_assignments", column: "TrainingSessionId");
            migrationBuilder.CreateIndex(name: "IX_training_assignments_TrainerEmployeeId", table: "training_assignments", column: "TrainerEmployeeId");
        }
    }
}
