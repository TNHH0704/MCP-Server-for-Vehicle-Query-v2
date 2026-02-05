using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace McpVersionVer2.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Sessions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Sessions");
        }
    }
}
