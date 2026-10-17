using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.UI;
using _2380601390.Exams;
using _2380601390.Exams.Teacher;
using _2380601390.Exams.Teacher.Dto;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace _2380601390.Tests.Exams;

/// <summary>Xem trước, sửa, xuất bản, bản sửa (qua DI/UoW của ABP, CSDL InMemory).</summary>
public class ExamEditPublishAppService_Tests : _2380601390TestBase
{
    private readonly IExamImportAppService _import;
    private readonly IExamAppService _exams;
    private readonly IExamEditAppService _edit;
    private readonly IExamPublishAppService _publish;

    public ExamEditPublishAppService_Tests()
    {
        _import = Resolve<IExamImportAppService>();
        _exams = Resolve<IExamAppService>();
        _edit = Resolve<IExamEditAppService>();
        _publish = Resolve<IExamPublishAppService>();
    }

    private async Task<long> ImportAsync(int imageSeedBase = 0)
    {
        var r = await _import.ImportAsync(new ImportExamInput
        {
            FileName = "de.docx",
            Content = ExamImportTests.BuildFull100(imageSeedBase: imageSeedBase).ToArray()
        });
        r.Errors.Select(e => e.Message).ShouldBeEmpty();
        return r.ExamVersionId.Value;
    }

    private static VersionIdInput V(long id) => new VersionIdInput { VersionId = id };

    private static UpdateQuestionInput QInput(QuestionPreviewDto q, string answer = null, string stem = null, string optionB = null) => new UpdateQuestionInput
    {
        QuestionId = q.Id,
        Stem = stem ?? q.Stem,
        Explanation = q.Explanation,
        ShuffleOptions = q.ShuffleOptions,
        CorrectOptionCode = answer ?? q.CorrectOptionCode,
        Options = q.Options.Select(o => new OptionTextInput { Code = o.Code, Text = o.Code == "B" && optionB != null ? optionB : o.Text }).ToList()
    };

    [Fact]
    public async Task Preview_returns_full_structure_with_answers_and_no_issues()
    {
        var id = await ImportAsync();
        var p = await _exams.GetPreviewAsync(id);

        p.Issues.ShouldBeEmpty();
        p.CanEdit.ShouldBeTrue();
        p.Status.ShouldBe(ExamVersionStatus.Draft);
        p.Title.ShouldBe("Demo Reading Test (minh hoa)");
        p.Parts.Select(x => x.PartNo).ShouldBe(new[] { 5, 6, 7 });
        p.Parts[0].Questions.Count.ShouldBe(30);
        p.Parts[1].Groups.Count.ShouldBe(4);
        p.Parts[2].Groups.Count.ShouldBe(18);
        p.Parts.SelectMany(x => x.Questions.Concat(x.Groups.SelectMany(g => g.Questions))).Count().ShouldBe(100);
        p.Parts[0].Questions[0].CorrectOptionCode.ShouldBe("B");
        p.Parts[0].Questions[0].Options.Select(o => o.Code).ShouldBe(new[] { "A", "B", "C", "D" });

        var doc = p.Parts[2].Groups[0].Documents[0];
        doc.Blocks.Select(b => b.Type).ShouldBe(new[] { ContentBlockType.Text, ContentBlockType.Image, ContentBlockType.Text });
        doc.Blocks[1].MediaAssetId.ShouldNotBeNull();
        p.Parts[1].Groups[0].Questions.Select(q => q.SourceNo).ShouldBe(new[] { 131, 132, 133, 134 });
    }

    [Fact]
    public async Task Update_question_is_saved_and_revalidated()
    {
        var id = await ImportAsync();
        var q = (await _exams.GetPreviewAsync(id)).Parts[0].Questions[0];

        var result = await _edit.UpdateQuestionAsync(QInput(q, answer: "C", stem: "Đã sửa nội dung câu hỏi", optionB: "beta (sửa)"));
        result.IsValid.ShouldBeTrue();

        var after = (await _exams.GetPreviewAsync(id)).Parts[0].Questions[0];
        after.Stem.ShouldBe("Đã sửa nội dung câu hỏi");
        after.CorrectOptionCode.ShouldBe("C");
        after.Options.Single(o => o.Code == "B").Text.ShouldBe("beta (sửa)");
        after.Options.Select(o => o.Id).ShouldBe(q.Options.Select(o => o.Id)); // vẫn là các lựa chọn cũ (giữ ID ổn định)
    }

    [Fact]
    public async Task Invalid_question_edits_are_rejected_and_nothing_changes()
    {
        var id = await ImportAsync();
        var q = (await _exams.GetPreviewAsync(id)).Parts[0].Questions[0];

        await Should.ThrowAsync<UserFriendlyException>(() => _edit.UpdateQuestionAsync(QInput(q, answer: "E")));
        await Should.ThrowAsync<UserFriendlyException>(() => _edit.UpdateQuestionAsync(QInput(q, optionB: "  ")));
        await Should.ThrowAsync<UserFriendlyException>(() => _edit.UpdateQuestionAsync(QInput(q, stem: " ")));
        var three = QInput(q);
        three.Options.RemoveAt(3);
        await Should.ThrowAsync<UserFriendlyException>(() => _edit.UpdateQuestionAsync(three));
        var noExpl = QInput(q);
        noExpl.Explanation = "";
        await Should.ThrowAsync<UserFriendlyException>(() => _edit.UpdateQuestionAsync(noExpl));

        var after = (await _exams.GetPreviewAsync(id)).Parts[0].Questions[0];
        after.Stem.ShouldBe(q.Stem);
        after.CorrectOptionCode.ShouldBe("B");
    }

    [Fact]
    public async Task Part6_question_may_have_empty_stem()
    {
        var id = await ImportAsync();
        var q = (await _exams.GetPreviewAsync(id)).Parts[1].Groups[0].Questions[0];
        var result = await _edit.UpdateQuestionAsync(QInput(q, stem: ""));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Shuffle_settings_follow_part6_rules()
    {
        var id = await ImportAsync();
        var p = await _exams.GetPreviewAsync(id);

        await Should.ThrowAsync<UserFriendlyException>(() =>
            _edit.UpdateGroupAsync(new UpdateGroupInput { GroupId = p.Parts[1].Groups[0].Id, ShuffleQuestions = true }));

        (await _edit.UpdateGroupAsync(new UpdateGroupInput { GroupId = p.Parts[2].Groups[0].Id, ShuffleQuestions = true })).IsValid.ShouldBeTrue();
        (await _edit.UpdatePartAsync(new UpdatePartInput { PartId = p.Parts[0].Id, ShuffleItems = false })).IsValid.ShouldBeTrue();

        var after = await _exams.GetPreviewAsync(id);
        after.Parts[2].Groups[0].ShuffleQuestions.ShouldBeTrue();
        after.Parts[0].ShuffleItems.ShouldBeFalse();
    }

    [Fact]
    public async Task Settings_title_and_duration_are_validated()
    {
        var id = await ImportAsync();
        await _edit.UpdateSettingsAsync(new UpdateSettingsInput { VersionId = id, Title = "Tên mới", DurationMinutes = 60 });
        var p = await _exams.GetPreviewAsync(id);
        p.Title.ShouldBe("Tên mới");
        p.DurationMinutes.ShouldBe(60);

        await Should.ThrowAsync<UserFriendlyException>(() =>
            _edit.UpdateSettingsAsync(new UpdateSettingsInput { VersionId = id, Title = "x", DurationMinutes = 0 }));
        await Should.ThrowAsync<UserFriendlyException>(() =>
            _edit.UpdateSettingsAsync(new UpdateSettingsInput { VersionId = id, Title = "x", DurationMinutes = 1000 }));
    }

    [Fact]
    public async Task Breaking_part6_blank_is_reported_and_blocks_publish()
    {
        var id = await ImportAsync();
        var block = (await _exams.GetPreviewAsync(id)).Parts[1].Groups[0].Documents[0].Blocks[0];
        block.Text.ShouldContain("{{blank:P6-131}}");

        var result = await _edit.UpdateBlockTextAsync(new UpdateBlockTextInput { BlockId = block.Id, Text = "Đoạn văn đã xóa hết chỗ trống." });
        result.IsValid.ShouldBeFalse();
        result.Issues.Select(i => i.Code).ShouldContain("E_BLANK_MISSING");

        var publish = await _publish.PublishAsync(V(id));
        publish.Success.ShouldBeFalse();
        publish.Errors.Select(e => e.Code).ShouldContain("E_BLANK_MISSING");
        (await _exams.GetPreviewAsync(id)).Status.ShouldBe(ExamVersionStatus.Draft);

        // Sửa lại đúng rồi xuất bản được.
        await _edit.UpdateBlockTextAsync(new UpdateBlockTextInput { BlockId = block.Id, Text = block.Text });
        (await _publish.PublishAsync(V(id))).Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Image_blocks_cannot_be_edited_as_text()
    {
        var id = await ImportAsync();
        var img = (await _exams.GetPreviewAsync(id)).Parts[2].Groups[0].Documents[0].Blocks[1];
        await Should.ThrowAsync<UserFriendlyException>(() => _edit.UpdateBlockTextAsync(new UpdateBlockTextInput { BlockId = img.Id, Text = "x" }));
    }

    [Fact]
    public async Task Publish_makes_version_published_and_it_becomes_immutable()
    {
        var id = await ImportAsync();
        var q = (await _exams.GetPreviewAsync(id)).Parts[0].Questions[0];

        var result = await _publish.PublishAsync(V(id));
        result.Success.ShouldBeTrue();

        var p = await _exams.GetPreviewAsync(id);
        p.Status.ShouldBe(ExamVersionStatus.Published);
        p.CanEdit.ShouldBeFalse();

        await Should.ThrowAsync<UserFriendlyException>(() => _edit.UpdateQuestionAsync(QInput(q, stem: "cố sửa bản đã xuất bản")));
        await Should.ThrowAsync<UserFriendlyException>(() => _edit.UpdateSettingsAsync(new UpdateSettingsInput { VersionId = id, Title = "x", DurationMinutes = 10 }));
        await Should.ThrowAsync<UserFriendlyException>(() => _publish.PublishAsync(V(id))); // đã xuất bản rồi
        await Should.ThrowAsync<UserFriendlyException>(() => _publish.DeleteDraftAsync(V(id)));

        (await _exams.GetPreviewAsync(id)).Parts[0].Questions[0].Stem.ShouldBe(q.Stem);
    }

    [Fact]
    public async Task Publishing_a_new_version_archives_the_previous_published_one()
    {
        var v1 = await ImportAsync();
        (await _publish.PublishAsync(V(v1))).Success.ShouldBeTrue();
        var v2 = await ImportAsync();
        (await _publish.PublishAsync(V(v2))).Success.ShouldBeTrue();

        (await _exams.GetPreviewAsync(v1)).Status.ShouldBe(ExamVersionStatus.Archived);
        (await _exams.GetPreviewAsync(v2)).Status.ShouldBe(ExamVersionStatus.Published);
    }

    [Fact]
    public async Task Unpublish_archives_published_version_only()
    {
        var id = await ImportAsync();
        await Should.ThrowAsync<UserFriendlyException>(() => _publish.UnpublishAsync(V(id))); // đang Nháp
        await _publish.PublishAsync(V(id));
        await _publish.UnpublishAsync(V(id));
        (await _exams.GetPreviewAsync(id)).Status.ShouldBe(ExamVersionStatus.Archived);
    }

    [Fact]
    public async Task Draft_copy_of_published_version_is_deep_and_original_stays_unchanged()
    {
        var v1 = await ImportAsync();
        await _publish.PublishAsync(V(v1));
        var original = await _exams.GetPreviewAsync(v1);

        var v2 = await _publish.CreateDraftAsync(V(v1));
        v2.ShouldNotBe(v1);
        var copy = await _exams.GetPreviewAsync(v2);
        copy.Status.ShouldBe(ExamVersionStatus.Draft);
        copy.VersionNo.ShouldBe(2);
        copy.Issues.ShouldBeEmpty();
        copy.Parts.SelectMany(p => p.Questions.Concat(p.Groups.SelectMany(g => g.Questions))).Count().ShouldBe(100);
        copy.Parts.Select(p => p.Groups.Count).ShouldBe(original.Parts.Select(p => p.Groups.Count));

        // ID của bản sao khác bản gốc (nội dung độc lập) nhưng đáp án đúng giống nhau theo mã.
        var oq = original.Parts.SelectMany(p => p.Questions.Concat(p.Groups.SelectMany(g => g.Questions))).OrderBy(x => x.SourceNo).ToList();
        var cq = copy.Parts.SelectMany(p => p.Questions.Concat(p.Groups.SelectMany(g => g.Questions))).OrderBy(x => x.SourceNo).ToList();
        cq.Select(x => x.Code).ShouldBe(oq.Select(x => x.Code));
        cq.Select(x => x.CorrectOptionCode).ShouldBe(oq.Select(x => x.CorrectOptionCode));
        cq.Select(x => x.Id).Intersect(oq.Select(x => x.Id)).ShouldBeEmpty();

        // Ảnh dùng chung (không nhân đôi dữ liệu ảnh).
        var imgOrig = original.Parts[2].Groups[0].Documents[0].Blocks[1].MediaAssetId;
        copy.Parts[2].Groups[0].Documents[0].Blocks[1].MediaAssetId.ShouldBe(imgOrig);

        // Sửa bản Nháp không làm thay đổi bản đã xuất bản.
        var cqFirst = copy.Parts[0].Questions[0];
        await _edit.UpdateQuestionAsync(QInput(cqFirst, answer: "D", stem: "Chỉ sửa ở bản nháp"));
        (await _exams.GetPreviewAsync(v1)).Parts[0].Questions[0].Stem.ShouldBe(original.Parts[0].Questions[0].Stem);
        (await _exams.GetPreviewAsync(v1)).Parts[0].Questions[0].CorrectOptionCode.ShouldBe("B");
        (await _exams.GetPreviewAsync(v2)).Parts[0].Questions[0].CorrectOptionCode.ShouldBe("D");

        await UsingDbContextAsync(async db =>
        {
            (await db.MediaAssets.CountAsync()).ShouldBe(25);
            (await db.Questions.CountAsync()).ShouldBe(200);
        });
    }

    [Fact]
    public async Task Only_one_draft_per_exam_and_delete_draft_frees_the_slot_without_reusing_numbers()
    {
        var v1 = await ImportAsync();
        await _publish.PublishAsync(V(v1));
        var v2 = await _publish.CreateDraftAsync(V(v1));

        await Should.ThrowAsync<UserFriendlyException>(() => _publish.CreateDraftAsync(V(v1)));

        await _publish.DeleteDraftAsync(V(v2));
        (await _exams.GetVersionsAsync()).Select(x => x.VersionNo).ShouldBe(new[] { 1 });

        var v3 = await _publish.CreateDraftAsync(V(v1));
        (await _exams.GetPreviewAsync(v3)).VersionNo.ShouldBe(3); // số 2 đã dùng, không tái sử dụng
    }

    [Fact]
    public async Task Validate_reports_issues_for_stored_version()
    {
        var id = await ImportAsync();
        (await _publish.ValidateAsync(V(id))).ShouldBeEmpty();
    }

    [Fact]
    public async Task Anonymous_user_cannot_use_edit_or_publish_services()
    {
        var id = await ImportAsync();
        AbpSession.UserId = null;
        await Should.ThrowAsync<AbpAuthorizationException>(() => _publish.PublishAsync(V(id)));
        await Should.ThrowAsync<AbpAuthorizationException>(() => _exams.GetPreviewAsync(id));
        await Should.ThrowAsync<AbpAuthorizationException>(() => _edit.UpdatePartAsync(new UpdatePartInput { PartId = 1, ShuffleItems = true }));
    }
}
