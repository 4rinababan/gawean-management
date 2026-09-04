using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueAiNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "issue_ai_notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueId = table.Column<Guid>(type: "uuid", nullable: false),
                    AskedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Question = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Answer = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issue_ai_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_issue_ai_notes_issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_issue_ai_notes_IssueId",
                table: "issue_ai_notes",
                column: "IssueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "issue_ai_notes");
        }
    }
}
