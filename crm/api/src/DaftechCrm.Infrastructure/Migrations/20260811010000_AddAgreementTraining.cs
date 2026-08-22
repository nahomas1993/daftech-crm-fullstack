using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgreementTraining : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrainingScanStorageKey",
                table: "agreements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrainingScanFileName",
                table: "agreements",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrainingDescription",
                table: "agreements",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TrainingStartDate",
                table: "agreements",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TrainingEndDate",
                table: "agreements",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TrainingScanStorageKey", table: "agreements");
            migrationBuilder.DropColumn(name: "TrainingScanFileName", table: "agreements");
            migrationBuilder.DropColumn(name: "TrainingDescription", table: "agreements");
            migrationBuilder.DropColumn(name: "TrainingStartDate", table: "agreements");
            migrationBuilder.DropColumn(name: "TrainingEndDate", table: "agreements");
        }
    }
}
