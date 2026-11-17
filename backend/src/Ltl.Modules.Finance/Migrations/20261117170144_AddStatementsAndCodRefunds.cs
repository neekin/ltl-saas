using System;
using Ltl.Modules.Finance.Domain;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Finance.Migrations
{
    /// <inheritdoc />
    public partial class AddStatementsAndCodRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cod_refund_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    operator_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cod_refund_records", x => x.id);
                    table.CheckConstraint("ck_cod_refund_records_amount", "amount > 0");
                });

            migrationBuilder.CreateTable(
                name: "customer_statement_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    statement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receivable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    waybill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    snapshot_settled_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_statement_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customer_statements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    confirmed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_statements", x => x.id);
                    table.CheckConstraint("ck_customer_statements_amount", "total_amount > 0");
                    table.CheckConstraint("ck_customer_statements_status", "status IN (1, 2, 3)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_cod_refund_records_collection",
                table: "cod_refund_records",
                columns: new[] { "tenant_id", "collection_id" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_statement_items_statement",
                table: "customer_statement_items",
                columns: new[] { "tenant_id", "statement_id" });

            migrationBuilder.CreateIndex(
                name: "ux_customer_statements_customer_period",
                table: "customer_statements",
                columns: new[] { "tenant_id", "customer_id", "period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_customer_statements_tenant_id",
                table: "customer_statements",
                columns: new[] { "tenant_id", "id" },
                unique: true);
            migrationBuilder.AddForeignKey(
                name: "fk_customer_statements_tenant",
                table: "customer_statements", column: "tenant_id",
                principalTable: "tenants", principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_customer_statements_tenant_customer",
                table: "customer_statements", columns: new[] { "tenant_id", "customer_id" },
                principalTable: "customers", principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_cod_refund_records_tenant",
                table: "cod_refund_records", column: "tenant_id",
                principalTable: "tenants", principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cod_refund_records");

            migrationBuilder.DropTable(
                name: "customer_statement_items");

            migrationBuilder.DropTable(
                name: "customer_statements");
        }
    }
}
