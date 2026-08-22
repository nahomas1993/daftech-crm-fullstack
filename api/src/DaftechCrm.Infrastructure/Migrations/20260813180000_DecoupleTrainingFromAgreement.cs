using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <summary>
    /// Training now belongs to the Client directly, not to an Agreement —
    /// training happens (and must finish) BEFORE any support agreement can
    /// be signed, so a training row can no longer require an AgreementId.
    /// SignDate goes back to being admin-entered and required: creating an
    /// Agreement now IS the signing act (see AgreementService.CreateAsync),
    /// which is only allowed once the client has a training with EndDate
    /// set. This intentionally reverses the "SignDate is derived" model
    /// from the previous migration — that model let a support agreement
    /// exist before training had happened at all, which is the behavior
    /// this migration corrects.
    /// </summary>
    public partial class DecoupleTrainingFromAgreement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "agreement_trainings",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            // Backfill each existing training's ClientId from its (until now
            // required) parent agreement, before AgreementId is loosened to
            // nullable and the real ClientId FK is enforced below.
            migrationBuilder.Sql(@"
                UPDATE agreement_trainings t
                SET ""ClientId"" = a.""ClientId""
                FROM agreements a
                WHERE t.""AgreementId"" = a.""Id"";
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_agreement_trainings_agreements_AgreementId",
                table: "agreement_trainings");

            migrationBuilder.AlterColumn<Guid>(
                name: "AgreementId",
                table: "agreement_trainings",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_agreement_trainings_ClientId",
                table: "agreement_trainings",
                column: "ClientId");

            migrationBuilder.AddForeignKey(
                name: "FK_agreement_trainings_clients_ClientId",
                table: "agreement_trainings",
                column: "ClientId",
                principalTable: "clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Deleting an agreement must no longer delete its trainings —
            // training history belongs to the client and outlives any one
            // agreement. Re-add as SetNull instead of Cascade.
            migrationBuilder.AddForeignKey(
                name: "FK_agreement_trainings_agreements_AgreementId",
                table: "agreement_trainings",
                column: "AgreementId",
                principalTable: "agreements",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // SignDate goes back to admin-entered and required. Existing
            // agreements already have a SignDate from the old derivation
            // logic (backfilled by the previous migration) in the common
            // case; any that are still null (agreement existed with no
            // training end date recorded) fall back to today so the column
            // can be made NOT NULL — this only affects pre-existing demo/
            // dev data, not new agreements going forward.
            migrationBuilder.Sql(@"
                UPDATE agreements
                SET ""SignDate"" = CURRENT_DATE
                WHERE ""SignDate"" IS NULL;
            ");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateOnly>(
                name: "SignDate",
                table: "agreements",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.DropForeignKey(
                name: "FK_agreement_trainings_agreements_AgreementId",
                table: "agreement_trainings");

            migrationBuilder.DropForeignKey(
                name: "FK_agreement_trainings_clients_ClientId",
                table: "agreement_trainings");

            migrationBuilder.DropIndex(
                name: "IX_agreement_trainings_ClientId",
                table: "agreement_trainings");

            // Any training left with no AgreementId can't be restored to the
            // old required-AgreementId shape, so it's dropped going back —
            // that data simply didn't exist under the old model.
            migrationBuilder.Sql(@"DELETE FROM agreement_trainings WHERE ""AgreementId"" IS NULL;");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "agreement_trainings");

            migrationBuilder.AlterColumn<Guid>(
                name: "AgreementId",
                table: "agreement_trainings",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_agreement_trainings_agreements_AgreementId",
                table: "agreement_trainings",
                column: "AgreementId",
                principalTable: "agreements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
