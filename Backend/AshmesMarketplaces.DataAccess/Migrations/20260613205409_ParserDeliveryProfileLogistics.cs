using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ParserDeliveryProfileLogistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "delivery_destination_name",
                table: "ParserWarehouseAvailabilityRows",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_profile_key",
                table: "ParserWarehouseAvailabilityRows",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_profile_version",
                table: "ParserWarehouseAvailabilityRows",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_destination_name",
                table: "ParserLogisticsSnapshotRows",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_profile_key",
                table: "ParserLogisticsSnapshotRows",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_profile_version",
                table: "ParserLogisticsSnapshotRows",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParserWarehouseAvailabilityRows_delivery_profile_key_source~",
                table: "ParserWarehouseAvailabilityRows",
                columns: new[] { "delivery_profile_key", "source_region_dest", "observed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserLogisticsSnapshotRows_delivery_profile_key_source_reg~",
                table: "ParserLogisticsSnapshotRows",
                columns: new[] { "delivery_profile_key", "source_region_dest", "observed_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ParserWarehouseAvailabilityRows_delivery_profile_key_source~",
                table: "ParserWarehouseAvailabilityRows");

            migrationBuilder.DropIndex(
                name: "IX_ParserLogisticsSnapshotRows_delivery_profile_key_source_reg~",
                table: "ParserLogisticsSnapshotRows");

            migrationBuilder.DropColumn(
                name: "delivery_destination_name",
                table: "ParserWarehouseAvailabilityRows");

            migrationBuilder.DropColumn(
                name: "delivery_profile_key",
                table: "ParserWarehouseAvailabilityRows");

            migrationBuilder.DropColumn(
                name: "delivery_profile_version",
                table: "ParserWarehouseAvailabilityRows");

            migrationBuilder.DropColumn(
                name: "delivery_destination_name",
                table: "ParserLogisticsSnapshotRows");

            migrationBuilder.DropColumn(
                name: "delivery_profile_key",
                table: "ParserLogisticsSnapshotRows");

            migrationBuilder.DropColumn(
                name: "delivery_profile_version",
                table: "ParserLogisticsSnapshotRows");
        }
    }
}
