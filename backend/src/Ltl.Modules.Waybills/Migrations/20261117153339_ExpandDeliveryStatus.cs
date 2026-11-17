using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Waybills.Migrations
{
    /// <inheritdoc />
    public partial class ExpandDeliveryStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_waybills_delivery_status",
                table: "waybills");

            migrationBuilder.AddCheckConstraint(
                name: "ck_waybills_delivery_status",
                table: "waybills",
                sql: "delivery_status IN (1, 2, 3, 4)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_waybills_delivery_status",
                table: "waybills");

            migrationBuilder.AddCheckConstraint(
                name: "ck_waybills_delivery_status",
                table: "waybills",
                sql: "delivery_status IN (1)");
        }
    }
}
