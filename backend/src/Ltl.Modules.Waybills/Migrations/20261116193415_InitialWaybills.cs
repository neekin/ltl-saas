using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Waybills.Migrations
{
    /// <inheritdoc />
    public partial class InitialWaybills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "waybills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    waybill_no = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    origin_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dest_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_type = table.Column<short>(type: "smallint", nullable: false),
                    waybill_status = table.Column<short>(type: "smallint", nullable: false),
                    cargo_status = table.Column<short>(type: "smallint", nullable: false),
                    transport_status = table.Column<short>(type: "smallint", nullable: false),
                    delivery_status = table.Column<short>(type: "smallint", nullable: false),
                    freight_status = table.Column<short>(type: "smallint", nullable: false),
                    cod_status = table.Column<short>(type: "smallint", nullable: false),
                    cod_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    sender_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sender_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sender_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    receiver_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    receiver_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    receiver_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_waybills", x => x.id);
                    table.UniqueConstraint("ak_waybills_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_waybills_cargo_status", "cargo_status IN (1, 2, 3)");
                    table.CheckConstraint("ck_waybills_cod_amount", "cod_amount IS NULL OR cod_amount >= 0");
                    table.CheckConstraint("ck_waybills_cod_status", "cod_status IN (1, 2, 3, 4, 5)");
                    table.CheckConstraint("ck_waybills_delivery_status", "delivery_status IN (1)");
                    table.CheckConstraint("ck_waybills_freight_status", "freight_status IN (1, 2, 3)");
                    table.CheckConstraint("ck_waybills_payment_type", "payment_type IN (1, 2, 3)");
                    table.CheckConstraint("ck_waybills_status", "waybill_status IN (1, 2, 3)");
                    table.CheckConstraint("ck_waybills_total_amount", "total_amount IS NULL OR total_amount >= 0");
                    table.CheckConstraint("ck_waybills_transport_status", "transport_status IN (1)");
                });

            migrationBuilder.CreateTable(
                name: "waybill_packages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    waybill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_no = table.Column<int>(type: "integer", nullable: false),
                    cargo_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    volume_m3 = table.Column<decimal>(type: "numeric(12,4)", nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_waybill_packages", x => x.id);
                    table.CheckConstraint("ck_waybill_packages_no", "package_no >= 1");
                    table.CheckConstraint("ck_waybill_packages_quantity", "quantity >= 1");
                    table.CheckConstraint("ck_waybill_packages_volume", "volume_m3 >= 0");
                    table.CheckConstraint("ck_waybill_packages_weight", "weight_kg >= 0");
                    table.ForeignKey(
                        name: "fk_waybill_packages_waybills_tenant_id_waybill_id",
                        columns: x => new { x.tenant_id, x.waybill_id },
                        principalTable: "waybills",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_waybill_packages_waybill_no",
                table: "waybill_packages",
                columns: new[] { "tenant_id", "waybill_id", "package_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_waybills_tenant_id",
                table: "waybills",
                columns: new[] { "tenant_id", "id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_waybills_tenant_no",
                table: "waybills",
                columns: new[] { "tenant_id", "waybill_no" },
                unique: true);

            // 手写复合外键：跨模块引用（AGENTS §5.1）
            migrationBuilder.AddForeignKey(
                name: "fk_waybills_tenant",
                table: "waybills",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_waybills_tenant_origin_branch",
                table: "waybills",
                columns: new[] { "tenant_id", "origin_branch_id" },
                principalTable: "branches",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_waybills_tenant_dest_branch",
                table: "waybills",
                columns: new[] { "tenant_id", "dest_branch_id" },
                principalTable: "branches",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_waybills_tenant_customer",
                table: "waybills",
                columns: new[] { "tenant_id", "customer_id" },
                principalTable: "customers",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_waybills_tenant_route",
                table: "waybills",
                columns: new[] { "tenant_id", "route_id" },
                principalTable: "routes",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_waybill_packages_tenant",
                table: "waybill_packages",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "waybill_packages");

            migrationBuilder.DropTable(
                name: "waybills");
        }
    }
}
