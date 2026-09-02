using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <summary>
    /// Adds a self-referencing ParentId to location_entries so Region -&gt;
    /// Zone -&gt; Woreda form a real hierarchy (a Zone's ParentId points at
    /// its owning Region; a Woreda's ParentId points at its owning Zone).
    /// City/Specialization/CustomRole rows, and existing Region rows, keep
    /// ParentId null — nothing about them changes. Cascade on delete: removing
    /// a Region removes its Zones, which in turn removes their Woredas.
    /// </summary>
    public partial class AddLocationHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "location_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_location_entries_ParentId",
                table: "location_entries",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_location_entries_location_entries_ParentId",
                table: "location_entries",
                column: "ParentId",
                principalTable: "location_entries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_location_entries_location_entries_ParentId",
                table: "location_entries");

            migrationBuilder.DropIndex(
                name: "IX_location_entries_ParentId",
                table: "location_entries");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "location_entries");
        }
    }
}
