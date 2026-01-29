using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenOrderSystem.Core.Migrations
{
    /// <inheritdoc />
    public partial class RemovedActors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Confguration_SystemActors_ActorId",
                table: "Confguration");

            migrationBuilder.DropTable(
                name: "SystemActors");

            migrationBuilder.DropIndex(
                name: "IX_Confguration_ActorId",
                table: "Confguration");

            migrationBuilder.DropColumn(
                name: "ActorId",
                table: "Confguration");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorId",
                table: "Confguration",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "SystemActors",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ActorScope = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemActors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemActors_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Confguration_ActorId",
                table: "Confguration",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemActors_UserId",
                table: "SystemActors",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Confguration_SystemActors_ActorId",
                table: "Confguration",
                column: "ActorId",
                principalTable: "SystemActors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
