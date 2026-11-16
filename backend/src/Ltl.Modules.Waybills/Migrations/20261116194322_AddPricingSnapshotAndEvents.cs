using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Waybills.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingSnapshotAndEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "idempotency_key",
                table: "waybills",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "waybill_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    waybill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<short>(type: "smallint", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    operator_id = table.Column<Guid>(type: "uuid", nullable: true),
                    from_status = table.Column<short>(type: "smallint", nullable: true),
                    to_status = table.Column<short>(type: "smallint", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_waybill_events", x => x.id);
                    table.CheckConstraint("ck_waybill_events_from_status", "from_status IS NULL OR from_status IN (1, 2, 3)");
                    table.CheckConstraint("ck_waybill_events_to_status", "to_status IN (1, 2, 3)");
                    table.CheckConstraint("ck_waybill_events_type", "event_type IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "fk_waybill_events_waybills_tenant_id_waybill_id",
                        columns: x => new { x.tenant_id, x.waybill_id },
                        principalTable: "waybills",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "waybill_pricing_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    waybill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_set_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    input_json = table.Column<string>(type: "jsonb", nullable: false),
                    quote_json = table.Column<string>(type: "jsonb", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_waybill_pricing_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_waybill_pricing_snapshots_waybills_tenant_id_waybill_id",
                        columns: x => new { x.tenant_id, x.waybill_id },
                        principalTable: "waybills",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_waybills_tenant_idempotency",
                table: "waybills",
                columns: new[] { "tenant_id", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_waybill_events_waybill_occurred",
                table: "waybill_events",
                columns: new[] { "tenant_id", "waybill_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_waybill_pricing_snapshots_waybill",
                table: "waybill_pricing_snapshots",
                columns: new[] { "tenant_id", "waybill_id" });
     
            // 手写复合外键：跨模块 → tenants（AGENTS §5.1）
            migrationBuilder.AddForeignKey(
                name: "fk_waybill_events_tenant",
                table: "waybill_events",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_waybill_pricing_snapshots_tenant",
                table: "waybill_pricing_snapshots",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "waybill_events");

            migrationBuilder.DropTable(
                name: "waybill_pricing_snapshots");

            migrationBuilder.DropIndex(
                name: "ux_waybills_tenant_idempotency",
                table: "waybills");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "waybills");
        }
    }
}
