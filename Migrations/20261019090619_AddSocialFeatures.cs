using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelligentProgrammingPlatform.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DirectConversations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserAId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UserBId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectConversations", x => x.Id);
                    table.CheckConstraint("CK_DirectConversations_Pair", "[UserAId] COLLATE Latin1_General_100_BIN2 < [UserBId] COLLATE Latin1_General_100_BIN2");
                    table.ForeignKey(
                        name: "FK_DirectConversations_AspNetUsers_UserAId",
                        column: x => x.UserAId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DirectConversations_AspNetUsers_UserBId",
                        column: x => x.UserBId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Friendships",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserAId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UserBId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Friendships", x => x.Id);
                    table.CheckConstraint("CK_Friendships_Pair", "[UserAId] COLLATE Latin1_General_100_BIN2 < [UserBId] COLLATE Latin1_General_100_BIN2");
                    table.CheckConstraint("CK_Friendships_Requester", "[RequestedByUserId] IN ([UserAId], [UserBId])");
                    table.CheckConstraint("CK_Friendships_Status", "([Status] = 0 AND [AcceptedAt] IS NULL) OR ([Status] = 1 AND [AcceptedAt] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Friendships_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Friendships_AspNetUsers_UserAId",
                        column: x => x.UserAId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Friendships_AspNetUsers_UserBId",
                        column: x => x.UserBId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserBlocks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BlockerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    BlockedUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBlocks", x => x.Id);
                    table.CheckConstraint("CK_UserBlocks_DifferentUsers", "[BlockerUserId] <> [BlockedUserId]");
                    table.ForeignKey(
                        name: "FK_UserBlocks_AspNetUsers_BlockedUserId",
                        column: x => x.BlockedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserBlocks_AspNetUsers_BlockerUserId",
                        column: x => x.BlockerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DirectMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    SenderUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ClientMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectMessages", x => x.Id);
                    table.CheckConstraint("CK_DirectMessages_Body", "LEN([Body]) BETWEEN 1 AND 2000");
                    table.ForeignKey(
                        name: "FK_DirectMessages_AspNetUsers_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DirectMessages_DirectConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "DirectConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectConversations_UserAId_LastMessageAt",
                table: "DirectConversations",
                columns: new[] { "UserAId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectConversations_UserAId_UserBId",
                table: "DirectConversations",
                columns: new[] { "UserAId", "UserBId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectConversations_UserBId_LastMessageAt",
                table: "DirectConversations",
                columns: new[] { "UserBId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_ConversationId_CreatedAt",
                table: "DirectMessages",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_ConversationId_Id",
                table: "DirectMessages",
                columns: new[] { "ConversationId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_ConversationId_ReadAt_SenderUserId",
                table: "DirectMessages",
                columns: new[] { "ConversationId", "ReadAt", "SenderUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_SenderUserId_ClientMessageId",
                table: "DirectMessages",
                columns: new[] { "SenderUserId", "ClientMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_RequestedByUserId",
                table: "Friendships",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_UserAId_UserBId",
                table: "Friendships",
                columns: new[] { "UserAId", "UserBId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_UserBId_Status",
                table: "Friendships",
                columns: new[] { "UserBId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UserBlocks_BlockedUserId",
                table: "UserBlocks",
                column: "BlockedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBlocks_BlockerUserId_BlockedUserId",
                table: "UserBlocks",
                columns: new[] { "BlockerUserId", "BlockedUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DirectMessages");

            migrationBuilder.DropTable(
                name: "Friendships");

            migrationBuilder.DropTable(
                name: "UserBlocks");

            migrationBuilder.DropTable(
                name: "DirectConversations");
        }
    }
}
