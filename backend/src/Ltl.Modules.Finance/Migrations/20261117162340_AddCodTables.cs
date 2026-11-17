using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Finance.Migrations
{
    /// <inheritdoc />
    public partial class AddCodTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cod_collections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cod_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    operator_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cod_collections", x => x.id);
                    table.CheckConstraint("ck_cod_collections_amount", "amount > 0");
                });

            migrationBuilder.CreateTable(
                name: "cod_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    waybill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    collected_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    settled_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cod_orders", x => x.id);
                    table.CheckConstraint("ck_cod_orders_amount", "amount > 0");
                    table.CheckConstraint("ck_cod_orders_collected", "collected_amount >= 0 AND collected_amount <= amount");
                    table.CheckConstraint("ck_cod_orders_paid", "paid_amount >= 0 AND paid_amount <= settled_amount");
                    table.CheckConstraint("ck_cod_orders_settled", "settled_amount >= 0 AND settled_amount <= collected_amount");
                    table.CheckConstraint("ck_cod_orders_status", "status IN (1, 2, 3, 4)");
                });

            migrationBuilder.CreateTable(
                name: "cod_settlements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cod_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    paid_by = table.Column<Guid>(type: "uuid", nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    operator_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cod_settlements", x => x.id);
                    table.CheckConstraint("ck_cod_settlements_amount", "amount > 0");
                    table.CheckConstraint("ck_cod_settlements_status", "status IN (1, 2, 3)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_cod_collections_order",
                table: "cod_collections",
                columns: new[] { "tenant_id", "cod_order_id" });

            migrationBuilder.CreateIndex(
                name: "ux_cod_orders_tenant_id",
                table: "cod_orders",
                columns: new[] { "tenant_id", "id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_cod_orders_tenant_waybill",
                table: "cod_orders",
                columns: new[] { "tenant_id", "waybill_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cod_settlements_order",
                table: "cod_settlements",
                columns: new[] { "tenant_id", "cod_order_id" });
            migrationBuilder.AddForeignKey(
                name: "fk_cod_orders_tenant",
                table: "cod_orders", column: "tenant_id",
                principalTable: "tenants", principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_cod_orders_tenant_waybill",
                table: "cod_orders", columns: new[] { "tenant_id", "waybill_id" },
                principalTable: "waybills", principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_cod_collections_tenant",
                table: "cod_collections", column: "tenant_id",
                principalTable: "tenants", principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_cod_collections_tenant_order",
                table: "cod_collections", columns: new[] { "tenant_id", "cod_order_id" },
                principalTable: "cod_orders", principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_cod_settlements_tenant",
                table: "cod_settlements", column: "tenant_id",
                principalTable: "tenants", principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_cod_settlements_tenant_order",
                table: "cod_settlements", columns: new[] { "tenant_id", "cod_order_id" },
                principalTable: "cod_orders", principalColumns: new[] { "tenant_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cod_collections");

            migrationBuilder.DropTable(
                name: "cod_orders");

            migrationBuilder.DropTable(
                name: "cod_settlements");
        }
    }
}
