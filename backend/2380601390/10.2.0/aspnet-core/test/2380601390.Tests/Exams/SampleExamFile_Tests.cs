using System;
using System.IO;
using System.Linq;
using _2380601390.Exams;
using _2380601390.Exams.Import;
using Shouldly;
using Xunit;

namespace _2380601390.Tests.Exams;

/// <summary>Kiểm tra file Word đề mẫu nộp kèm (docs/sample/TOEIC_Reading_Test05.docx) đúng 100% chuẩn tags v1.</summary>
public class SampleExamFile_Tests
{
    private static string FindSample()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var p = Path.Combine(dir.FullName, "docs", "sample", "TOEIC_Reading_Test05.docx");
            if (File.Exists(p)) return p;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Không tìm thấy docs/sample/TOEIC_Reading_Test05.docx");
    }

    private static ImportResult Import()
    {
        using var fs = File.OpenRead(FindSample());
        return ExamDocxImporter.Parse(fs, ExamRules.ValidationOptions());
    }

    [Fact]
    public void Sample_file_is_100_percent_valid_with_three_parts_and_images()
    {
        var r = Import();

        r.Errors.Select(e => e.ToString()).ShouldBeEmpty();
        r.Success.ShouldBeTrue();
        r.Exam.Id.ShouldBe("RC-TEST-05");
        r.Exam.DurationMinutes.ShouldBe(75);
        r.Exam.AllQuestions.Count().ShouldBe(100);
        r.Exam.Parts.Select(p => p.PartNo).ShouldBe(new[] { 5, 6, 7 });
        r.Exam.Parts[0].Questions.Count.ShouldBe(30);
        r.Exam.Parts[1].Groups.Count.ShouldBe(4);
        r.Exam.Parts[2].Groups.Count.ShouldBe(15);

        var images = r.Exam.Parts.SelectMany(p => p.Groups).SelectMany(g => g.Documents).SelectMany(d => d.Blocks)
            .Where(b => b.Type == ContentBlockType.Image).ToList();
        images.Count.ShouldBe(4);
        images.Select(i => i.Image.Sha256).Distinct().Count().ShouldBe(4);
    }

    [Fact]
    public void Sample_file_has_complete_answers_and_position_questions_do_not_shuffle_options()
    {
        var r = Import();
        r.Success.ShouldBeTrue();

        r.Exam.AllQuestions.All(q => q.Options.Count == 4 && !string.IsNullOrWhiteSpace(q.Explanation)).ShouldBeTrue();
        r.Exam.AllQuestions.Select(q => q.AnswerRef).Distinct().OrderBy(x => x).ShouldBe(new[] { "A", "B", "C", "D" });

        // Câu chèn câu ([1]-[4]) phụ thuộc thứ tự lựa chọn nên phải cấm hoán vị lựa chọn.
        foreach (var no in new[] { 167, 171 })
        {
            r.Exam.AllQuestions.Single(q => q.No == no).ShouldSatisfyAllConditions(
                q => q.ShuffleOptions.ShouldBeFalse(),
                q => q.Options.Select(o => o.Text).ShouldBe(new[] { "[1]", "[2]", "[3]", "[4]" }));
        }

        // Part 6: mỗi câu gắn đúng một chỗ trống, thân câu hỏi rỗng.
        r.Exam.Parts[1].Groups.SelectMany(g => g.Questions).All(q => q.Stem == "").ShouldBeTrue();
    }
}
