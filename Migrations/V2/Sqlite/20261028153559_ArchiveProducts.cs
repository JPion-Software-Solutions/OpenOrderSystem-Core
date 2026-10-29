using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenOrderSystem.Core.Migrations.V2.Sqlite
{
    /// <inheritdoc />
    public partial class ArchiveProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderLines_ProductSnapshots_ProductSnapshotId",
                table: "OrderLines");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductSnapshots_Products_ProductId",
                table: "ProductSnapshots");

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Product_IsArchived",
                table: "Products",
                column: "IsArchived");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderLines_ProductSnapshots_ProductSnapshotId",
                table: "OrderLines",
                column: "ProductSnapshotId",
                principalTable: "ProductSnapshots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductSnapshots_Products_ProductId",
                table: "ProductSnapshots",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderLines_ProductSnapshots_ProductSnapshotId",
                table: "OrderLines");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductSnapshots_Products_ProductId",
                table: "ProductSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_Product_IsArchived",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Products");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderLines_ProductSnapshots_ProductSnapshotId",
                table: "OrderLines",
                column: "ProductSnapshotId",
                principalTable: "ProductSnapshots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductSnapshots_Products_ProductId",
                table: "ProductSnapshots",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
