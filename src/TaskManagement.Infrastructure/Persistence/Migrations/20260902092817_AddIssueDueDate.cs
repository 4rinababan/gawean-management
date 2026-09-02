using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueDueDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "issues",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_issues_AssigneeUserId_Status_DueDate",
                table: "issues",
                columns: new[] { "AssigneeUserId", "Status", "DueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_issues_AssigneeUserId_Status_DueDate",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "issues");
        }
    }
}
