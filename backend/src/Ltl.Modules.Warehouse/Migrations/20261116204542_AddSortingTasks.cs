using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Warehouse.Migrations
{
    /// <inheritdoc />
    public partial class AddSortingTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cargo_handling_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    waybill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_type = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    operator_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    completed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cargo_handling_tasks", x => x.id);
                    table.CheckConstraint("ck_cargo_handling_tasks_status", "status IN (1, 2)");
                    table.CheckConstraint("ck_cargo_handling_tasks_type", "task_type IN (1)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_cargo_handling_tasks_waybill",
                table: "cargo_handling_tasks",
                columns: new[] { "tenant_id", "waybill_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_cargo_handling_tasks_active",
                table: "cargo_handling_tasks",
                columns: new[] { "tenant_id", "waybill_id" },
                unique: true,
                filter: "status = 1");
            // 手写复合外键（AGENTS §5.1）
            migrationBuilder.AddForeignKey(
                name: "fk_cargo_handling_tasks_tenant",
                table: "cargo_handling_tasks",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_cargo_handling_tasks_tenant_waybill",
                table: "cargo_handling_tasks",
                columns: new[] { "tenant_id", "waybill_id" },
                principalTable: "waybills",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_cargo_handling_tasks_tenant_branch",
                table: "cargo_handling_tasks",
                columns: new[] { "tenant_id", "branch_id" },
                principalTable: "branches",
                principalColumns: new[] { "tenant_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cargo_handling_tasks");
        }
    }
}
