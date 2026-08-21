using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <summary>
    /// Introduces the SystemProduct and AgreementType layers:
    ///
    ///   Client -> SystemProduct -> Agreement -> AgreementType
    ///
    /// replacing the old flat Client -> Agreement model, and renames/
    /// restructures agreement_trainings into training_sessions, one-to-one
    /// with a Training-type Agreement instead of standing independently
    /// against a Client.
    ///
    /// DATA MIGRATION (never destroys existing rows):
    ///  1. agreement_types gets its two seeded rows (Support/Training) —
    ///     also (re-)ensured on every app startup by
    ///     DependencyInjection.EnsureCoreAgreementTypesAsync, so this insert
    ///     is belt-and-suspenders for environments that migrate without
    ///     immediately restarting the app.
    ///  2. Exactly one system_products row is created per DISTINCT client
    ///     that has at least one existing agreement or training — named
    ///     "General" since the old model had no per-system distinction to
    ///     preserve. A client with zero agreements/trainings gets none (an
    ///     Admin creates their first SystemProduct going forward via the
    ///     new UI).
    ///  3. Every existing agreements row is re-pointed at that client's new
    ///     "General" SystemProduct and given AgreementTypeId = Support
    ///     (this preserves the old model's implicit meaning: every existing
    ///     agreement WAS a support agreement, since Training wasn't its own
    ///     agreement type before this migration).
    ///  4. Every existing agreement_trainings row becomes a NEW Training-
    ///     type agreement (own DocumentNumber, same "General" SystemProduct)
    ///     plus its corresponding training_sessions row, carrying over
    ///     Description into TopicsCovered, StartDate/EndDate, and the scan
    ///     file reference. A training with EndDate set is marked Completed;
    ///     otherwise InProgress. This is additive only — it creates new
    ///     Agreement rows, it does not alter or remove the Support
    ///     agreements from step 3.
    ///
    /// Nothing here overwrites or deletes an existing agreement row's own
    /// data (DocumentNumber, dates, status, tier, scanned file) — only its
    /// ClientId column is replaced by SystemProductId, and a new
    /// AgreementTypeId column is populated.
    /// </summary>
    public partial class AddSystemProductAndAgreementType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- 1. agreement_types ---
            migrationBuilder.CreateTable(
                name: "agreement_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsSystemDefined = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agreement_types", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agreement_types_Name",
                table: "agreement_types",
                column: "Name",
                unique: true);

            migrationBuilder.Sql(@"
                INSERT INTO agreement_types (""Id"", ""Name"", ""Description"", ""IsSystemDefined"")
                VALUES
                    ('66666666-0000-0000-0000-000000000001', 'Support', 'Ongoing technical support for a client''s system/product.', true),
                    ('66666666-0000-0000-0000-000000000002', 'Training', 'Client staff training on a system/product — must be completed before a Support agreement can be signed for the same system/product.', true)
                ON CONFLICT (""Id"") DO NOTHING;
            ");

            // --- 2. system_products ---
            migrationBuilder.CreateTable(
                name: "system_products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DeploymentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_system_products_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_system_products_ClientId",
                table: "system_products",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_system_products_ReferenceNumber",
                table: "system_products",
                column: "ReferenceNumber",
                unique: true);

            // Backfill: one "General" SystemProduct per client that has at
            // least one existing agreement or training. Reference numbers
            // are generated deterministically here (DAF-SYS-MIGR-#####) so
            // they don't collide with ReferenceNumberService's own
            // DAF-SYS-YYYY-#### sequence going forward.
            migrationBuilder.Sql(@"
                INSERT INTO system_products (""Id"", ""ClientId"", ""ReferenceNumber"", ""Name"", ""Description"", ""IsDeleted"")
                SELECT gen_random_uuid(), c.""Id"",
                       'DAF-SYS-MIGR-' || LPAD((ROW_NUMBER() OVER (ORDER BY c.""Id""))::text, 5, '0'),
                       'General', 'Auto-created during the System/Product migration to hold this client''s pre-existing agreements and trainings.', false
                FROM clients c
                WHERE c.""Id"" IN (
                    SELECT ""ClientId"" FROM agreements
                    UNION
                    SELECT ""ClientId"" FROM agreement_trainings
                );
            ");

            // --- 3. agreements: SystemProductId + AgreementTypeId replace ClientId ---
            migrationBuilder.AddColumn<Guid>(
                name: "SystemProductId",
                table: "agreements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AgreementTypeId",
                table: "agreements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "agreements",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE agreements a
                SET ""SystemProductId"" = sp.""Id"",
                    ""AgreementTypeId"" = '66666666-0000-0000-0000-000000000001'
                FROM system_products sp
                WHERE sp.""ClientId"" = a.""ClientId"" AND sp.""Name"" = 'General';
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_agreements_clients_ClientId",
                table: "agreements");

            migrationBuilder.DropIndex(
                name: "IX_agreements_ClientId",
                table: "agreements");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "agreements");

            migrationBuilder.AlterColumn<Guid>(
                name: "SystemProductId",
                table: "agreements",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AgreementTypeId",
                table: "agreements",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_agreements_SystemProductId",
                table: "agreements",
                column: "SystemProductId");

            migrationBuilder.CreateIndex(
                name: "IX_agreements_AgreementTypeId",
                table: "agreements",
                column: "AgreementTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_agreements_system_products_SystemProductId",
                table: "agreements",
                column: "SystemProductId",
                principalTable: "system_products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_agreements_agreement_types_AgreementTypeId",
                table: "agreements",
                column: "AgreementTypeId",
                principalTable: "agreement_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // --- 4. training_sessions replaces agreement_trainings ---
            migrationBuilder.CreateTable(
                name: "training_sessions",
                columns: table => new
                {
                    AgreementId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainerEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.ForeignKey(
                        name: "FK_training_sessions_employees_TrainerEmployeeId",
                        column: x => x.TrainerEmployeeId,
                        principalTable: "employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_training_sessions_TrainerEmployeeId",
                table: "training_sessions",
                column: "TrainerEmployeeId");

            // Each existing agreement_trainings row becomes a brand-new
            // Training-type Agreement (own DocumentNumber, same client's
            // "General" SystemProduct) plus its training_sessions row.
            // SupportWindowMonths=0 and ExpiryDate=EndDate (or SignDate if
            // no EndDate yet) since a training agreement doesn't carry a
            // support window the way a Support agreement does.
            //
            // A temporary column tracks which old agreement_trainings row
            // each new agreement came from, so the training_sessions insert
            // below can join back precisely — matching on data values
            // (dates/description) would risk misattributing rows for the
            // same client with identical start dates.
            migrationBuilder.AddColumn<Guid>(
                name: "MigratedFromTrainingId",
                table: "agreements",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(@"
                INSERT INTO agreements (
                    ""Id"", ""SystemProductId"", ""AgreementTypeId"", ""DocumentNumber"", ""ScannedFileUrl"",
                    ""AgreementPlace"", ""SignDate"", ""ExpiryDate"", ""SupportWindowMonths"", ""Status"", ""BillingTier"", ""Details"",
                    ""MigratedFromTrainingId""
                )
                SELECT
                    gen_random_uuid(), sp.""Id"", '66666666-0000-0000-0000-000000000002',
                    'DAF-AGR-MIGR-' || LPAD((ROW_NUMBER() OVER (ORDER BY t.""Id""))::text, 5, '0'),
                    NULL, 'Migrated from prior training record',
                    COALESCE(t.""StartDate"", CURRENT_DATE), COALESCE(t.""EndDate"", t.""StartDate"", CURRENT_DATE),
                    0, 0, 0, 'Migrated from the pre-restructure training record for this client.',
                    t.""Id""
                FROM agreement_trainings t
                JOIN system_products sp ON sp.""ClientId"" = t.""ClientId"" AND sp.""Name"" = 'General';

                INSERT INTO training_sessions (
                    ""AgreementId"", ""StartDate"", ""EndDate"", ""TopicsCovered"", ""CompletionStatus"", ""ScanStorageKey"", ""ScanFileName""
                )
                SELECT
                    a.""Id"", t.""StartDate"", t.""EndDate"", t.""Description"",
                    CASE WHEN t.""EndDate"" IS NOT NULL THEN 2 ELSE 1 END,
                    t.""ScanStorageKey"", t.""ScanFileName""
                FROM agreements a
                JOIN agreement_trainings t ON t.""Id"" = a.""MigratedFromTrainingId"";
            ");

            migrationBuilder.DropColumn(name: "MigratedFromTrainingId", table: "agreements");

            migrationBuilder.DropTable(name: "agreement_trainings");

            // --- 5. clients.Zone ---
            migrationBuilder.AddColumn<string>(
                name: "Zone",
                table: "clients",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Zone", table: "clients");

            migrationBuilder.CreateTable(
                name: "agreement_trainings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uuid", nullable: true),
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
                        name: "FK_agreement_trainings_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agreement_trainings_agreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "agreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.Sql(@"
                INSERT INTO agreement_trainings (""Id"", ""ClientId"", ""AgreementId"", ""Description"", ""StartDate"", ""EndDate"", ""ScanStorageKey"", ""ScanFileName"")
                SELECT gen_random_uuid(), sp.""ClientId"", NULL, ts.""TopicsCovered"", ts.""StartDate"", ts.""EndDate"", ts.""ScanStorageKey"", ts.""ScanFileName""
                FROM training_sessions ts
                JOIN agreements a ON a.""Id"" = ts.""AgreementId""
                JOIN system_products sp ON sp.""Id"" = a.""SystemProductId"";
            ");

            migrationBuilder.DropTable(name: "training_sessions");

            migrationBuilder.DropForeignKey(name: "FK_agreements_system_products_SystemProductId", table: "agreements");
            migrationBuilder.DropForeignKey(name: "FK_agreements_agreement_types_AgreementTypeId", table: "agreements");
            migrationBuilder.DropIndex(name: "IX_agreements_SystemProductId", table: "agreements");
            migrationBuilder.DropIndex(name: "IX_agreements_AgreementTypeId", table: "agreements");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "agreements",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE agreements a
                SET ""ClientId"" = sp.""ClientId""
                FROM system_products sp
                WHERE sp.""Id"" = a.""SystemProductId""
                  AND a.""AgreementTypeId"" = '66666666-0000-0000-0000-000000000001';
            ");

            // Any agreement that was created as a Training-type row by this
            // migration's Up() (or a genuinely new one since) has no
            // pre-restructure equivalent to roll back to — delete it, its
            // ClientId would otherwise be left null and violate the
            // restored NOT NULL constraint below.
            migrationBuilder.Sql(@"DELETE FROM agreements WHERE ""ClientId"" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "ClientId",
                table: "agreements",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(name: "SystemProductId", table: "agreements");
            migrationBuilder.DropColumn(name: "AgreementTypeId", table: "agreements");
            migrationBuilder.DropColumn(name: "Details", table: "agreements");

            migrationBuilder.CreateIndex(name: "IX_agreements_ClientId", table: "agreements", column: "ClientId");

            migrationBuilder.AddForeignKey(
                name: "FK_agreements_clients_ClientId",
                table: "agreements",
                column: "ClientId",
                principalTable: "clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropTable(name: "system_products");
            migrationBuilder.DropTable(name: "agreement_types");
        }
    }
}
