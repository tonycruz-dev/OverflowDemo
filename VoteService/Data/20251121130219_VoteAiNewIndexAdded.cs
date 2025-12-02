using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoteService.Data
{
    /// <inheritdoc />
    public partial class VoteAiNewIndexAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_VoteAIs_UserId_TargetType_TargetId_AiId",
                table: "VoteAIs",
                columns: new[] { "UserId", "TargetType", "TargetId", "AiId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VoteAIs_UserId_TargetType_TargetId_AiId",
                table: "VoteAIs");
        }
    }
}
