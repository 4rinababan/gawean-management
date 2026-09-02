using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CascadeWorkspaceDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_notifications_OrganizationId",
                table: "notifications",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_organizations_OrganizationId",
                table: "notifications",
                column: "OrganizationId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_projects_organizations_OrganizationId",
                table: "projects",
                column: "OrganizationId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_notifications_organizations_OrganizationId",
                table: "notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_projects_organizations_OrganizationId",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "IX_notifications_OrganizationId",
                table: "notifications");
        }
    }
}
