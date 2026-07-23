using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SaveSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OriginalFilePath = table.Column<string>(type: "TEXT", nullable: false),
                    LastBackupTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GameVersionToken = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaveSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemId = table.Column<string>(type: "TEXT", nullable: false),
                    SlotIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    SaveSessionId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryItems_SaveSessions_SaveSessionId",
                        column: x => x.SaveSessionId,
                        principalTable: "SaveSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventorySlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContainerType = table.Column<string>(type: "TEXT", nullable: false),
                    ContainerId = table.Column<string>(type: "TEXT", nullable: false),
                    SlotType = table.Column<string>(type: "TEXT", nullable: false),
                    XIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    YIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemId = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxAmount = table.Column<int>(type: "INTEGER", nullable: false),
                    SaveSessionId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventorySlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventorySlots_SaveSessions_SaveSessionId",
                        column: x => x.SaveSessionId,
                        principalTable: "SaveSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SaveSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    GameVersionToken = table.Column<string>(type: "TEXT", nullable: false),
                    Units = table.Column<long>(type: "INTEGER", nullable: false),
                    Nanites = table.Column<long>(type: "INTEGER", nullable: false),
                    Quicksilver = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerStates_SaveSessions_SaveSessionId",
                        column: x => x.SaveSessionId,
                        principalTable: "SaveSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_SaveSessionId",
                table: "InventoryItems",
                column: "SaveSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySlots_SaveSessionId",
                table: "InventorySlots",
                column: "SaveSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStates_SaveSessionId",
                table: "PlayerStates",
                column: "SaveSessionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "InventorySlots");

            migrationBuilder.DropTable(
                name: "PlayerStates");

            migrationBuilder.DropTable(
                name: "SaveSessions");
        }
    }
}
