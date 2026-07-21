using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    public partial class ParserDeliveryDestinationMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "delivery_destination_address",
                table: "ParserWarehouseAvailabilityRows",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_destination_city",
                table: "ParserWarehouseAvailabilityRows",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_destination_label",
                table: "ParserWarehouseAvailabilityRows",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "delivery_destination_latitude",
                table: "ParserWarehouseAvailabilityRows",
                type: "numeric(10,7)",
                precision: 10,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "delivery_destination_longitude",
                table: "ParserWarehouseAvailabilityRows",
                type: "numeric(10,7)",
                precision: 10,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_destination_address",
                table: "ParserLogisticsSnapshotRows",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_destination_city",
                table: "ParserLogisticsSnapshotRows",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_destination_label",
                table: "ParserLogisticsSnapshotRows",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "delivery_destination_latitude",
                table: "ParserLogisticsSnapshotRows",
                type: "numeric(10,7)",
                precision: 10,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "delivery_destination_longitude",
                table: "ParserLogisticsSnapshotRows",
                type: "numeric(10,7)",
                precision: 10,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<System.DateTime>(
                name: "visible_delivery_date",
                table: "ParserLogisticsSnapshotRows",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "visible_delivery_label",
                table: "ParserLogisticsSnapshotRows",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<System.DateTime>(
                name: "visible_delivery_observed_at_utc",
                table: "ParserLogisticsSnapshotRows",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "visible_delivery_source",
                table: "ParserLogisticsSnapshotRows",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "delivery_destination_address",
                table: "ParserWarehouseAvailabilityRows");

            migrationBuilder.DropColumn(
                name: "delivery_destination_city",
                table: "ParserWarehouseAvailabilityRows");

            migrationBuilder.DropColumn(
                name: "delivery_destination_label",
                table: "ParserWarehouseAvailabilityRows");

            migrationBuilder.DropColumn(
                name: "delivery_destination_latitude",
                table: "ParserWarehouseAvailabilityRows");

            migrationBuilder.DropColumn(
                name: "delivery_destination_longitude",
                table: "ParserWarehouseAvailabilityRows");

            migrationBuilder.DropColumn(
                name: "delivery_destination_address",
                table: "ParserLogisticsSnapshotRows");

            migrationBuilder.DropColumn(
                name: "delivery_destination_city",
                table: "ParserLogisticsSnapshotRows");

            migrationBuilder.DropColumn(
                name: "delivery_destination_label",
                table: "ParserLogisticsSnapshotRows");

            migrationBuilder.DropColumn(
                name: "delivery_destination_latitude",
                table: "ParserLogisticsSnapshotRows");

            migrationBuilder.DropColumn(
                name: "delivery_destination_longitude",
                table: "ParserLogisticsSnapshotRows");

            migrationBuilder.DropColumn(
                name: "visible_delivery_date",
                table: "ParserLogisticsSnapshotRows");

            migrationBuilder.DropColumn(
                name: "visible_delivery_label",
                table: "ParserLogisticsSnapshotRows");

            migrationBuilder.DropColumn(
                name: "visible_delivery_observed_at_utc",
                table: "ParserLogisticsSnapshotRows");

            migrationBuilder.DropColumn(
                name: "visible_delivery_source",
                table: "ParserLogisticsSnapshotRows");
        }
    }
}
