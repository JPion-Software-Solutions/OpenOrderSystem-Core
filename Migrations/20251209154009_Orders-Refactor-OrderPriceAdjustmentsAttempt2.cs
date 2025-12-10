using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenOrderSystem.Core.Migrations
{
    /// <inheritdoc />
    public partial class OrdersRefactorOrderPriceAdjustmentsAttempt2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PriceAdjustments",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Totals",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{\"grossSubtotal\":0,\"netSubtotal\":0,\"discount\":0,\"tax\":0,\"total\":0,\"additionalAdjustments\":{}}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PriceAdjustments",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Totals",
                table: "Orders");
        }
    }
}
