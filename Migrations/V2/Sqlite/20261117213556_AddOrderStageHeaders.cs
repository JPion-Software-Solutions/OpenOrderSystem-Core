using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenOrderSystem.Core.Migrations.V2.Sqlite
{
    /// <inheritdoc />
    public partial class AddOrderStageHeaders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HeaderId",
                table: "OrderStages",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ShadowAnchorStageId",
                table: "OrderStages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrderStageHeaders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    ShadowOfHeaderId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsProtected = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStageHeaders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderStageHeaders_OrderStageHeaders_ShadowOfHeaderId",
                        column: x => x.ShadowOfHeaderId,
                        principalTable: "OrderStageHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderStages_HeaderId",
                table: "OrderStages",
                column: "HeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStages_ShadowAnchorStageId",
                table: "OrderStages",
                column: "ShadowAnchorStageId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStageHeaders_ShadowOfHeaderId",
                table: "OrderStageHeaders",
                column: "ShadowOfHeaderId");

            // Existing stages predate headers: put them all on one main chain so the FK below holds.
            // Fresh databases have no stages, so no header is created for them.
            migrationBuilder.Sql("INSERT INTO \"OrderStageHeaders\" (\"Id\", \"Name\", \"Description\", \"ShadowOfHeaderId\", \"IsProtected\") SELECT '5E0C2A1D-6B7F-4C3E-9A1B-0D2F8E4C7A90', 'Default', 'Created when stage chains gained headers; holds the stages that existed before.', NULL, 0 WHERE EXISTS (SELECT 1 FROM \"OrderStages\");");
            migrationBuilder.Sql("UPDATE \"OrderStages\" SET \"HeaderId\" = '5E0C2A1D-6B7F-4C3E-9A1B-0D2F8E4C7A90';");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderStages_OrderStageHeaders_HeaderId",
                table: "OrderStages",
                column: "HeaderId",
                principalTable: "OrderStageHeaders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderStages_OrderStages_ShadowAnchorStageId",
                table: "OrderStages",
                column: "ShadowAnchorStageId",
                principalTable: "OrderStages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderStages_OrderStageHeaders_HeaderId",
                table: "OrderStages");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderStages_OrderStages_ShadowAnchorStageId",
                table: "OrderStages");

            migrationBuilder.DropTable(
                name: "OrderStageHeaders");

            migrationBuilder.DropIndex(
                name: "IX_OrderStages_HeaderId",
                table: "OrderStages");

            migrationBuilder.DropIndex(
                name: "IX_OrderStages_ShadowAnchorStageId",
                table: "OrderStages");

            migrationBuilder.DropColumn(
                name: "HeaderId",
                table: "OrderStages");

            migrationBuilder.DropColumn(
                name: "ShadowAnchorStageId",
                table: "OrderStages");
        }
    }
}
