using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <summary>
    /// Adds stored_files — binary content for PostgresFileStorageService,
    /// so ticket attachments and voice-note recordings live in this
    /// database instead of a third-party host (Cloudinary). Storage.Provider
    /// in appsettings switches which IFileStorageService implementation is
    /// active; this table only matters when Provider is Postgres.
    /// </summary>
    public partial class AddStoredFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stored_files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stored_files", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "stored_files");
        }
    }
}
