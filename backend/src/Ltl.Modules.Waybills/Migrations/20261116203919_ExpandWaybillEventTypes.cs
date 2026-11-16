using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Waybills.Migrations
{
    /// <inheritdoc />
    public partial class ExpandWaybillEventTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_waybill_events_type",
                table: "waybill_events");

            migrationBuilder.AddCheckConstraint(
                name: "ck_waybill_events_type",
                table: "waybill_events",
                sql: "event_type IN (1, 2, 3, 4, 5)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_waybill_events_type",
                table: "waybill_events");

            migrationBuilder.AddCheckConstraint(
                name: "ck_waybill_events_type",
                table: "waybill_events",
                sql: "event_type IN (1, 2, 3)");
        }
    }
}
