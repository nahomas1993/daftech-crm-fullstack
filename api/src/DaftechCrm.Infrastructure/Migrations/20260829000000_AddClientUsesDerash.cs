using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <summary>
    /// clients.UsesDerash: whether the client uses the Derash payment
    /// system. Defaults to false for every existing client, so nothing
    /// changes for them until an admin ticks the box on the client page.
    /// A Derash client always gets free support, even after the support
    /// window of its agreement has run out.
    /// </summary>
    public partial class AddClientUsesDerash : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UsesDerash",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UsesDerash",
                table: "clients");
        }
    }
}
