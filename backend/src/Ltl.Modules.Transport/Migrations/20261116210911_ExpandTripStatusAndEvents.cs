using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Transport.Migrations
{
    /// <inheritdoc />
    public partial class ExpandTripStatusAndEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_transport_trips_status",
                table: "transport_trips");

            migrationBuilder.CreateTable(
                name: "transport_trip_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    detail = table.Column<string>(type: "jsonb", nullable: true),
                    operator_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transport_trip_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transport_trip_fees",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fee_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    operator_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transport_trip_fees", x => x.id);
                    table.CheckConstraint("ck_transport_trip_fees_amount", "amount >= 0");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_transport_trips_status",
                table: "transport_trips",
                sql: "status IN (1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "ix_transport_trip_events_trip",
                table: "transport_trip_events",
                columns: new[] { "tenant_id", "trip_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_transport_trip_fees_trip",
                table: "transport_trip_fees",
                columns: new[] { "tenant_id", "trip_id" });
            // 手写复合外键（AGENTS §5.1）
            migrationBuilder.AddForeignKey(
                name: "fk_transport_trip_fees_tenant",
                table: "transport_trip_fees",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_transport_trip_fees_tenant_trip",
                table: "transport_trip_fees",
                columns: new[] { "tenant_id", "trip_id" },
                principalTable: "transport_trips",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_transport_trip_events_tenant",
                table: "transport_trip_events",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_transport_trip_events_tenant_trip",
                table: "transport_trip_events",
                columns: new[] { "tenant_id", "trip_id" },
                principalTable: "transport_trips",
                principalColumns: new[] { "tenant_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transport_trip_events");

            migrationBuilder.DropTable(
                name: "transport_trip_fees");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transport_trips_status",
                table: "transport_trips");

            migrationBuilder.AddCheckConstraint(
                name: "ck_transport_trips_status",
                table: "transport_trips",
                sql: "status IN (1)");
        }
    }
}
