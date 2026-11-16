using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Platform.Migrations
{
    /// <inheritdoc />
    public partial class InitialPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "branch_sequences",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_type = table.Column<short>(type: "smallint", nullable: false),
                    period = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    current_value = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_branch_sequences", x => new { x.tenant_id, x.branch_id, x.doc_type, x.period });
                    table.CheckConstraint("ck_branch_sequences_current_value", "current_value >= 0");
                });

            // ===== 数据库层租户一致性约束（SEQ-001 设计文档第 3 节） =====
            migrationBuilder.AddForeignKey(
                name: "fk_branch_sequences_tenant",
                table: "branch_sequences",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");

            // 复合外键：序号行引用的网点必须属于本租户（branches 唯一索引由 ORG-001 迁移建立）
            migrationBuilder.AddForeignKey(
                name: "fk_branch_sequences_tenant_branch",
                table: "branch_sequences",
                columns: new[] { "tenant_id", "branch_id" },
                principalTable: "branches",
                principalColumns: new[] { "tenant_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "branch_sequences");
        }
    }
}
