using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Transport.Migrations
{
    /// <inheritdoc />
    public partial class InitialTrips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transport_trip_segments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seq = table.Column<int>(type: "integer", nullable: false),
                    from_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transport_trip_segments", x => x.id);
                    table.CheckConstraint("ck_transport_trip_segments_seq", "seq >= 1");
                });

            migrationBuilder.CreateTable(
                name: "transport_trip_waybills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_id = table.Column<Guid>(type: "uuid", nullable: false),
                    waybill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    loaded_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    loaded_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transport_trip_waybills", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transport_trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_no = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origin_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dest_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transport_trips", x => x.id);
                    table.CheckConstraint("ck_transport_trips_status", "status IN (1)");
                });

            migrationBuilder.CreateIndex(
                name: "ux_transport_trip_segments_trip_seq",
                table: "transport_trip_segments",
                columns: new[] { "tenant_id", "trip_id", "seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_transport_trip_waybills_active_waybill",
                table: "transport_trip_waybills",
                columns: new[] { "tenant_id", "waybill_id" },
                unique: true,
                filter: "active = true");

            migrationBuilder.CreateIndex(
                name: "ux_transport_trip_waybills_trip_waybill",
                table: "transport_trip_waybills",
                columns: new[] { "tenant_id", "trip_id", "waybill_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_transport_trips_tenant_id",
                table: "transport_trips",
                columns: new[] { "tenant_id", "id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_transport_trips_tenant_no",
                table: "transport_trips",
                columns: new[] { "tenant_id", "trip_no" },
                unique: true);
            // 手写复合外键（AGENTS §5.1）
            migrationBuilder.AddForeignKey(
                name: "fk_transport_trips_tenant",
                table: "transport_trips",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_transport_trips_tenant_vehicle",
                table: "transport_trips",
                columns: new[] { "tenant_id", "vehicle_id" },
                principalTable: "vehicles",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_transport_trips_tenant_driver",
                table: "transport_trips",
                columns: new[] { "tenant_id", "driver_id" },
                principalTable: "drivers",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_transport_trips_tenant_origin_branch",
                table: "transport_trips",
                columns: new[] { "tenant_id", "origin_branch_id" },
                principalTable: "branches",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_transport_trips_tenant_dest_branch",
                table: "transport_trips",
                columns: new[] { "tenant_id", "dest_branch_id" },
                principalTable: "branches",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_transport_trip_segments_tenant",
                table: "transport_trip_segments",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_transport_trip_segments_tenant_trip",
                table: "transport_trip_segments",
                columns: new[] { "tenant_id", "trip_id" },
                principalTable: "transport_trips",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_transport_trip_segments_tenant_from_branch",
                table: "transport_trip_segments",
                columns: new[] { "tenant_id", "from_branch_id" },
                principalTable: "branches",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_transport_trip_segments_tenant_to_branch",
                table: "transport_trip_segments",
                columns: new[] { "tenant_id", "to_branch_id" },
                principalTable: "branches",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_transport_trip_waybills_tenant",
                table: "transport_trip_waybills",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_transport_trip_waybills_tenant_trip",
                table: "transport_trip_waybills",
                columns: new[] { "tenant_id", "trip_id" },
                principalTable: "transport_trips",
                principalColumns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_transport_trip_waybills_tenant_waybill",
                table: "transport_trip_waybills",
                columns: new[] { "tenant_id", "waybill_id" },
                principalTable: "waybills",
                principalColumns: new[] { "tenant_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transport_trip_segments");

            migrationBuilder.DropTable(
                name: "transport_trip_waybills");

            migrationBuilder.DropTable(
                name: "transport_trips");
        }
    }
}
