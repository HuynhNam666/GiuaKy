using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _2380601390.Migrations
{
    /// <inheritdoc />
    public partial class Added_Exam_Domain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Exams",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierUserId = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeleterUserId = table.Column<long>(type: "bigint", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaAssets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExamVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExamId = table.Column<long>(type: "bigint", nullable: false),
                    VersionNo = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    TotalQuestions = table.Column<int>(type: "int", nullable: false),
                    TagsVersion = table.Column<int>(type: "int", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    SourceFileSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierUserId = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeleterUserId = table.Column<long>(type: "bigint", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamVersions_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExamAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ExamVersionId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalQuestions = table.Column<int>(type: "int", nullable: false),
                    CorrectCount = table.Column<int>(type: "int", nullable: true),
                    WrongCount = table.Column<int>(type: "int", nullable: true),
                    SkippedCount = table.Column<int>(type: "int", nullable: true),
                    ScorePercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamAttempts_ExamVersions_ExamVersionId",
                        column: x => x.ExamVersionId,
                        principalTable: "ExamVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamParts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExamVersionId = table.Column<long>(type: "bigint", nullable: false),
                    PartNo = table.Column<int>(type: "int", nullable: false),
                    FromNo = table.Column<int>(type: "int", nullable: false),
                    ToNo = table.Column<int>(type: "int", nullable: false),
                    ShuffleItems = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamParts_ExamVersions_ExamVersionId",
                        column: x => x.ExamVersionId,
                        principalTable: "ExamVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReadingGroups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExamPartId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    ShuffleQuestions = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingGroups_ExamParts_ExamPartId",
                        column: x => x.ExamPartId,
                        principalTable: "ExamParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttemptGroups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExamAttemptId = table.Column<long>(type: "bigint", nullable: false),
                    ReadingGroupId = table.Column<long>(type: "bigint", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttemptGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttemptGroups_ExamAttempts_ExamAttemptId",
                        column: x => x.ExamAttemptId,
                        principalTable: "ExamAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttemptGroups_ReadingGroups_ReadingGroupId",
                        column: x => x.ReadingGroupId,
                        principalTable: "ReadingGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReadingDocuments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReadingGroupId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingDocuments_ReadingGroups_ReadingGroupId",
                        column: x => x.ReadingGroupId,
                        principalTable: "ReadingGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentBlocks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReadingDocumentId = table.Column<long>(type: "bigint", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    BlockType = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MediaAssetId = table.Column<long>(type: "bigint", nullable: true),
                    AltText = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentBlocks", x => x.Id);
                    table.CheckConstraint("CK_ContentBlock_TextOrImage", "([BlockType] = 0 AND [Text] IS NOT NULL AND [MediaAssetId] IS NULL) OR ([BlockType] = 1 AND [MediaAssetId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ContentBlocks_MediaAssets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentBlocks_ReadingDocuments_ReadingDocumentId",
                        column: x => x.ReadingDocumentId,
                        principalTable: "ReadingDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttemptOptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttemptQuestionId = table.Column<long>(type: "bigint", nullable: false),
                    QuestionOptionId = table.Column<long>(type: "bigint", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttemptOptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttemptQuestions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExamAttemptId = table.Column<long>(type: "bigint", nullable: false),
                    QuestionId = table.Column<long>(type: "bigint", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    SelectedOptionId = table.Column<long>(type: "bigint", nullable: true),
                    AnswerRevision = table.Column<int>(type: "int", nullable: false),
                    AnsweredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttemptQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttemptQuestions_ExamAttempts_ExamAttemptId",
                        column: x => x.ExamAttemptId,
                        principalTable: "ExamAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuestionOptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuestionId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionOptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Questions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExamVersionId = table.Column<long>(type: "bigint", nullable: false),
                    ExamPartId = table.Column<long>(type: "bigint", nullable: false),
                    ReadingGroupId = table.Column<long>(type: "bigint", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceNo = table.Column<int>(type: "int", nullable: false),
                    SourceOrder = table.Column<int>(type: "int", nullable: false),
                    StemText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Explanation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShuffleOptions = table.Column<bool>(type: "bit", nullable: false),
                    CorrectOptionId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Questions_ExamParts_ExamPartId",
                        column: x => x.ExamPartId,
                        principalTable: "ExamParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Questions_ExamVersions_ExamVersionId",
                        column: x => x.ExamVersionId,
                        principalTable: "ExamVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Questions_QuestionOptions_CorrectOptionId",
                        column: x => x.CorrectOptionId,
                        principalTable: "QuestionOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Questions_ReadingGroups_ReadingGroupId",
                        column: x => x.ReadingGroupId,
                        principalTable: "ReadingGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttemptGroups_ExamAttemptId_DisplayOrder",
                table: "AttemptGroups",
                columns: new[] { "ExamAttemptId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttemptGroups_ExamAttemptId_ReadingGroupId",
                table: "AttemptGroups",
                columns: new[] { "ExamAttemptId", "ReadingGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttemptGroups_ReadingGroupId",
                table: "AttemptGroups",
                column: "ReadingGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_AttemptOptions_AttemptQuestionId_DisplayOrder",
                table: "AttemptOptions",
                columns: new[] { "AttemptQuestionId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttemptOptions_AttemptQuestionId_QuestionOptionId",
                table: "AttemptOptions",
                columns: new[] { "AttemptQuestionId", "QuestionOptionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttemptOptions_QuestionOptionId",
                table: "AttemptOptions",
                column: "QuestionOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_AttemptQuestions_ExamAttemptId_DisplayOrder",
                table: "AttemptQuestions",
                columns: new[] { "ExamAttemptId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttemptQuestions_ExamAttemptId_QuestionId",
                table: "AttemptQuestions",
                columns: new[] { "ExamAttemptId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttemptQuestions_QuestionId_IsCorrect",
                table: "AttemptQuestions",
                columns: new[] { "QuestionId", "IsCorrect" });

            migrationBuilder.CreateIndex(
                name: "IX_AttemptQuestions_SelectedOptionId",
                table: "AttemptQuestions",
                column: "SelectedOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentBlocks_MediaAssetId",
                table: "ContentBlocks",
                column: "MediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentBlocks_ReadingDocumentId_DisplayOrder",
                table: "ContentBlocks",
                columns: new[] { "ReadingDocumentId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttempts_ExamVersionId_Status",
                table: "ExamAttempts",
                columns: new[] { "ExamVersionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttempts_Status_ExpiresAtUtc",
                table: "ExamAttempts",
                columns: new[] { "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_ExamAttempt_OneInProgress",
                table: "ExamAttempts",
                columns: new[] { "UserId", "ExamVersionId" },
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ExamParts_ExamVersionId_PartNo",
                table: "ExamParts",
                columns: new[] { "ExamVersionId", "PartNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Exams_Code",
                table: "Exams",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamVersions_ExamId_VersionNo",
                table: "ExamVersions",
                columns: new[] { "ExamId", "VersionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamVersions_Status",
                table: "ExamVersions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_Sha256",
                table: "MediaAssets",
                column: "Sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionOptions_QuestionId_Code",
                table: "QuestionOptions",
                columns: new[] { "QuestionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Questions_CorrectOptionId",
                table: "Questions",
                column: "CorrectOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_ExamPartId",
                table: "Questions",
                column: "ExamPartId");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_ExamVersionId_Code",
                table: "Questions",
                columns: new[] { "ExamVersionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Questions_ExamVersionId_SourceNo",
                table: "Questions",
                columns: new[] { "ExamVersionId", "SourceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Questions_ReadingGroupId",
                table: "Questions",
                column: "ReadingGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingDocuments_ReadingGroupId_DisplayOrder",
                table: "ReadingDocuments",
                columns: new[] { "ReadingGroupId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReadingGroups_ExamPartId_Code",
                table: "ReadingGroups",
                columns: new[] { "ExamPartId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AttemptOptions_AttemptQuestions_AttemptQuestionId",
                table: "AttemptOptions",
                column: "AttemptQuestionId",
                principalTable: "AttemptQuestions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AttemptOptions_QuestionOptions_QuestionOptionId",
                table: "AttemptOptions",
                column: "QuestionOptionId",
                principalTable: "QuestionOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AttemptQuestions_QuestionOptions_SelectedOptionId",
                table: "AttemptQuestions",
                column: "SelectedOptionId",
                principalTable: "QuestionOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AttemptQuestions_Questions_QuestionId",
                table: "AttemptQuestions",
                column: "QuestionId",
                principalTable: "Questions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionOptions_Questions_QuestionId",
                table: "QuestionOptions",
                column: "QuestionId",
                principalTable: "Questions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Questions_ReadingGroups_ReadingGroupId",
                table: "Questions");

            migrationBuilder.DropForeignKey(
                name: "FK_Questions_QuestionOptions_CorrectOptionId",
                table: "Questions");

            migrationBuilder.DropTable(
                name: "AttemptGroups");

            migrationBuilder.DropTable(
                name: "AttemptOptions");

            migrationBuilder.DropTable(
                name: "ContentBlocks");

            migrationBuilder.DropTable(
                name: "AttemptQuestions");

            migrationBuilder.DropTable(
                name: "MediaAssets");

            migrationBuilder.DropTable(
                name: "ReadingDocuments");

            migrationBuilder.DropTable(
                name: "ExamAttempts");

            migrationBuilder.DropTable(
                name: "ReadingGroups");

            migrationBuilder.DropTable(
                name: "QuestionOptions");

            migrationBuilder.DropTable(
                name: "Questions");

            migrationBuilder.DropTable(
                name: "ExamParts");

            migrationBuilder.DropTable(
                name: "ExamVersions");

            migrationBuilder.DropTable(
                name: "Exams");
        }
    }
}
