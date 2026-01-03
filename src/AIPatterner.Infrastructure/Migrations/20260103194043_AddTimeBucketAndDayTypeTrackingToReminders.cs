using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIPatterner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeBucketAndDayTypeTrackingToReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MostCommonDayType",
                table: "remindercandidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MostCommonTimeBucket",
                table: "remindercandidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservedDayTypeHistogramJson",
                table: "remindercandidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservedTimeBucketHistogramJson",
                table: "remindercandidates",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MostCommonDayType",
                table: "remindercandidates");

            migrationBuilder.DropColumn(
                name: "MostCommonTimeBucket",
                table: "remindercandidates");

            migrationBuilder.DropColumn(
                name: "ObservedDayTypeHistogramJson",
                table: "remindercandidates");

            migrationBuilder.DropColumn(
                name: "ObservedTimeBucketHistogramJson",
                table: "remindercandidates");
        }
    }
}
