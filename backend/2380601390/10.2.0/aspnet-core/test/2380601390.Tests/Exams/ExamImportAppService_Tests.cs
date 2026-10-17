using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using _2380601390.Exams;
using _2380601390.Exams.Teacher;
using _2380601390.Exams.Teacher.Dto;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace _2380601390.Tests.Exams;

/// <summary>Kiểm thử AppService import qua DI/UoW của ABP (CSDL InMemory: kiểm đồ thị lưu đúng, không kiểm ràng buộc SQL).</summary>
public class ExamImportAppService_Tests : _2380601390TestBase
{
    private readonly IExamImportAppService _import;
    private readonly IExamAppService _exams;

    public ExamImportAppService_Tests()
    {
        _import = Resolve<IExamImportAppService>();
        _exams = Resolve<IExamAppService>();
    }

    private static ImportExamInput Input(System.IO.MemoryStream docx, string name = "de-demo.docx") =>
        new ImportExamInput { FileName = name, Content = docx.ToArray() };

    [Fact]
    public async Task Valid_file_is_saved_as_draft_with_full_structure()
    {
        var result = await _import.ImportAsync(Input(ExamImportTests.BuildFull100()));

        result.Errors.Select(e => e.Message).ShouldBeEmpty();
        result.Success.ShouldBeTrue();
        result.VersionNo.ShouldBe(1);
        result.QuestionCount.ShouldBe(100);
        result.ImageCount.ShouldBe(25);

        await UsingDbContextAsync(async db =>
        {
            (await db.Exams.CountAsync()).ShouldBe(1);
            var v = await db.ExamVersions.SingleAsync();
            v.Status.ShouldBe(ExamVersionStatus.Draft);
            v.DurationMinutes.ShouldBe(75);
            v.TotalQuestions.ShouldBe(100);
            v.SourceFileName.ShouldBe("de-demo.docx");
            v.SourceFileSha256.Length.ShouldBe(64);

            (await db.ExamParts.CountAsync()).ShouldBe(3);
            (await db.Questions.CountAsync()).ShouldBe(100);
            (await db.QuestionOptions.CountAsync()).ShouldBe(400);
            (await db.ReadingGroups.CountAsync()).ShouldBe(22);
            (await db.ReadingDocuments.CountAsync()).ShouldBe(28);
            (await db.MediaAssets.CountAsync()).ShouldBe(25);
            (await db.ContentBlocks.CountAsync(b => b.BlockType == ContentBlockType.Image)).ShouldBe(25);

            // Mọi câu có đáp án đúng thuộc chính câu đó.
            var questions = await db.Questions.Include(q => q.Options).ToListAsync();
            questions.All(q => q.CorrectOptionId.HasValue && q.Options.Any(o => o.Id == q.CorrectOptionId)).ShouldBeTrue();
            questions.Where(q => q.SourceNo >= 101 && q.SourceNo <= 130).All(q => q.ReadingGroupId == null).ShouldBeTrue();
            questions.Where(q => q.SourceNo >= 131).All(q => q.ReadingGroupId != null).ShouldBeTrue();
            questions.Where(q => q.SourceNo >= 131 && q.SourceNo <= 146).All(q => q.StemText == null).ShouldBeTrue();
        });
    }

    [Fact]
    public async Task Invalid_file_saves_nothing_and_returns_located_errors()
    {
        var result = await _import.ImportAsync(Input(ExamImportTests.BuildFull100(dropLastAnswer: true)));

        result.Success.ShouldBeFalse();
        result.ExamVersionId.ShouldBeNull();
        var error = result.Errors.Single();
        error.Code.ShouldBe("E_ANSWER_MISSING");
        error.QuestionNo.ShouldBe(200);
        error.PartNo.ShouldBe(7);

        await UsingDbContextAsync(async db =>
        {
            (await db.Exams.CountAsync()).ShouldBe(0);
            (await db.ExamVersions.CountAsync()).ShouldBe(0);
            (await db.Questions.CountAsync()).ShouldBe(0);
            (await db.MediaAssets.CountAsync()).ShouldBe(0);
        });
    }

    [Fact]
    public async Task Importing_same_exam_code_creates_next_version_and_reuses_images()
    {
        var first = await _import.ImportAsync(Input(ExamImportTests.BuildFull100()));
        var second = await _import.ImportAsync(Input(ExamImportTests.BuildFull100()));

        second.Success.ShouldBeTrue();
        second.ExamId.ShouldBe(first.ExamId);
        second.VersionNo.ShouldBe(2);

        await UsingDbContextAsync(async db =>
        {
            (await db.Exams.CountAsync()).ShouldBe(1);
            (await db.ExamVersions.CountAsync()).ShouldBe(2);
            (await db.Questions.CountAsync()).ShouldBe(200);
            (await db.MediaAssets.CountAsync()).ShouldBe(25); // ảnh trùng SHA-256 không nhân đôi
        });

        var list = await _exams.GetVersionsAsync();
        list.Select(x => x.VersionNo).ShouldBe(new[] { 2, 1 }, ignoreOrder: true);
        list.All(x => x.Code == "DEMO-01" && x.Status == ExamVersionStatus.Draft).ShouldBeTrue();
    }

    [Fact]
    public async Task Non_docx_extension_and_garbage_content_are_rejected_without_saving()
    {
        var wrongExt = await _import.ImportAsync(new ImportExamInput { FileName = "de.pdf", Content = new byte[] { 1, 2, 3 } });
        wrongExt.Success.ShouldBeFalse();
        wrongExt.Errors.Single().Code.ShouldBe("E_FILE_INVALID");

        var garbage = await _import.ImportAsync(new ImportExamInput { FileName = "de.docx", Content = new byte[] { 1, 2, 3, 4, 5 } });
        garbage.Success.ShouldBeFalse();
        garbage.Errors.Single().Code.ShouldBe("E_FILE_INVALID");

        await UsingDbContextAsync(async db => (await db.Exams.CountAsync()).ShouldBe(0));
    }

    [Fact]
    public async Task Anonymous_user_cannot_import()
    {
        AbpSession.UserId = null;
        await Should.ThrowAsync<AbpAuthorizationException>(() => _import.ImportAsync(Input(ExamImportTests.BuildFull100())));
    }
}
