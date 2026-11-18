using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Delivery.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "assigned_at",
                table: "delivery_orders",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "assigned_by",
                table: "delivery_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "assignee_user_id",
                table: "delivery_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_orders_assignee",
                table: "delivery_orders",
                columns: new[] { "tenant_id", "assignee_user_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_delivery_orders_assignee",
                table: "delivery_orders");

            migrationBuilder.DropColumn(
                name: "assigned_at",
                table: "delivery_orders");

            migrationBuilder.DropColumn(
                name: "assigned_by",
                table: "delivery_orders");

            migrationBuilder.DropColumn(
                name: "assignee_user_id",
                table: "delivery_orders");
        }
    }
}
