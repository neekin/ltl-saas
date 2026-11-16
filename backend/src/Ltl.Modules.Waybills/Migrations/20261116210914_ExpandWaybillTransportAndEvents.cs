using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Waybills.Migrations
{
    /// <inheritdoc />
    public partial class ExpandWaybillTransportAndEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_waybills_transport_status",
                table: "waybills");

            migrationBuilder.DropCheckConstraint(
                name: "ck_waybill_events_type",
                table: "waybill_events");

            migrationBuilder.AddCheckConstraint(
                name: "ck_waybills_transport_status",
                table: "waybills",
                sql: "transport_status IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_waybill_events_type",
                table: "waybill_events",
                sql: "event_type IN (1, 2, 3, 4, 5, 6, 7)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_waybills_transport_status",
                table: "waybills");

            migrationBuilder.DropCheckConstraint(
                name: "ck_waybill_events_type",
                table: "waybill_events");

            migrationBuilder.AddCheckConstraint(
                name: "ck_waybills_transport_status",
                table: "waybills",
                sql: "transport_status IN (1)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_waybill_events_type",
                table: "waybill_events",
                sql: "event_type IN (1, 2, 3, 4, 5)");
        }
    }
}
