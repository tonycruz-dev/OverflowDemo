using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestionService.Data
{
    /// <inheritdoc />
    public partial class DiagnosisAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "AiAnswers",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(5000)",
                oldMaxLength: 5000);

            migrationBuilder.AddColumn<string>(
                name: "Alternatives",
                table: "AiAnswers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodePatch",
                table: "AiAnswers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Diagnosis",
                table: "AiAnswers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FixStepByStep",
                table: "AiAnswers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gotchas",
                table: "AiAnswers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LikelyRootCause",
                table: "AiAnswers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Alternatives",
                table: "AiAnswers");

            migrationBuilder.DropColumn(
                name: "CodePatch",
                table: "AiAnswers");

            migrationBuilder.DropColumn(
                name: "Diagnosis",
                table: "AiAnswers");

            migrationBuilder.DropColumn(
                name: "FixStepByStep",
                table: "AiAnswers");

            migrationBuilder.DropColumn(
                name: "Gotchas",
                table: "AiAnswers");

            migrationBuilder.DropColumn(
                name: "LikelyRootCause",
                table: "AiAnswers");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "AiAnswers",
                type: "character varying(5000)",
                maxLength: 5000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
