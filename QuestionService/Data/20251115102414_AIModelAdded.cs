using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace QuestionService.Data
{
    /// <inheritdoc />
    public partial class AIModelAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIModels",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIModels", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "AIModels",
                columns: new[] { "Id", "CreatedAt", "Description", "Name", "UpdatedAt", "Version" },
                values: new object[,]
                {
                    { "a1f1e941-3d7e-4f07-ab43-21ba1f70a001", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "OpenAI gpt-5-chat model.", "gpt-5-chat", null, "5.0" },
                    { "a1f1e941-3d7e-4f07-ab43-21ba1f70a002", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Google Gemini 2.5 Flash model.", "gemini-2.5-flash", null, "2.5" },
                    { "a1f1e941-3d7e-4f07-ab43-21ba1f70a003", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "OpenAI GPT-OSS-120B open source model.", "openai/gpt-oss-120b", null, "1.0" },
                    { "a1f1e941-3d7e-4f07-ab43-21ba1f70a004", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Meta Llama 4 Maverick 17B Instruct model.", "meta-llama/llama-4-maverick-17b-128e-instruct", null, "4.0" },
                    { "a1f1e941-3d7e-4f07-ab43-21ba1f70a005", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Alibaba Qwen 3 32B model.", "qwen/qwen3-32b", null, "3.0" },
                    { "a1f1e941-3d7e-4f07-ab43-21ba1f70a006", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "OpenAI GPT-4.1 model.", "gpt-4.1", null, "4.1" },
                    { "a1f1e941-3d7e-4f07-ab43-21ba1f70a007", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "DeepSeek V3 (March 2024) model.", "deepseek-v3-0324", null, "3.0" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIModels");
        }
    }
}
