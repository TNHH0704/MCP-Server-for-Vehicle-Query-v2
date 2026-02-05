using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace McpVersionVer2.Migrations
{
    /// <inheritdoc />
    public partial class InitialConversationDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    SessionId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastAccessedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BearerTokenHash = table.Column<string>(type: "TEXT", nullable: true),
                    IsAnonymous = table.Column<bool>(type: "INTEGER", nullable: false),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.SessionId);
                });

            migrationBuilder.CreateTable(
                name: "ConversationEntries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SessionId = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    ToolName = table.Column<string>(type: "TEXT", nullable: true),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationEntries_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConversationSummaries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SessionId = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    SummarySequence = table.Column<int>(type: "INTEGER", nullable: false),
                    MessageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationSummaries_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationEntries_SessionId_Timestamp",
                table: "ConversationEntries",
                columns: new[] { "SessionId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSummaries_SessionId",
                table: "ConversationSummaries",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSummaries_SessionId_SummarySequence",
                table: "ConversationSummaries",
                columns: new[] { "SessionId", "SummarySequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_BearerTokenHash",
                table: "Sessions",
                column: "BearerTokenHash",
                filter: "BearerTokenHash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_LastAccessedAt",
                table: "Sessions",
                column: "LastAccessedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationEntries");

            migrationBuilder.DropTable(
                name: "ConversationSummaries");

            migrationBuilder.DropTable(
                name: "Sessions");
        }
    }
}
