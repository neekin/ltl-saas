using System;
using Ltl.Modules.Delivery.Domain;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Delivery.Migrations
{
    /// <inheritdoc />
    public partial class InitialDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "delivery_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    detail = table.Column<string>(type: "jsonb", nullable: true),
                    operator_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "delivery_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    waybill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_type = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    receiver_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    operator_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    completed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_orders", x => x.id);
                    table.CheckConstraint("ck_delivery_orders_status", "status IN (1, 2, 3)");
                    table.CheckConstraint("ck_delivery_orders_type", "delivery_type IN (1, 2)");
                });

            migrationBuilder.CreateTable(
                name: "proof_of_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receiver_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    photo_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    signed_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_proof_of_deliveries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_events_order",
                table: "delivery_events",
                columns: new[] { "tenant_id", "delivery_order_id" });

            migrationBuilder.CreateIndex(
                name: "ux_delivery_orders_active_waybill",
                table: "delivery_orders",
                columns: new[] { "tenant_id", "waybill_id" },
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ux_delivery_orders_tenant_id",
                table: "delivery_orders",
                columns: new[] { "tenant_id", "id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_proof_of_deliveries_order",
                table: "proof_of_deliveries",
                columns: new[] { "tenant_id", "delivery_order_id" },
                unique: true);
            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "code", "name", "module" },
                columnTypes: new[] { "uuid", "character varying(100)", "character varying(200)", "character varying(64)" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000901"), DeliveryPermissionCodes.DeliveryRead, "查看交付单/凭证", DeliveryPermissionCodes.Module },
                    { new Guid("00000000-0000-0000-0000-000000000902"), DeliveryPermissionCodes.DeliveryManage, "管理交付/签收/拒收", DeliveryPermissionCodes.Module },
                });

            migrationBuilder.AddForeignKey(
                name: "fk_delivery_orders_tenant",
                table: "delivery_orders", column: "tenant_id",
                principalTable: "tenants", principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_delivery_orders_tenant_waybill",
                table: "delivery_orders", columns: new[] { "tenant_id", "waybill_id" },
                principalTable: "waybills", principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_delivery_orders_tenant_branch",
                table: "delivery_orders", columns: new[] { "tenant_id", "branch_id" },
                principalTable: "branches", principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_proof_of_deliveries_tenant",
                table: "proof_of_deliveries", column: "tenant_id",
                principalTable: "tenants", principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_delivery_events_tenant",
                table: "delivery_events", column: "tenant_id",
                principalTable: "tenants", principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "permissions", keyColumn: "id", keyColumnType: "uuid",
                keyValue: new Guid("00000000-0000-0000-0000-000000000901"));
            migrationBuilder.DeleteData(
                table: "permissions", keyColumn: "id", keyColumnType: "uuid",
                keyValue: new Guid("00000000-0000-0000-0000-000000000902"));

            migrationBuilder.DropTable(
                name: "delivery_events");

            migrationBuilder.DropTable(
                name: "delivery_orders");

            migrationBuilder.DropTable(
                name: "proof_of_deliveries");
        }
    }
}
