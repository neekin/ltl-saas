using System;
using Ltl.Modules.Finance.Domain;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Finance.Migrations
{
    /// <inheritdoc />
    public partial class InitialFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receivable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_allocations", x => x.id);
                    table.CheckConstraint("ck_payment_allocations_amount", "amount > 0");
                });

            migrationBuilder.CreateTable(
                name: "payment_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    waybill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    method = table.Column<short>(type: "smallint", nullable: false),
                    operator_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_records", x => x.id);
                    table.CheckConstraint("ck_payment_records_amount", "amount > 0");
                    table.CheckConstraint("ck_payment_records_method", "method IN (1, 2, 3)");
                });

            migrationBuilder.CreateTable(
                name: "receivables",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    waybill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    settled_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("pk_receivables", x => x.id);
                    table.CheckConstraint("ck_receivables_amount", "amount > 0");
                    table.CheckConstraint("ck_receivables_settled", "settled_amount >= 0 AND settled_amount <= amount");
                    table.CheckConstraint("ck_receivables_status", "status IN (1, 2, 3)");
                });

            migrationBuilder.CreateTable(
                name: "refund_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    operator_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refund_records", x => x.id);
                    table.CheckConstraint("ck_refund_records_amount", "amount > 0");
                });

            migrationBuilder.CreateIndex(
                name: "ux_payment_allocations_payment_receivable",
                table: "payment_allocations",
                columns: new[] { "tenant_id", "payment_id", "receivable_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_records_waybill",
                table: "payment_records",
                columns: new[] { "tenant_id", "waybill_id" });

            migrationBuilder.CreateIndex(
                name: "ux_receivables_tenant_id",
                table: "receivables",
                columns: new[] { "tenant_id", "id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_receivables_tenant_waybill",
                table: "receivables",
                columns: new[] { "tenant_id", "waybill_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refund_records_payment",
                table: "refund_records",
                columns: new[] { "tenant_id", "payment_id" });
            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "code", "name", "module" },
                columnTypes: new[] { "uuid", "character varying(100)", "character varying(200)", "character varying(64)" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000001001"), FinancePermissionCodes.FinanceRead, "查看应收/收款/退款", FinancePermissionCodes.Module },
                    { new Guid("00000000-0000-0000-0000-000000001002"), FinancePermissionCodes.FinanceManage, "管理收款/退款/核销", FinancePermissionCodes.Module },
                    { new Guid("00000000-0000-0000-0000-000000001003"), "cod.read", "查看 COD 订单/收款/结算", "cod" },
                    { new Guid("00000000-0000-0000-0000-000000001004"), "cod.manage", "管理 COD 收款/结算/付款", "cod" },
                });

            migrationBuilder.AddForeignKey(
                name: "fk_receivables_tenant",
                table: "receivables", column: "tenant_id",
                principalTable: "tenants", principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_receivables_tenant_waybill",
                table: "receivables", columns: new[] { "tenant_id", "waybill_id" },
                principalTable: "waybills", principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_payment_records_tenant",
                table: "payment_records", column: "tenant_id",
                principalTable: "tenants", principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_payment_records_tenant_waybill",
                table: "payment_records", columns: new[] { "tenant_id", "waybill_id" },
                principalTable: "waybills", principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_refund_records_tenant",
                table: "refund_records", column: "tenant_id",
                principalTable: "tenants", principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_allocations");

            migrationBuilder.DropTable(
                name: "payment_records");

            migrationBuilder.DropTable(
                name: "receivables");

            migrationBuilder.DeleteData(
                table: "permissions", keyColumn: "id", keyColumnType: "uuid",
                keyValue: new Guid("00000000-0000-0000-0000-000000001001"));
            migrationBuilder.DeleteData(
                table: "permissions", keyColumn: "id", keyColumnType: "uuid",
                keyValue: new Guid("00000000-0000-0000-0000-000000001002"));

            migrationBuilder.DropTable(
                name: "refund_records");
        }
    }
}
