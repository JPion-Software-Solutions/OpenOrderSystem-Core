using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenOrderSystem.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddedDefaultPrinterAndPrintTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DefaultEndOfDayTemplate",
                table: "PrintTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultOrderTemplate",
                table: "PrintTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultEndOfDayPrinter",
                table: "Printers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultOrderPrinter",
                table: "Printers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultEndOfDayTemplate",
                table: "PrintTemplates");

            migrationBuilder.DropColumn(
                name: "DefaultOrderTemplate",
                table: "PrintTemplates");

            migrationBuilder.DropColumn(
                name: "DefaultEndOfDayPrinter",
                table: "Printers");

            migrationBuilder.DropColumn(
                name: "DefaultOrderPrinter",
                table: "Printers");
        }
    }
}
