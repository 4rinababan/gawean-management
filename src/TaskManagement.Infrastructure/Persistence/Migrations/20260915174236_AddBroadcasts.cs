using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBroadcasts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "broadcasts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ImageStorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TargetOrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_broadcasts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "broadcast_dismissals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BroadcastId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_broadcast_dismissals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_broadcast_dismissals_broadcasts_BroadcastId",
                        column: x => x.BroadcastId,
                        principalTable: "broadcasts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "broadcast_target_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BroadcastId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_broadcast_target_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_broadcast_target_users_broadcasts_BroadcastId",
                        column: x => x.BroadcastId,
                        principalTable: "broadcasts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_broadcast_dismissals_BroadcastId_UserId",
                table: "broadcast_dismissals",
                columns: new[] { "BroadcastId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_broadcast_dismissals_UserId",
                table: "broadcast_dismissals",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_broadcast_target_users_BroadcastId_UserId",
                table: "broadcast_target_users",
                columns: new[] { "BroadcastId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_broadcasts_IsActive_CreatedAt",
                table: "broadcasts",
                columns: new[] { "IsActive", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "broadcast_dismissals");

            migrationBuilder.DropTable(
                name: "broadcast_target_users");

            migrationBuilder.DropTable(
                name: "broadcasts");
        }
    }
}
