using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ParserProxyRunSplitCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "empty_ranges_count",
                table: "ParserProxyRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "final_ranges_count",
                table: "ParserProxyRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "range_checks_count",
                table: "ParserProxyRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "split_ranges_count",
                table: "ParserProxyRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "empty_ranges_count",
                table: "ParserProxyRuns");

            migrationBuilder.DropColumn(
                name: "final_ranges_count",
                table: "ParserProxyRuns");

            migrationBuilder.DropColumn(
                name: "range_checks_count",
                table: "ParserProxyRuns");

            migrationBuilder.DropColumn(
                name: "split_ranges_count",
                table: "ParserProxyRuns");
        }
    }
}
