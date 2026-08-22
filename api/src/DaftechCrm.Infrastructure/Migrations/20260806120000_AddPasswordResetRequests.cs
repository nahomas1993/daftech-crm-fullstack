using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "password_reset_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountType = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequestIpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DismissReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_requests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_requests_AccountType_AccountId_Status",
                table: "password_reset_requests",
                columns: new[] { "AccountType", "AccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_requests_Status",
                table: "password_reset_requests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "password_reset_requests");
        }
    }
}
