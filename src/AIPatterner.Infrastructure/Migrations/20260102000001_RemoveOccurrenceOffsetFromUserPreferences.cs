using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIPatterner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOccurrenceOffsetFromUserPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OccurrenceOffsetMin",
                table: "userreminderpreferences");

            migrationBuilder.DropColumn(
                name: "OccurrenceOffsetMax",
                table: "userreminderpreferences");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OccurrenceOffsetMin",
                table: "userreminderpreferences",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OccurrenceOffsetMax",
                table: "userreminderpreferences",
                type: "integer",
                nullable: false,
                defaultValue: 15);
        }
    }
}

