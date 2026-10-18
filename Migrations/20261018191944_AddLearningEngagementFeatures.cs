using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelligentProgrammingPlatform.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningEngagementFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcceptedCodeReviews",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubmissionId = table.Column<long>(type: "bigint", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    ReadabilityFeedback = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false),
                    NamingFeedback = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false),
                    EfficiencyFeedback = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false),
                    StyleFeedback = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false),
                    TimeComplexity = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SpaceComplexity = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ComplexityExplanation = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: false),
                    SuggestionsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 6000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InputTokens = table.Column<int>(type: "int", nullable: true),
                    OutputTokens = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcceptedCodeReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcceptedCodeReviews_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LessonProgresses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LessonId = table.Column<int>(type: "int", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonProgresses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LessonProgresses_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TaskFavorites",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ProgrammingTaskId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskFavorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskFavorites_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskFavorites_ProgrammingTasks_ProgrammingTaskId",
                        column: x => x.ProgrammingTaskId,
                        principalTable: "ProgrammingTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_UserId_CreatedAt",
                table: "Submissions",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AcceptedCodeReviews_SubmissionId",
                table: "AcceptedCodeReviews",
                column: "SubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LessonProgresses_LessonId",
                table: "LessonProgresses",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonProgresses_UserId_LessonId",
                table: "LessonProgresses",
                columns: new[] { "UserId", "LessonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskFavorites_ProgrammingTaskId",
                table: "TaskFavorites",
                column: "ProgrammingTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskFavorites_UserId_ProgrammingTaskId",
                table: "TaskFavorites",
                columns: new[] { "UserId", "ProgrammingTaskId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcceptedCodeReviews");

            migrationBuilder.DropTable(
                name: "LessonProgresses");

            migrationBuilder.DropTable(
                name: "TaskFavorites");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_UserId_CreatedAt",
                table: "Submissions");
        }
    }
}
