using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ParserProxyRunPhaseProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "completed_ranges_count",
                table: "ParserProxyRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "phase",
                table: "ParserProxyRuns",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "download");

            migrationBuilder.AddColumn<int>(
                name: "planned_ranges_count",
                table: "ParserProxyRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "range_progress_percent",
                table: "ParserProxyRuns",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "completed_ranges_count",
                table: "ParserProxyRuns");

            migrationBuilder.DropColumn(
                name: "phase",
                table: "ParserProxyRuns");

            migrationBuilder.DropColumn(
                name: "planned_ranges_count",
                table: "ParserProxyRuns");

            migrationBuilder.DropColumn(
                name: "range_progress_percent",
                table: "ParserProxyRuns");
        }
    }
}
