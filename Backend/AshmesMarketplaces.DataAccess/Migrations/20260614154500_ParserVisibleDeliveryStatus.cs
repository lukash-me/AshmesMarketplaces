using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    public partial class ParserVisibleDeliveryStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "visible_delivery_status",
                table: "ParserLogisticsSnapshotRows",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "visible_delivery_raw_payload",
                table: "ParserLogisticsSnapshotRows",
                type: "jsonb",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "visible_delivery_status",
                table: "ParserLogisticsSnapshotRows");

            migrationBuilder.DropColumn(
                name: "visible_delivery_raw_payload",
                table: "ParserLogisticsSnapshotRows");
        }
    }
}
