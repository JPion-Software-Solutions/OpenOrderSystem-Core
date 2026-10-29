using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenOrderSystem.Core.Migrations.V2.SqlServer
{
    /// <inheritdoc />
    public partial class OrderingFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "OrderNumber",
                table: "Orders",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<Guid>(
                name: "EntryStageId",
                table: "OrderQueue",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_OrderQueue_EntryStageId",
                table: "OrderQueue",
                column: "EntryStageId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderQueue_OrderStages_EntryStageId",
                table: "OrderQueue",
                column: "EntryStageId",
                principalTable: "OrderStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderQueue_OrderStages_EntryStageId",
                table: "OrderQueue");

            migrationBuilder.DropIndex(
                name: "IX_OrderQueue_EntryStageId",
                table: "OrderQueue");

            migrationBuilder.DropColumn(
                name: "EntryStageId",
                table: "OrderQueue");

            migrationBuilder.AlterColumn<long>(
                name: "OrderNumber",
                table: "Orders",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
