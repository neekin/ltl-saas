using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Pricing.Migrations
{
    /// <inheritdoc />
    public partial class InitialPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pricing_rule_set_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    definition = table.Column<string>(type: "jsonb", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    published_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pricing_rule_set_versions", x => x.id);
                    table.CheckConstraint("ck_pricing_rule_set_versions_number", "version_number >= 1");
                    table.CheckConstraint("ck_pricing_rule_set_versions_status", "status IN (0, 1, 2)");
                });

            migrationBuilder.CreateTable(
                name: "pricing_rule_sets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
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
                    table.PrimaryKey("pk_pricing_rule_sets", x => x.id);
                    table.CheckConstraint("ck_pricing_rule_sets_status", "status IN (1, 2)");
                });

            migrationBuilder.CreateIndex(
                name: "ux_pricing_rule_set_versions_current_published",
                table: "pricing_rule_set_versions",
                columns: new[] { "rule_set_id", "tenant_id" },
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ux_pricing_rule_set_versions_draft",
                table: "pricing_rule_set_versions",
                columns: new[] { "tenant_id", "rule_set_id" },
                unique: true,
                filter: "status = 0");

            migrationBuilder.CreateIndex(
                name: "ux_pricing_rule_set_versions_set_version",
                table: "pricing_rule_set_versions",
                columns: new[] { "tenant_id", "rule_set_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pricing_rule_sets_tenant_code",
                table: "pricing_rule_sets",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pricing_rule_sets_tenant_id",
                table: "pricing_rule_sets",
                columns: new[] { "tenant_id", "id" },
                unique: true);

            // 手写复合外键：规则集租户一致性（跨模块 → tenants，AGENTS §5.1）
            migrationBuilder.AddForeignKey(
                name: "fk_pricing_rule_sets_tenant",
                table: "pricing_rule_sets",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");

            // 手写复合外键：版本必须引用本租户的规则集（(tenant_id, rule_set_id) → sets(tenant_id, id)）
            migrationBuilder.AddForeignKey(
                name: "fk_pricing_rule_set_versions_rule_set",
                table: "pricing_rule_set_versions",
                columns: new[] { "tenant_id", "rule_set_id" },
                principalTable: "pricing_rule_sets",
                principalColumns: new[] { "tenant_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pricing_rule_set_versions");

            migrationBuilder.DropTable(
                name: "pricing_rule_sets");
        }
    }
}
