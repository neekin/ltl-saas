using System;
using Ltl.Modules.Warehouse.Domain;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Warehouse.Migrations
{
    /// <inheritdoc />
    public partial class InitialWarehouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cargo_receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    waybill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    received_pieces = table.Column<int>(type: "integer", nullable: false),
                    received_weight_kg = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    received_volume_m3 = table.Column<decimal>(type: "numeric(12,4)", nullable: false),
                    operator_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cargo_receipts", x => x.id);
                    table.CheckConstraint("ck_cargo_receipts_pieces", "received_pieces >= 1");
                    table.CheckConstraint("ck_cargo_receipts_volume", "received_volume_m3 >= 0");
                    table.CheckConstraint("ck_cargo_receipts_weight", "received_weight_kg >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_cargo_receipts_waybill",
                table: "cargo_receipts",
                columns: new[] { "tenant_id", "waybill_id", "occurred_at" });
            // 权限点 seed（平台预置清单，ORG-001 约定）
            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "code", "name", "module" },
                columnTypes: new[] { "uuid", "character varying(100)", "character varying(200)", "character varying(64)" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000201"), WarehousePermissionCodes.CargoReceive, "收货登记（称重、体积复核）", WarehousePermissionCodes.Module },
                    { new Guid("00000000-0000-0000-0000-000000000202"), WarehousePermissionCodes.CargoRead, "查看收货记录与仓储状态", WarehousePermissionCodes.Module },
                    { new Guid("00000000-0000-0000-0000-000000000203"), WarehousePermissionCodes.CargoSort, "分拣任务操作", WarehousePermissionCodes.Module },
                });
            // 手写复合外键（AGENTS §5.1）
            migrationBuilder.AddForeignKey(
                name: "fk_cargo_receipts_tenant",
                table: "cargo_receipts",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_cargo_receipts_tenant_waybill",
                table: "cargo_receipts",
                columns: new[] { "tenant_id", "waybill_id" },
                principalTable: "waybills",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_cargo_receipts_tenant_branch",
                table: "cargo_receipts",
                columns: new[] { "tenant_id", "branch_id" },
                principalTable: "branches",
                principalColumns: new[] { "tenant_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyColumnType: "uuid",
                keyValue: new Guid("00000000-0000-0000-0000-000000000201"));
            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyColumnType: "uuid",
                keyValue: new Guid("00000000-0000-0000-0000-000000000202"));
            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyColumnType: "uuid",
                keyValue: new Guid("00000000-0000-0000-0000-000000000203"));

            migrationBuilder.DropTable(
                name: "cargo_receipts");
        }
    }
}
