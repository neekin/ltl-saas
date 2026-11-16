using System;
using Ltl.Modules.Transport.Domain;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Transport.Migrations
{
    /// <inheritdoc />
    public partial class InitialTransport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    license_no = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_drivers", x => x.id);
                    table.CheckConstraint("ck_drivers_status", "status IN (0, 1)");
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plate_no = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vehicle_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    max_load_kg = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    max_volume_m3 = table.Column<decimal>(type: "numeric(12,4)", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vehicles", x => x.id);
                    table.CheckConstraint("ck_vehicles_load", "max_load_kg > 0");
                    table.CheckConstraint("ck_vehicles_status", "status IN (0, 1)");
                    table.CheckConstraint("ck_vehicles_volume", "max_volume_m3 > 0");
                });

            migrationBuilder.CreateIndex(
                name: "ux_drivers_tenant_id",
                table: "drivers",
                columns: new[] { "tenant_id", "id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_drivers_tenant_license",
                table: "drivers",
                columns: new[] { "tenant_id", "license_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_vehicles_tenant_id",
                table: "vehicles",
                columns: new[] { "tenant_id", "id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_vehicles_tenant_plate",
                table: "vehicles",
                columns: new[] { "tenant_id", "plate_no" },
                unique: true);
            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "code", "name", "module" },
                columnTypes: new[] { "uuid", "character varying(100)", "character varying(200)", "character varying(64)" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000401"), TransportPermissionCodes.VehicleRead, "查看车辆/司机档案", TransportPermissionCodes.Module },
                    { new Guid("00000000-0000-0000-0000-000000000402"), TransportPermissionCodes.VehicleManage, "管理车辆/司机档案", TransportPermissionCodes.Module },
                    { new Guid("00000000-0000-0000-0000-000000000403"), TransportPermissionCodes.TripRead, "查看运输批次/配载", TransportPermissionCodes.Module },
                    { new Guid("00000000-0000-0000-0000-000000000404"), TransportPermissionCodes.TripManage, "管理运输批次/配载/发车/到达", TransportPermissionCodes.Module },
                });

            // 手写复合外键：跨模块 → tenants（AGENTS §5.1）
            migrationBuilder.AddForeignKey(
                name: "fk_vehicles_tenant",
                table: "vehicles",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_drivers_tenant",
                table: "drivers",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyColumnType: "uuid",
                keyValue: new Guid("00000000-0000-0000-0000-000000000401"));
            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyColumnType: "uuid",
                keyValue: new Guid("00000000-0000-0000-0000-000000000402"));
            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyColumnType: "uuid",
                keyValue: new Guid("00000000-0000-0000-0000-000000000403"));
            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyColumnType: "uuid",
                keyValue: new Guid("00000000-0000-0000-0000-000000000404"));

            migrationBuilder.DropTable(
                name: "drivers");

            migrationBuilder.DropTable(
                name: "vehicles");

            migrationBuilder.DropTable(
                name: "vehicles");
        }
    }
}
