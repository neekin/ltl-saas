using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Routing.Migrations
{
    /// <inheritdoc />
    public partial class InitialRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "route_stops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    remark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_route_stops", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "routes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    origin_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_routes", x => x.id);
                    table.CheckConstraint("ck_routes_status", "status IN (0, 1)");
                });

            migrationBuilder.CreateTable(
                name: "transport_plan_segments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    from_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transport_plan_segments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transport_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_transport_plans", x => x.id);
                    table.CheckConstraint("ck_transport_plans_status", "status IN (0, 1)");
                });

            migrationBuilder.CreateIndex(
                name: "ux_route_stops_tenant_route_branch",
                table: "route_stops",
                columns: new[] { "tenant_id", "route_id", "branch_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_route_stops_tenant_route_sequence",
                table: "route_stops",
                columns: new[] { "tenant_id", "route_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_routes_tenant_code",
                table: "routes",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_routes_tenant_id",
                table: "routes",
                columns: new[] { "tenant_id", "id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_transport_plan_segments_tenant_plan_sequence",
                table: "transport_plan_segments",
                columns: new[] { "tenant_id", "plan_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_transport_plans_tenant_id",
                table: "transport_plans",
                columns: new[] { "tenant_id", "id" },
                unique: true);

            // ===== 数据库层租户一致性约束（AGENTS.md §5.1、ROUTE-001 设计文档第 5 节） =====
            // 手工维护：EF 模型不配置跨模块关系；branches 唯一索引由 ORG-001 迁移建立。

            migrationBuilder.AddForeignKey(
                name: "fk_routes_tenant",
                table: "routes",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_route_stops_tenant",
                table: "route_stops",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_transport_plans_tenant",
                table: "transport_plans",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_transport_plan_segments_tenant",
                table: "transport_plan_segments",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_routes_tenant_origin_branch",
                table: "routes",
                columns: new[] { "tenant_id", "origin_branch_id" },
                principalTable: "branches",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_routes_tenant_destination_branch",
                table: "routes",
                columns: new[] { "tenant_id", "destination_branch_id" },
                principalTable: "branches",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_route_stops_tenant_route",
                table: "route_stops",
                columns: new[] { "tenant_id", "route_id" },
                principalTable: "routes",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_route_stops_tenant_branch",
                table: "route_stops",
                columns: new[] { "tenant_id", "branch_id" },
                principalTable: "branches",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_transport_plans_tenant_route",
                table: "transport_plans",
                columns: new[] { "tenant_id", "route_id" },
                principalTable: "routes",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_transport_plan_segments_tenant_plan",
                table: "transport_plan_segments",
                columns: new[] { "tenant_id", "plan_id" },
                principalTable: "transport_plans",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_transport_plan_segments_tenant_from_branch",
                table: "transport_plan_segments",
                columns: new[] { "tenant_id", "from_branch_id" },
                principalTable: "branches",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_transport_plan_segments_tenant_to_branch",
                table: "transport_plan_segments",
                columns: new[] { "tenant_id", "to_branch_id" },
                principalTable: "branches",
                principalColumns: new[] { "tenant_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 删除顺序遵循外键依赖：先区段/站点，再方案/线路
            migrationBuilder.DropTable(
                name: "transport_plan_segments");

            migrationBuilder.DropTable(
                name: "route_stops");

            migrationBuilder.DropTable(
                name: "transport_plans");

            migrationBuilder.DropTable(
                name: "routes");
        }
    }
}
