using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <summary>
    /// Single migration covering the remaining Priority 1 backend items:
    ///
    ///  - agreement_types: IsTrainingItem / IsRequiredForCompletion flags,
    ///    both defaulting false so every existing row (including the seeded
    ///    Support/Training rows) stays exactly as before until an Admin
    ///    opts a type in via the new admin toggles.
    ///  - failure_types: optional RequiredSpecialization (free text,
    ///    max 100) driving specialty-aware ticket auto-assignment. Null on
    ///    every existing row — no specialty restriction, same behavior as
    ///    before this column existed.
    ///  - tickets: ItSupportContact (contact snapshot taken at submission),
    ///    RequiredSpecialization (specialty snapshot taken at submission),
    ///    CompletedAt / CompletedByEmployeeId / WorkingMinutesToComplete
    ///    (technician completion stamps, written once when a ticket is
    ///    marked Resolved). All nullable — every existing ticket simply has
    ///    nulls here, which is the correct "we don't know, this predates
    ///    the field" state, not a data-loss concern.
    ///  - maintenance_records: optional ClientId / SystemProductId /
    ///    TicketId links plus a {ClientId, Date} index, powering the new
    ///    client- and system-product-scoped maintenance history views.
    ///    Nullable so historical rows logged before this feature existed
    ///    are preserved rather than orphaned or deleted.
    /// </summary>
    public partial class AddPriority1CompletionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- agreement_types ---

            migrationBuilder.AddColumn<bool>(
                name: "IsTrainingItem",
                table: "agreement_types",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRequiredForCompletion",
                table: "agreement_types",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // --- failure_types ---

            migrationBuilder.AddColumn<string>(
                name: "RequiredSpecialization",
                table: "failure_types",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // --- tickets ---

            migrationBuilder.AddColumn<string>(
                name: "ItSupportContact",
                table: "tickets",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredSpecialization",
                table: "tickets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompletedByEmployeeId",
                table: "tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkingMinutesToComplete",
                table: "tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tickets_CompletedByEmployeeId",
                table: "tickets",
                column: "CompletedByEmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_tickets_employees_CompletedByEmployeeId",
                table: "tickets",
                column: "CompletedByEmployeeId",
                principalTable: "employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // --- maintenance_records ---

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "maintenance_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SystemProductId",
                table: "maintenance_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TicketId",
                table: "maintenance_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_records_SystemProductId",
                table: "maintenance_records",
                column: "SystemProductId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_records_TicketId",
                table: "maintenance_records",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_records_ClientId_Date",
                table: "maintenance_records",
                columns: new[] { "ClientId", "Date" });

            migrationBuilder.AddForeignKey(
                name: "FK_maintenance_records_clients_ClientId",
                table: "maintenance_records",
                column: "ClientId",
                principalTable: "clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_maintenance_records_system_products_SystemProductId",
                table: "maintenance_records",
                column: "SystemProductId",
                principalTable: "system_products",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_maintenance_records_tickets_TicketId",
                table: "maintenance_records",
                column: "TicketId",
                principalTable: "tickets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // --- maintenance_records ---

            migrationBuilder.DropForeignKey(
                name: "FK_maintenance_records_clients_ClientId",
                table: "maintenance_records");

            migrationBuilder.DropForeignKey(
                name: "FK_maintenance_records_system_products_SystemProductId",
                table: "maintenance_records");

            migrationBuilder.DropForeignKey(
                name: "FK_maintenance_records_tickets_TicketId",
                table: "maintenance_records");

            migrationBuilder.DropIndex(
                name: "IX_maintenance_records_SystemProductId",
                table: "maintenance_records");

            migrationBuilder.DropIndex(
                name: "IX_maintenance_records_TicketId",
                table: "maintenance_records");

            migrationBuilder.DropIndex(
                name: "IX_maintenance_records_ClientId_Date",
                table: "maintenance_records");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "maintenance_records");

            migrationBuilder.DropColumn(
                name: "SystemProductId",
                table: "maintenance_records");

            migrationBuilder.DropColumn(
                name: "TicketId",
                table: "maintenance_records");

            // --- tickets ---

            migrationBuilder.DropForeignKey(
                name: "FK_tickets_employees_CompletedByEmployeeId",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "IX_tickets_CompletedByEmployeeId",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "ItSupportContact",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "RequiredSpecialization",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "CompletedByEmployeeId",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "WorkingMinutesToComplete",
                table: "tickets");

            // --- failure_types ---

            migrationBuilder.DropColumn(
                name: "RequiredSpecialization",
                table: "failure_types");

            // --- agreement_types ---

            migrationBuilder.DropColumn(
                name: "IsTrainingItem",
                table: "agreement_types");

            migrationBuilder.DropColumn(
                name: "IsRequiredForCompletion",
                table: "agreement_types");
        }
    }
}
