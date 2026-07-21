using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Brands",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false),
                    country = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    manufacturer = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    sales_amount = table.Column<int>(type: "integer", nullable: false),
                    rate_redemption = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    date_mp_registration = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_update = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_parent_category = table.Column<Guid>(type: "uuid", nullable: true),
                    id_on_mp = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    date_update = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.id);
                    table.ForeignKey(
                        name: "FK_Categories_Categories_id_parent_category",
                        column: x => x.id_parent_category,
                        principalTable: "Categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Categories_Expenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    date_create = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_update = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories_Expenses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Marketplaces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    api_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    api_version = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    currency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    region = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    type_commission = table.Column<int>(type: "integer", nullable: false),
                    scheme_delivery = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    date_update = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Marketplaces", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions_Categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    date_update = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_create = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions_Categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Recommendations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_model = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    type_object = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    explanation = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    snapshot = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    date_create = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_update = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recommendations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    date_create = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_update = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    number = table.Column<int>(type: "integer", nullable: true),
                    domain = table.Column<int>(type: "integer", nullable: false),
                    date_create = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_update = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Sets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    date_update = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_create = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Workspaces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_brand = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    url_invite = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    date_create = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_update = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspaces", x => x.id);
                    table.ForeignKey(
                        name: "FK_Workspaces_Brands_id_brand",
                        column: x => x.id_brand,
                        principalTable: "Brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Brands_Marketplaces",
                columns: table => new
                {
                    id_mp = table.Column<Guid>(type: "uuid", nullable: false),
                    id_brand = table.Column<Guid>(type: "uuid", nullable: false),
                    id_on_mp = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands_Marketplaces", x => new { x.id_mp, x.id_brand });
                    table.ForeignKey(
                        name: "FK_Brands_Marketplaces_Brands_id_brand",
                        column: x => x.id_brand,
                        principalTable: "Brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Brands_Marketplaces_Marketplaces_id_mp",
                        column: x => x.id_mp,
                        principalTable: "Marketplaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Warehouses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_mp = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    region = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    city = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    address = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    latitude = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    longitude = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    date_create = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_update = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => x.id);
                    table.ForeignKey(
                        name: "FK_Warehouses_Marketplaces_id_mp",
                        column: x => x.id_mp,
                        principalTable: "Marketplaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_category = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    domain = table.Column<int>(type: "integer", nullable: false),
                    date_create = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_update = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_Permissions_Permissions_Categories_id_category",
                        column: x => x.id_category,
                        principalTable: "Permissions_Categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Recommendation_Categories",
                columns: table => new
                {
                    id_recommendation = table.Column<Guid>(type: "uuid", nullable: false),
                    id_category = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recommendation_Categories", x => new { x.id_recommendation, x.id_category });
                    table.ForeignKey(
                        name: "FK_Recommendation_Categories_Categories_id_category",
                        column: x => x.id_category,
                        principalTable: "Categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Recommendation_Categories_Recommendations_id_recommendation",
                        column: x => x.id_recommendation,
                        principalTable: "Recommendations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Role_Subroles",
                columns: table => new
                {
                    id_role = table.Column<Guid>(type: "uuid", nullable: false),
                    id_subrole = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role_Subroles", x => new { x.id_role, x.id_subrole });
                    table.ForeignKey(
                        name: "FK_Role_Subroles_Roles_id_role",
                        column: x => x.id_role,
                        principalTable: "Roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Role_Subroles_Roles_id_subrole",
                        column: x => x.id_subrole,
                        principalTable: "Roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_role = table.Column<Guid>(type: "uuid", nullable: false),
                    login = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    date_create = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_login = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.id);
                    table.ForeignKey(
                        name: "FK_Users_Roles_id_role",
                        column: x => x.id_role,
                        principalTable: "Roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sets_Rules",
                columns: table => new
                {
                    id_set = table.Column<Guid>(type: "uuid", nullable: false),
                    id_rule = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sets_Rules", x => new { x.id_set, x.id_rule });
                    table.ForeignKey(
                        name: "FK_Sets_Rules_Rules_id_rule",
                        column: x => x.id_rule,
                        principalTable: "Rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sets_Rules_Sets_id_set",
                        column: x => x.id_set,
                        principalTable: "Sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Roles_Permissions",
                columns: table => new
                {
                    id_role = table.Column<Guid>(type: "uuid", nullable: false),
                    id_permission = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles_Permissions", x => new { x.id_role, x.id_permission });
                    table.ForeignKey(
                        name: "FK_Roles_Permissions_Permissions_id_permission",
                        column: x => x.id_permission,
                        principalTable: "Permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Roles_Permissions_Roles_id_role",
                        column: x => x.id_role,
                        principalTable: "Roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_workspace = table.Column<Guid>(type: "uuid", nullable: false),
                    id_category = table.Column<Guid>(type: "uuid", nullable: true),
                    id_creator = table.Column<Guid>(type: "uuid", nullable: false),
                    id_responsible = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    date_pay = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    date_create = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_update = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.id);
                    table.ForeignKey(
                        name: "FK_Expenses_Categories_Expenses_id_category",
                        column: x => x.id_category,
                        principalTable: "Categories_Expenses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_Users_id_creator",
                        column: x => x.id_creator,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_Users_id_responsible",
                        column: x => x.id_responsible,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_Workspaces_id_workspace",
                        column: x => x.id_workspace,
                        principalTable: "Workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_set_price = table.Column<Guid>(type: "uuid", nullable: true),
                    id_workspace = table.Column<Guid>(type: "uuid", nullable: true),
                    id_brand = table.Column<Guid>(type: "uuid", nullable: true),
                    id_mp = table.Column<Guid>(type: "uuid", nullable: false),
                    id_user = table.Column<Guid>(type: "uuid", nullable: true),
                    id_category = table.Column<Guid>(type: "uuid", nullable: true),
                    id_on_mp = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    sku_product = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    sku_seller = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    characteristics = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    commission = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    date_update = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_create = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.id);
                    table.ForeignKey(
                        name: "FK_Products_Brands_id_brand",
                        column: x => x.id_brand,
                        principalTable: "Brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_Categories_id_category",
                        column: x => x.id_category,
                        principalTable: "Categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_Marketplaces_id_mp",
                        column: x => x.id_mp,
                        principalTable: "Marketplaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_Sets_id_set_price",
                        column: x => x.id_set_price,
                        principalTable: "Sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_Users_id_user",
                        column: x => x.id_user,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_Workspaces_id_workspace",
                        column: x => x.id_workspace,
                        principalTable: "Workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_user = table.Column<Guid>(type: "uuid", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    token = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    date_create = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_refreshed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    date_expires = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_Sessions_Users_id_user",
                        column: x => x.id_user,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users_Workspaces",
                columns: table => new
                {
                    id_user = table.Column<Guid>(type: "uuid", nullable: false),
                    id_workspace = table.Column<Guid>(type: "uuid", nullable: false),
                    id_role = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users_Workspaces", x => new { x.id_user, x.id_workspace });
                    table.ForeignKey(
                        name: "FK_Users_Workspaces_Roles_id_role",
                        column: x => x.id_role,
                        principalTable: "Roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Users_Workspaces_Users_id_user",
                        column: x => x.id_user,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Users_Workspaces_Workspaces_id_workspace",
                        column: x => x.id_workspace,
                        principalTable: "Workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Campaign",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_product = table.Column<Guid>(type: "uuid", nullable: false),
                    id_set_campaign = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    budget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    region = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    time_to_impression = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    date_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    date_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    date_create = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_update = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaign", x => x.id);
                    table.ForeignKey(
                        name: "FK_Campaign_Products_id_product",
                        column: x => x.id_product,
                        principalTable: "Products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Campaign_Sets_id_set_campaign",
                        column: x => x.id_set_campaign,
                        principalTable: "Sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Logistics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_product = table.Column<Guid>(type: "uuid", nullable: false),
                    id_warehouse = table.Column<Guid>(type: "uuid", nullable: true),
                    stock_amount = table.Column<int>(type: "integer", nullable: true),
                    stock_amount_statistic = table.Column<int>(type: "integer", nullable: false),
                    stock_in_transit = table.Column<int>(type: "integer", nullable: true),
                    cost_storage = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    cost_logistic = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    type = table.Column<int>(type: "integer", nullable: false),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Logistics", x => x.id);
                    table.ForeignKey(
                        name: "FK_Logistics_Products_id_product",
                        column: x => x.id_product,
                        principalTable: "Products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Logistics_Warehouses_id_warehouse",
                        column: x => x.id_warehouse,
                        principalTable: "Warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_product = table.Column<Guid>(type: "uuid", nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    discount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    amount = table.Column<int>(type: "integer", nullable: false),
                    location_source = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    location_destination = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    date_delivered = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    date_opened = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_closed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    date_update = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_Orders_Products_id_product",
                        column: x => x.id_product,
                        principalTable: "Products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_product = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_main = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImages", x => x.id);
                    table.ForeignKey(
                        name: "FK_ProductImages_Products_id_product",
                        column: x => x.id_product,
                        principalTable: "Products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Products_Historical",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_product = table.Column<Guid>(type: "uuid", nullable: false),
                    cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    discount = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_auto_discount_active = table.Column<bool>(type: "boolean", nullable: true),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products_Historical", x => x.id);
                    table.ForeignKey(
                        name: "FK_Products_Historical_Products_id_product",
                        column: x => x.id_product,
                        principalTable: "Products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductVideos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_product = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVideos", x => x.id);
                    table.ForeignKey(
                        name: "FK_ProductVideos_Products_id_product",
                        column: x => x.id_product,
                        principalTable: "Products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recommendation_Products",
                columns: table => new
                {
                    id_recommendation = table.Column<Guid>(type: "uuid", nullable: false),
                    id_product = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recommendation_Products", x => new { x.id_recommendation, x.id_product });
                    table.ForeignKey(
                        name: "FK_Recommendation_Products_Products_id_product",
                        column: x => x.id_product,
                        principalTable: "Products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Recommendation_Products_Recommendations_id_recommendation",
                        column: x => x.id_recommendation,
                        principalTable: "Recommendations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_product = table.Column<Guid>(type: "uuid", nullable: false),
                    id_on_mp = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: true),
                    is_replied = table.Column<bool>(type: "boolean", nullable: false),
                    date_create = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_reply = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_Reviews_Products_id_product",
                        column: x => x.id_product,
                        principalTable: "Products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Metrics_Campaign",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_campaign = table.Column<Guid>(type: "uuid", nullable: false),
                    impression_amount = table.Column<int>(type: "integer", nullable: true),
                    clicks_amount = table.Column<int>(type: "integer", nullable: true),
                    cost_day = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Metrics_Campaign", x => x.id);
                    table.ForeignKey(
                        name: "FK_Metrics_Campaign_Campaign_id_campaign",
                        column: x => x.id_campaign,
                        principalTable: "Campaign",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reply",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_review = table.Column<Guid>(type: "uuid", nullable: false),
                    id_on_mp = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    date_create = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_update = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reply", x => x.id);
                    table.ForeignKey(
                        name: "FK_Reply_Reviews_id_review",
                        column: x => x.id_review,
                        principalTable: "Reviews",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Brands_Marketplaces_id_brand",
                table: "Brands_Marketplaces",
                column: "id_brand");

            migrationBuilder.CreateIndex(
                name: "IX_Campaign_id_product",
                table: "Campaign",
                column: "id_product");

            migrationBuilder.CreateIndex(
                name: "IX_Campaign_id_set_campaign",
                table: "Campaign",
                column: "id_set_campaign");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_id_parent_category",
                table: "Categories",
                column: "id_parent_category");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_id_category",
                table: "Expenses",
                column: "id_category");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_id_creator",
                table: "Expenses",
                column: "id_creator");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_id_responsible",
                table: "Expenses",
                column: "id_responsible");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_id_workspace",
                table: "Expenses",
                column: "id_workspace");

            migrationBuilder.CreateIndex(
                name: "IX_Logistics_id_product",
                table: "Logistics",
                column: "id_product");

            migrationBuilder.CreateIndex(
                name: "IX_Logistics_id_warehouse",
                table: "Logistics",
                column: "id_warehouse");

            migrationBuilder.CreateIndex(
                name: "IX_Metrics_Campaign_id_campaign",
                table: "Metrics_Campaign",
                column: "id_campaign");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_id_product",
                table: "Orders",
                column: "id_product");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_id_category",
                table: "Permissions",
                column: "id_category");

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_id_product",
                table: "ProductImages",
                column: "id_product");

            migrationBuilder.CreateIndex(
                name: "IX_Products_id_brand",
                table: "Products",
                column: "id_brand");

            migrationBuilder.CreateIndex(
                name: "IX_Products_id_category",
                table: "Products",
                column: "id_category");

            migrationBuilder.CreateIndex(
                name: "IX_Products_id_mp",
                table: "Products",
                column: "id_mp");

            migrationBuilder.CreateIndex(
                name: "IX_Products_id_set_price",
                table: "Products",
                column: "id_set_price");

            migrationBuilder.CreateIndex(
                name: "IX_Products_id_user",
                table: "Products",
                column: "id_user");

            migrationBuilder.CreateIndex(
                name: "IX_Products_id_workspace",
                table: "Products",
                column: "id_workspace");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Historical_id_product",
                table: "Products_Historical",
                column: "id_product");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVideos_id_product",
                table: "ProductVideos",
                column: "id_product");

            migrationBuilder.CreateIndex(
                name: "IX_Recommendation_Categories_id_category",
                table: "Recommendation_Categories",
                column: "id_category");

            migrationBuilder.CreateIndex(
                name: "IX_Recommendation_Products_id_product",
                table: "Recommendation_Products",
                column: "id_product");

            migrationBuilder.CreateIndex(
                name: "IX_Reply_id_review",
                table: "Reply",
                column: "id_review");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_id_product",
                table: "Reviews",
                column: "id_product");

            migrationBuilder.CreateIndex(
                name: "IX_Role_Subroles_id_subrole",
                table: "Role_Subroles",
                column: "id_subrole");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Permissions_id_permission",
                table: "Roles_Permissions",
                column: "id_permission");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_id_user",
                table: "Sessions",
                column: "id_user");

            migrationBuilder.CreateIndex(
                name: "IX_Sets_Rules_id_rule",
                table: "Sets_Rules",
                column: "id_rule");

            migrationBuilder.CreateIndex(
                name: "IX_Users_id_role",
                table: "Users",
                column: "id_role");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Workspaces_id_role",
                table: "Users_Workspaces",
                column: "id_role");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Workspaces_id_workspace",
                table: "Users_Workspaces",
                column: "id_workspace");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_id_mp",
                table: "Warehouses",
                column: "id_mp");

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_id_brand",
                table: "Workspaces",
                column: "id_brand");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Brands_Marketplaces");

            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "Logistics");

            migrationBuilder.DropTable(
                name: "Metrics_Campaign");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "ProductImages");

            migrationBuilder.DropTable(
                name: "Products_Historical");

            migrationBuilder.DropTable(
                name: "ProductVideos");

            migrationBuilder.DropTable(
                name: "Recommendation_Categories");

            migrationBuilder.DropTable(
                name: "Recommendation_Products");

            migrationBuilder.DropTable(
                name: "Reply");

            migrationBuilder.DropTable(
                name: "Role_Subroles");

            migrationBuilder.DropTable(
                name: "Roles_Permissions");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "Sets_Rules");

            migrationBuilder.DropTable(
                name: "Users_Workspaces");

            migrationBuilder.DropTable(
                name: "Categories_Expenses");

            migrationBuilder.DropTable(
                name: "Warehouses");

            migrationBuilder.DropTable(
                name: "Campaign");

            migrationBuilder.DropTable(
                name: "Recommendations");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Rules");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Permissions_Categories");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Marketplaces");

            migrationBuilder.DropTable(
                name: "Sets");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Workspaces");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Brands");
        }
    }
}
