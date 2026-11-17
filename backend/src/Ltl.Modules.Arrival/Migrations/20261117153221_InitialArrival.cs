using System;
using Ltl.Modules.Arrivals.Domain;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Arrivals.Migrations
{
    /// <inheritdoc />
    public partial class InitialArrival : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "arrivals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operator_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    arrived_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arrivals", x => x.id);
                    table.UniqueConstraint("ak_arrivals_tenant_id_id", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "arrival_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    arrival_id = table.Column<Guid>(type: "uuid", nullable: false),
                    waybill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    loaded_pieces = table.Column<int>(type: "integer", nullable: false),
                    received_pieces = table.Column<int>(type: "integer", nullable: false),
                    broken_pieces = table.Column<int>(type: "integer", nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arrival_items", x => x.id);
                    table.CheckConstraint("ck_arrival_items_broken", "broken_pieces >= 0");
                    table.CheckConstraint("ck_arrival_items_loaded", "loaded_pieces >= 1");
                    table.CheckConstraint("ck_arrival_items_received", "received_pieces >= 0");
                    table.ForeignKey(
                        name: "fk_arrival_items_arrivals_tenant_id_arrival_id",
                        columns: x => new { x.tenant_id, x.arrival_id },
                        principalTable: "arrivals",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_arrival_items_tenant_id_arrival_id",
                table: "arrival_items",
                columns: new[] { "tenant_id", "arrival_id" });

            migrationBuilder.CreateIndex(
                name: "ux_arrival_items_tenant_waybill",
                table: "arrival_items",
                columns: new[] { "tenant_id", "waybill_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_arrivals_tenant_id",
                table: "arrivals",
                columns: new[] { "tenant_id", "id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_arrivals_tenant_trip",
                table: "arrivals",
                columns: new[] { "tenant_id", "trip_id" },
                unique: true);
            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "code", "name", "module" },
                columnTypes: new[] { "uuid", "character varying(100)", "character varying(200)", "character varying(64)" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000801"), ArrivalPermissionCodes.ArrivalRead, "查看到货记录", ArrivalPermissionCodes.Module },
                    { new Guid("00000000-0000-0000-0000-000000000802"), ArrivalPermissionCodes.ArrivalRecord, "到货登记", ArrivalPermissionCodes.Module },
                });

            migrationBuilder.AddForeignKey(
                name: "fk_arrivals_tenant",
                table: "arrivals",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_arrivals_tenant_trip",
                table: "arrivals",
                columns: new[] { "tenant_id", "trip_id" },
                principalTable: "transport_trips",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_arrivals_tenant_branch",
                table: "arrivals",
                column: "tenant_id",
                principalTable: "branches",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_arrival_items_tenant",
                table: "arrival_items",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_arrival_items_tenant_arrival",
                table: "arrival_items",
                columns: new[] { "tenant_id", "arrival_id" },
                principalTable: "arrivals",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_arrival_items_tenant_waybill",
                table: "arrival_items",
                columns: new[] { "tenant_id", "waybill_id" },
                principalTable: "waybills",
                principalColumns: new[] { "tenant_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyColumnType: "uuid",
                keyValue: new Guid("00000000-0000-0000-0000-000000000801"));
            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyColumnType: "uuid",
                keyValue: new Guid("00000000-0000-0000-0000-000000000802"));

            migrationBuilder.DropTable(
                name: "arrival_items");

            migrationBuilder.DropTable(
                name: "arrivals");
        }
    }
}
