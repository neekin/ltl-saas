using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Customers.Migrations
{
    /// <inheritdoc />
    public partial class InitialCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_addresses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_customer_addresses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customer_branch_relations",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_branch_relations", x => new { x.tenant_id, x.customer_id, x.branch_id });
                });

            migrationBuilder.CreateTable(
                name: "customer_contacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_customer_contacts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("pk_customers", x => x.id);
                    table.CheckConstraint("ck_customers_status", "status IN (0, 1)");
                });

            migrationBuilder.CreateIndex(
                name: "ux_customer_addresses_tenant_customer_default",
                table: "customer_addresses",
                columns: new[] { "tenant_id", "customer_id" },
                unique: true,
                filter: "is_default");

            migrationBuilder.CreateIndex(
                name: "ux_customer_contacts_tenant_customer_primary",
                table: "customer_contacts",
                columns: new[] { "tenant_id", "customer_id" },
                unique: true,
                filter: "is_primary");

            migrationBuilder.CreateIndex(
                name: "ux_customers_tenant_code",
                table: "customers",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_customers_tenant_id",
                table: "customers",
                columns: new[] { "tenant_id", "id" },
                unique: true);

            // ===== 数据库层租户一致性约束（AGENTS.md §5.1、CUS-001 设计文档第 4 节） =====
            // 手工维护：EF 模型不配置跨模块关系；FK 保留在 Initial 迁移中，可在空库执行。

            migrationBuilder.AddForeignKey(
                name: "fk_customers_tenant",
                table: "customers",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_customer_contacts_tenant_customer",
                table: "customer_contacts",
                columns: new[] { "tenant_id", "customer_id" },
                principalTable: "customers",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_customer_addresses_tenant_customer",
                table: "customer_addresses",
                columns: new[] { "tenant_id", "customer_id" },
                principalTable: "customers",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_customer_branch_relations_tenant_customer",
                table: "customer_branch_relations",
                columns: new[] { "tenant_id", "customer_id" },
                principalTable: "customers",
                principalColumns: new[] { "tenant_id", "id" });

            // 跨模块引用：branches(tenant_id, id) 唯一索引由 ORG-001 迁移建立
            migrationBuilder.AddForeignKey(
                name: "fk_customer_branch_relations_tenant_branch",
                table: "customer_branch_relations",
                columns: new[] { "tenant_id", "branch_id" },
                principalTable: "branches",
                principalColumns: new[] { "tenant_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 删除顺序遵循外键依赖：先关联子表，再客户主表
            migrationBuilder.DropTable(
                name: "customer_branch_relations");

            migrationBuilder.DropTable(
                name: "customer_addresses");

            migrationBuilder.DropTable(
                name: "customer_contacts");

            migrationBuilder.DropTable(
                name: "customers");
        }
    }
}
