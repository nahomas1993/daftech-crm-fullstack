using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <summary>
    /// Adds IsDeleted/DeletedAt to employees and clients — backs the new
    /// Edit/Delete actions on the Employees and Clients pages. "Delete"
    /// here is a soft delete (hides the account from active lists/login)
    /// rather than a real DELETE, since both tables have historical
    /// records (tickets, time logs, agreements, etc.) referencing them by
    /// Id that must survive.
    /// </summary>
    public partial class AddSoftDeleteToEmployeesAndClients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "employees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "clients",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsDeleted", table: "employees");
            migrationBuilder.DropColumn(name: "DeletedAt", table: "employees");
            migrationBuilder.DropColumn(name: "IsDeleted", table: "clients");
            migrationBuilder.DropColumn(name: "DeletedAt", table: "clients");
        }
    }
}
