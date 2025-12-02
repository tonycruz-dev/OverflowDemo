using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoteService.Data
{
    /// <inheritdoc />
    public partial class VoteAiIndexChangedAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VoteAIs_AiId_TargetType_TargetId",
                table: "VoteAIs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_VoteAIs_AiId_TargetType_TargetId",
                table: "VoteAIs",
                columns: new[] { "AiId", "TargetType", "TargetId" },
                unique: true);
        }
    }
}
