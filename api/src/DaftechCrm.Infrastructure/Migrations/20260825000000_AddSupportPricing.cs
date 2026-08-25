using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <summary>
    /// Support pricing. Each failure type gets its own base price, and a new
    /// support_types table holds the admin-defined support options with the
    /// extra fee each one adds. Tickets remember which support type was
    /// chosen, what the client was quoted, and whether they accepted the
    /// charge. Everything defaults to 0 / null / false, so existing failure
    /// types and tickets stay exactly as they are until an admin sets prices.
    /// </summary>
    public partial class AddSupportPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BasePrice",
                table: "failure_types",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "support_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AdditionalFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_types", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_support_types_Name",
                table: "support_types",
                column: "Name",
                unique: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupportTypeId",
                table: "tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ChargeAmount",
                table: "tickets",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ChargeAcknowledged",
                table: "tickets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_tickets_SupportTypeId",
                table: "tickets",
                column: "SupportTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_tickets_support_types_SupportTypeId",
                table: "tickets",
                column: "SupportTypeId",
                principalTable: "support_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tickets_support_types_SupportTypeId",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "IX_tickets_SupportTypeId",
                table: "tickets");

            migrationBuilder.DropColumn(name: "ChargeAcknowledged", table: "tickets");
            migrationBuilder.DropColumn(name: "ChargeAmount", table: "tickets");
            migrationBuilder.DropColumn(name: "SupportTypeId", table: "tickets");

            migrationBuilder.DropTable(name: "support_types");

            migrationBuilder.DropColumn(name: "BasePrice", table: "failure_types");
        }
    }
}
