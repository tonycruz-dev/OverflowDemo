using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestionService.Data
{
    /// <inheritdoc />
    public partial class RoleToAiModelAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "AIModels",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: "a1f1e941-3d7e-4f07-ab43-21ba1f70a001",
                column: "Role",
                value: "");

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: "a1f1e941-3d7e-4f07-ab43-21ba1f70a002",
                column: "Role",
                value: "");

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: "a1f1e941-3d7e-4f07-ab43-21ba1f70a003",
                column: "Role",
                value: "");

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: "a1f1e941-3d7e-4f07-ab43-21ba1f70a004",
                column: "Role",
                value: "");

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: "a1f1e941-3d7e-4f07-ab43-21ba1f70a005",
                column: "Role",
                value: "");

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: "a1f1e941-3d7e-4f07-ab43-21ba1f70a006",
                column: "Role",
                value: "");

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: "a1f1e941-3d7e-4f07-ab43-21ba1f70a007",
                column: "Role",
                value: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "AIModels");
        }
    }
}
