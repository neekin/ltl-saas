using System;
using Ltl.Modules.Warehouse.Domain;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Warehouse.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "branch_transfers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transfer_no = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    from_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    submitted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    dispatched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dispatched_by = table.Column<Guid>(type: "uuid", nullable: true),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    received_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_branch_transfers", x => x.id);
                    table.CheckConstraint("ck_branch_transfers_status", "status IN (1, 2, 3, 4, 5)");
                });

            migrationBuilder.CreateTable(
                name: "branch_transfer_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transfer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seq = table.Column<int>(type: "integer", nullable: false),
                    cargo_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    declared_quantity = table.Column<int>(type: "integer", nullable: false),
                    received_quantity = table.Column<int>(type: "integer", nullable: true),
                    broken_quantity = table.Column<int>(type: "integer", nullable: true),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    branch_transfer_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_branch_transfer_items", x => x.id);
                    table.CheckConstraint("ck_branch_transfer_items_broken", "broken_quantity IS NULL OR broken_quantity >= 0");
                    table.CheckConstraint("ck_branch_transfer_items_declared", "declared_quantity >= 1");
                    table.CheckConstraint("ck_branch_transfer_items_received", "received_quantity IS NULL OR received_quantity >= 0");
                    table.CheckConstraint("ck_branch_transfer_items_seq", "seq >= 1");
                    table.ForeignKey(
                        name: "fk_branch_transfer_items_branch_transfers_branch_transfer_id",
                        column: x => x.branch_transfer_id,
                        principalTable: "branch_transfers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_branch_transfer_items_branch_transfer_id",
                table: "branch_transfer_items",
                column: "branch_transfer_id");

            migrationBuilder.CreateIndex(
                name: "ux_branch_transfer_items_transfer_seq",
                table: "branch_transfer_items",
                columns: new[] { "tenant_id", "transfer_id", "seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_branch_transfers_tenant_id",
                table: "branch_transfers",
                columns: new[] { "tenant_id", "id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_branch_transfers_tenant_no",
                table: "branch_transfers",
                columns: new[] { "tenant_id", "transfer_no" },
                unique: true);
            // 权限点 seed（调拨：0501/0502）
            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "code", "name", "module" },
                columnTypes: new[] { "uuid", "character varying(100)", "character varying(200)", "character varying(64)" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000204"), WarehousePermissionCodes.TransferRead, "查看网点调拨", WarehousePermissionCodes.Module },
                    { new Guid("00000000-0000-0000-0000-000000000205"), WarehousePermissionCodes.TransferManage, "管理网点调拨（创建/提交/发出/验收）", WarehousePermissionCodes.Module },
                });

            // 手写复合外键（AGENTS §5.1）
            migrationBuilder.AddForeignKey(
                name: "fk_branch_transfers_tenant",
                table: "branch_transfers",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_branch_transfers_tenant_from_branch",
                table: "branch_transfers",
                columns: new[] { "tenant_id", "from_branch_id" },
                principalTable: "branches",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_branch_transfers_tenant_to_branch",
                table: "branch_transfers",
                columns: new[] { "tenant_id", "to_branch_id" },
                principalTable: "branches",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_branch_transfer_items_tenant",
                table: "branch_transfer_items",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_branch_transfer_items_tenant_transfer",
                table: "branch_transfer_items",
                columns: new[] { "tenant_id", "transfer_id" },
                principalTable: "branch_transfers",
                principalColumns: new[] { "tenant_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyColumnType: "uuid",
                keyValue: new Guid("00000000-0000-0000-0000-000000000204"));
            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyColumnType: "uuid",
                keyValue: new Guid("00000000-0000-0000-0000-000000000205"));

            migrationBuilder.DropTable(
                name: "branch_transfer_items");

            migrationBuilder.DropTable(
                name: "branch_transfers");
        }
    }
}
