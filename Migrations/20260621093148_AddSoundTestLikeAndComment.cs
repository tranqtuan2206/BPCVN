using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BPCVN.Migrations
{
    /// <inheritdoc />
    public partial class AddSoundTestLikeAndComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SoundTestComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Content = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SoundTestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentCommentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoundTestComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoundTestComments_SoundTestComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "SoundTestComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SoundTestComments_SoundTests_SoundTestId",
                        column: x => x.SoundTestId,
                        principalTable: "SoundTests",
                        principalColumn: "TestId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SoundTestComments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SoundTestLikes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SoundTestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoundTestLikes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoundTestLikes_SoundTests_SoundTestId",
                        column: x => x.SoundTestId,
                        principalTable: "SoundTests",
                        principalColumn: "TestId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SoundTestLikes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SoundTestComments_ParentCommentId",
                table: "SoundTestComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_SoundTestComments_SoundTestId",
                table: "SoundTestComments",
                column: "SoundTestId");

            migrationBuilder.CreateIndex(
                name: "IX_SoundTestComments_UserId",
                table: "SoundTestComments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SoundTestLikes_SoundTestId",
                table: "SoundTestLikes",
                column: "SoundTestId");

            migrationBuilder.CreateIndex(
                name: "IX_SoundTestLikes_UserId_SoundTestId",
                table: "SoundTestLikes",
                columns: new[] { "UserId", "SoundTestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SoundTestComments");

            migrationBuilder.DropTable(
                name: "SoundTestLikes");
        }
    }
}
