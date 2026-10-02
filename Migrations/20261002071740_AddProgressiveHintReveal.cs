using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelligentProgrammingPlatform.Migrations
{
    /// <inheritdoc />
    public partial class AddProgressiveHintReveal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RevealedHintCount",
                table: "AiFeedbacks",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("""
                UPDATE feedback SET RevealedHintCount = 0
                FROM AiFeedbacks AS feedback
                WHERE DATALENGTH(feedback.HintsJson) > 12000
                   OR LEFT(LTRIM(REPLACE(REPLACE(REPLACE(feedback.HintsJson, CHAR(9), ' '), CHAR(10), ' '), CHAR(13), ' ')), 1) <> '['
                   OR NOT EXISTS (SELECT 1 FROM OPENJSON(CASE WHEN ISJSON(feedback.HintsJson) = 1 THEN feedback.HintsJson ELSE N'[]' END))
                   OR EXISTS (
                       SELECT 1 FROM OPENJSON(CASE WHEN ISJSON(feedback.HintsJson) = 1 THEN feedback.HintsJson ELSE N'[]' END)
                       WHERE [type] <> 1 OR DATALENGTH([value]) > 600
                          OR NULLIF(LTRIM(RTRIM(REPLACE(REPLACE(REPLACE([value], CHAR(9), ' '), CHAR(10), ' '), CHAR(13), ' '))), N'') IS NULL);
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiFeedbacks_RevealedHintCount",
                table: "AiFeedbacks",
                sql: "[RevealedHintCount] BETWEEN 0 AND 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AiFeedbacks_RevealedHintCount",
                table: "AiFeedbacks");

            migrationBuilder.DropColumn(
                name: "RevealedHintCount",
                table: "AiFeedbacks");
        }
    }
}
