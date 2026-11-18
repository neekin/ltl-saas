using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Finance.Migrations
{
    /// <inheritdoc />
    public partial class AddCommandIdempotencyKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "idempotency_key",
                table: "payment_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "idempotency_key",
                table: "cod_collections",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_payment_records_idempotency",
                table: "payment_records",
                columns: new[] { "tenant_id", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_cod_collections_idempotency",
                table: "cod_collections",
                columns: new[] { "tenant_id", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_payment_records_idempotency",
                table: "payment_records");

            migrationBuilder.DropIndex(
                name: "ux_cod_collections_idempotency",
                table: "cod_collections");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "payment_records");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "cod_collections");
        }
    }
}
