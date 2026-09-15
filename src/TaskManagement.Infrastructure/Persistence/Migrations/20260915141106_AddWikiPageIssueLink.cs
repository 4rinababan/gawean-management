using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWikiPageIssueLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IssueId",
                table: "wiki_pages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_IssueId",
                table: "wiki_pages",
                column: "IssueId");

            migrationBuilder.AddForeignKey(
                name: "FK_wiki_pages_issues_IssueId",
                table: "wiki_pages",
                column: "IssueId",
                principalTable: "issues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_wiki_pages_issues_IssueId",
                table: "wiki_pages");

            migrationBuilder.DropIndex(
                name: "IX_wiki_pages_IssueId",
                table: "wiki_pages");

            migrationBuilder.DropColumn(
                name: "IssueId",
                table: "wiki_pages");
        }
    }
}
