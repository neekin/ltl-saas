using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Customers.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerCreditLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "credit_limit",
                table: "customers",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "payment_terms_days",
                table: "customers",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "credit_limit",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "payment_terms_days",
                table: "customers");
        }
    }
}
