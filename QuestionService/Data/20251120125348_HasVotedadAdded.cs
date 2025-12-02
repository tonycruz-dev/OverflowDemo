using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestionService.Data
{
    /// <inheritdoc />
    public partial class HasVotedadAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasVoted",
                table: "AiAnswers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasVoted",
                table: "AiAnswers");
        }
    }
}
