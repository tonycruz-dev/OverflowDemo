using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestionService.Data
{
    /// <inheritdoc />
    public partial class QuestionAiAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Accepted",
                table: "AiAnswers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Accepted",
                table: "AiAnswers");
        }
    }
}
