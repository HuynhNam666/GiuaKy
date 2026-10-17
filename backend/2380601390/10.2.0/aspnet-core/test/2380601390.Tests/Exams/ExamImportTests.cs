using System.Collections.Generic;
using System.IO;
using System.Linq;
using _2380601390.Exams;
using _2380601390.Exams.Import;
using Shouldly;
using Xunit;

namespace _2380601390.Tests.Exams;

public class ExamImportTests
{
    // ---------- dữ liệu minh họa (tự soạn, KHÔNG phải đề TOEIC thật) ----------

    private static IEnumerable<string> Q(string id, int no, string stem = "Demo stem ------- here.", string answer = "B", string shuffle = "on", bool withStem = true)
    {
        yield return $"[QUESTION id={id} no={no} shuffle-options={shuffle}]";
        if (withStem) yield return stem;
        yield return "[OPTION id=A]alpha";
        yield return "[OPTION id=B]beta";
        yield return "[OPTION id=C]gamma";
        yield return "[OPTION id=D]delta";
        yield return $"[ANSWER ref={answer}]";
        yield return "[EXPLANATION]";
        yield return "Giải thích minh họa cho " + id;
        yield return "[/EXPLANATION]";
        yield return "[/QUESTION]";
    }

    private static List<string> MiniPart5()
    {
        var l = new List<string>
        {
            "[EXAM id=T tagsv=1 title=\"Mini\" duration=75 total=2]",
            "[PART no=5 from=101 to=102 shuffle-items=on]"
        };
        l.AddRange(Q("P5-101", 101));
        l.AddRange(Q("P5-102", 102));
        l.Add("[/PART]");
        l.Add("[/EXAM]");
        return l;
    }

    private static ImportResult Run(IEnumerable<string> lines, bool split = false, bool requireAll = false, int? expected = null)
    {
        var ms = new DocxTestBuilder().Lines(lines, split).Build();
        return ExamDocxImporter.Parse(ms, new ExamImportOptions { RequireAllParts = requireAll, ExpectedTotalQuestions = expected });
    }

    private static List<string> Codes(ImportResult r) => r.Errors.Select(e => e.Code).ToList();

    private static void ShouldFailWith(ImportResult r, string code)
    {
        r.Success.ShouldBeFalse();
        r.Exam.ShouldBeNull();
        Codes(r).ShouldContain(code, string.Join("\n", r.Errors.Select(e => e.ToString())));
    }

    // ---------- đề đầy đủ 100 câu, 3 Part, có ảnh ----------

    internal static MemoryStream BuildFull100(bool dropLastAnswer = false, int imageSeedBase = 0)
    {
        var b = new DocxTestBuilder();
        b.Line("[EXAM id=DEMO-01 tagsv=1 title=\"Demo Reading Test (minh hoa)\" duration=75 total=100]", true);

        b.Line("[PART no=5 from=101 to=130 shuffle-items=on]");
        for (var n = 101; n <= 130; n++) b.Lines(Q("P5-" + n, n), true);
        b.Line("[/PART]");

        b.Line("[PART no=6 from=131 to=146 shuffle-items=on]");
        var no = 131;
        for (var g = 1; g <= 4; g++)
        {
            b.Line($"[GROUP id=G6-{g} shuffle-questions=off]");
            b.Line($"[DOCUMENT id=D6-{g}]");
            b.Line($"Dear residents, notice {g}: {{{{blank:P6-{no}}}}} first. Then {{{{blank:P6-{no + 1}}}}} second.");
            if (g == 2) b.ImageLine(DocxTestBuilder.Png(imageSeedBase + 2));
            b.Line($"Next paragraph {{{{blank:P6-{no + 2}}}}} third and {{{{blank:P6-{no + 3}}}}} fourth.");
            b.Line("[/DOCUMENT]");
            for (var i = 0; i < 4; i++) b.Lines(Q("P6-" + (no + i), no + i, withStem: false), true);
            b.Line("[/GROUP]");
            no += 4;
        }

        b.Line("[/PART]");

        b.Line("[PART no=7 from=147 to=200 shuffle-items=on]");
        no = 147;
        for (var g = 1; g <= 18; g++)
        {
            b.Line($"[GROUP id=G7-{g} shuffle-questions={(g % 2 == 0 ? "on" : "off")}]");
            b.Line($"[DOCUMENT id=D7-{g}a]");
            b.Line($"Email text before logo {g}.");
            b.ImageLine(DocxTestBuilder.Png(imageSeedBase + 10 + g));
            b.Line($"Email text after logo {g}.");
            b.Line("[/DOCUMENT]");
            if (g % 3 == 0)
            {
                b.Line($"[DOCUMENT id=D7-{g}b]");
                b.ImageLine(DocxTestBuilder.Png(imageSeedBase + 50 + g));
                b.Line("[/DOCUMENT]");
            }

            for (var i = 0; i < 3; i++)
            {
                var lines = Q($"P7-{no}", no, "What is the purpose of the message?").ToList();
                if (dropLastAnswer && no == 200) lines.RemoveAll(x => x.StartsWith("[ANSWER"));
                b.Lines(lines, true);
                no++;
            }

            b.Line("[/GROUP]");
        }

        b.Line("[/PART]");
        b.Line("[/EXAM]");
        return b.Build();
    }

    [Fact]
    public void Valid_full_exam_has_three_parts_100_questions_and_images()
    {
        var r = ExamDocxImporter.Parse(BuildFull100(), new ExamImportOptions { ExpectedTotalQuestions = 100 });

        r.Errors.Select(e => e.ToString()).ShouldBeEmpty();
        r.Success.ShouldBeTrue();
        r.Exam.Parts.Select(p => p.PartNo).ShouldBe(new[] { 5, 6, 7 });
        r.Exam.AllQuestions.Count().ShouldBe(100);
        r.Exam.Parts[0].Questions.Count.ShouldBe(30);
        r.Exam.Parts[0].Groups.ShouldBeEmpty();
        r.Exam.Parts[1].Groups.Count.ShouldBe(4);
        r.Exam.Parts[2].Groups.Count.ShouldBe(18);
        r.Exam.Parts[2].Groups.Count(g => g.Documents.Count == 2).ShouldBe(6);
        r.Exam.Parts[2].Groups[1].ShuffleQuestions.ShouldBeTrue();

        var images = r.Exam.Parts.SelectMany(p => p.Groups).SelectMany(g => g.Documents).SelectMany(d => d.Blocks)
            .Where(b => b.Type == ContentBlockType.Image).ToList();
        images.Count.ShouldBe(1 + 18 + 6);
        images.All(i => i.Image.Sha256.Length == 64 && i.Image.ContentType == "image/png").ShouldBeTrue();
        images.Select(i => i.Image.Sha256).Distinct().Count().ShouldBe(images.Count);

        r.Exam.Parts[0].Questions[0].AnswerRef.ShouldBe("B");
        r.Exam.Parts[0].Questions[0].Options.Select(o => o.Code).ShouldBe(new[] { "A", "B", "C", "D" });
        r.Exam.Parts[0].Questions[0].Explanation.ShouldContain("P5-101");
    }

    [Fact]
    public void Part7_document_keeps_text_image_text_order()
    {
        var r = ExamDocxImporter.Parse(BuildFull100(), new ExamImportOptions());
        var doc = r.Exam.Parts[2].Groups[0].Documents[0];
        doc.Blocks.Select(b => b.Type).ShouldBe(new[] { ContentBlockType.Text, ContentBlockType.Image, ContentBlockType.Text });
        doc.Blocks[0].Text.ShouldBe("Email text before logo 1.");
        doc.Blocks[2].Text.ShouldBe("Email text after logo 1.");
        doc.Blocks[1].Image.AltText.ShouldBe("alt");
    }

    [Fact]
    public void Part6_blanks_are_kept_in_document_text()
    {
        var r = ExamDocxImporter.Parse(BuildFull100(), new ExamImportOptions());
        var g = r.Exam.Parts[1].Groups[0];
        g.Documents[0].Blocks[0].Text.ShouldContain("{{blank:P6-131}}");
        g.Questions.Select(q => q.Stem).All(string.IsNullOrEmpty).ShouldBeTrue();
        g.Questions.Select(q => q.No).ShouldBe(new[] { 131, 132, 133, 134 });
    }

    // ---------- tag bị tách run, ký tự đặc biệt ----------

    [Fact]
    public void Tags_split_across_runs_and_smart_quotes_are_parsed()
    {
        var lines = MiniPart5();
        lines[0] = "[EXAM id=T tagsv=1 title=“Mini test” duration=75 total=2]";
        var r = Run(lines, split: true);

        r.Errors.Select(e => e.ToString()).ShouldBeEmpty();
        r.Exam.Title.ShouldBe("Mini test");
    }

    [Fact]
    public void Non_breaking_spaces_inside_tag_are_tolerated()
    {
        var lines = MiniPart5();
        lines[1] = "[PART no=5 from=101 to=102 shuffle-items=on]";
        Run(lines).Success.ShouldBeTrue();
    }

    // ---------- lỗi cấu trúc ----------

    [Fact]
    public void Missing_close_tag_is_reported_and_nothing_is_returned()
    {
        var lines = MiniPart5();
        lines.Remove("[/PART]");
        var r = Run(lines);
        ShouldFailWith(r, "E_TAG_UNCLOSED");
        r.Errors.First(e => e.Code == "E_TAG_UNCLOSED").Message.ShouldContain("PART");
    }

    [Fact]
    public void Missing_exam_tag_is_reported()
    {
        var lines = MiniPart5().Skip(1).ToList();
        ShouldFailWith(Run(lines), "E_TAG_MISSING");
    }

    [Fact]
    public void Missing_required_attribute_and_unknown_attribute()
    {
        var lines = MiniPart5();
        lines[2] = "[QUESTION id=P5-101 no=101]";
        ShouldFailWith(Run(lines), "E_ATTR_MISSING");

        lines = MiniPart5();
        lines[2] = "[QUESTION id=P5-101 no=101 shuffle-options=on colour=red]";
        ShouldFailWith(Run(lines), "E_ATTR_UNKNOWN");
    }

    [Fact]
    public void Unknown_tag_and_stray_text_are_reported()
    {
        var lines = MiniPart5();
        lines.Insert(2, "[FOO id=1]");
        ShouldFailWith(Run(lines), "E_TAG_UNKNOWN");

        lines = MiniPart5();
        lines.Insert(1, "Đây là văn bản lạc");
        ShouldFailWith(Run(lines), "E_STRAY_TEXT");
    }

    [Fact]
    public void Tag_inside_table_is_rejected()
    {
        var b = new DocxTestBuilder();
        b.Line("[EXAM id=T tagsv=1 title=\"Mini\" duration=75 total=2]");
        b.Table(new[] { "[PART no=5 from=101 to=102 shuffle-items=on]", "x" });
        var r = ExamDocxImporter.Parse(b.Build(), new ExamImportOptions { RequireAllParts = false });
        ShouldFailWith(r, "E_TAG_IN_TABLE");
        r.Errors.First(e => e.Code == "E_TAG_IN_TABLE").Table.ShouldBe(1);
    }

    [Fact]
    public void Group_in_part5_and_question_outside_group_in_part6()
    {
        var lines = new List<string>
        {
            "[EXAM id=T tagsv=1 title=\"Mini\" duration=75 total=1]",
            "[PART no=5 from=101 to=101 shuffle-items=on]",
            "[GROUP id=G]",
            "[/GROUP]",
            "[/PART]",
            "[/EXAM]"
        };
        ShouldFailWith(Run(lines), "E_GROUP_IN_PART5");

        lines = new List<string>
        {
            "[EXAM id=T tagsv=1 title=\"Mini\" duration=75 total=1]",
            "[PART no=6 from=131 to=131 shuffle-items=on]"
        };
        lines.AddRange(Q("P6-131", 131, withStem: false));
        lines.Add("[/PART]");
        lines.Add("[/EXAM]");
        ShouldFailWith(Run(lines), "E_QUESTION_OUTSIDE_GROUP");
    }

    // ---------- lỗi nội dung ----------

    [Fact]
    public void Question_with_three_options_is_rejected_with_location()
    {
        var lines = MiniPart5();
        var idx = lines.FindIndex(x => x.StartsWith("[OPTION id=D]"));
        lines.RemoveAt(idx);
        var r = Run(lines);
        ShouldFailWith(r, "E_OPTION_COUNT");
        var e = r.Errors.First(x => x.Code == "E_OPTION_COUNT");
        e.QuestionNo.ShouldBe(101);
        e.PartNo.ShouldBe(5);
        e.Paragraph.ShouldBe(3);
        e.Message.ShouldContain("Đoạn 3");
        e.Message.ShouldContain("Part 5");
        e.Message.ShouldContain("câu 101");
    }

    [Fact]
    public void Answer_pointing_to_missing_option_is_rejected()
    {
        var lines = MiniPart5();
        var idx = lines.FindIndex(x => x.StartsWith("[ANSWER"));
        lines[idx] = "[ANSWER ref=E]";
        ShouldFailWith(Run(lines), "E_ANSWER_NOT_FOUND");
    }

    [Fact]
    public void Missing_answer_and_missing_explanation_are_rejected()
    {
        var lines = MiniPart5();
        lines.RemoveAt(lines.FindIndex(x => x.StartsWith("[ANSWER")));
        ShouldFailWith(Run(lines), "E_ANSWER_MISSING");

        lines = MiniPart5();
        var i = lines.IndexOf("[EXPLANATION]");
        lines.RemoveRange(i, 3);
        ShouldFailWith(Run(lines), "E_EXPLANATION_MISSING");
    }

    [Fact]
    public void Duplicate_question_number_and_broken_sequence_are_rejected()
    {
        var lines = MiniPart5();
        lines[lines.IndexOf("[QUESTION id=P5-102 no=102 shuffle-options=on]")] = "[QUESTION id=P5-102 no=101 shuffle-options=on]";
        ShouldFailWith(Run(lines), "E_DUP_NO");

        lines = new List<string>
        {
            "[EXAM id=T tagsv=1 title=\"Mini\" duration=75 total=3]",
            "[PART no=5 from=101 to=103 shuffle-items=on]"
        };
        lines.AddRange(Q("A", 101));
        lines.AddRange(Q("B", 103));
        lines.AddRange(Q("C", 102));
        lines.Add("[/PART]");
        lines.Add("[/EXAM]");
        ShouldFailWith(Run(lines), "E_NO_SEQUENCE");
    }

    [Fact]
    public void Duplicate_question_id_and_out_of_range_number_are_rejected()
    {
        var lines = MiniPart5();
        lines[lines.IndexOf("[QUESTION id=P5-102 no=102 shuffle-options=on]")] = "[QUESTION id=P5-101 no=102 shuffle-options=on]";
        ShouldFailWith(Run(lines), "E_DUP_ID");

        lines = MiniPart5();
        lines[lines.IndexOf("[QUESTION id=P5-102 no=102 shuffle-options=on]")] = "[QUESTION id=P5-102 no=150 shuffle-options=on]";
        ShouldFailWith(Run(lines), "E_NO_RANGE");
    }

    [Fact]
    public void Total_mismatch_and_configured_total_are_checked()
    {
        var lines = MiniPart5();
        lines[0] = "[EXAM id=T tagsv=1 title=\"Mini\" duration=75 total=100]";
        ShouldFailWith(Run(lines), "E_TOTAL_MISMATCH");

        ShouldFailWith(Run(MiniPart5(), expected: 100), "E_TOTAL_CONFIG");
    }

    [Fact]
    public void All_three_parts_are_required_by_default()
    {
        var r = Run(MiniPart5(), requireAll: true);
        ShouldFailWith(r, "E_PART_MISSING");
        r.Errors.Count(e => e.Code == "E_PART_MISSING").ShouldBe(2);
    }

    [Fact]
    public void Part6_shuffle_questions_on_is_forbidden()
    {
        var r = Run(Part6Doc(groupShuffle: "on"));
        ShouldFailWith(r, "E_SHUFFLE_FORBIDDEN");
    }

    [Fact]
    public void Part6_blank_must_match_questions()
    {
        Run(Part6Doc()).Success.ShouldBeTrue();

        ShouldFailWith(Run(Part6Doc(secondBlank: "P6-139")), "E_BLANK_REF");
        ShouldFailWith(Run(Part6Doc(secondBlank: "P6-139")), "E_BLANK_MISSING");
        ShouldFailWith(Run(Part6Doc(secondBlank: "P6-131")), "E_BLANK_DUP");
        ShouldFailWith(Run(Part6Doc(swapBlanks: true)), "E_BLANK_ORDER");
        ShouldFailWith(Run(Part6Doc(brokenBlank: true)), "E_BLANK_SYNTAX");
    }

    private static List<string> Part6Doc(string groupShuffle = "off", string secondBlank = "P6-132", bool swapBlanks = false, bool brokenBlank = false)
    {
        var first = swapBlanks ? "P6-132" : "P6-131";
        var second = swapBlanks ? "P6-131" : secondBlank;
        var l = new List<string>
        {
            "[EXAM id=T tagsv=1 title=\"Mini\" duration=75 total=2]",
            "[PART no=6 from=131 to=132 shuffle-items=off]",
            $"[GROUP id=G6-1 shuffle-questions={groupShuffle}]",
            "[DOCUMENT id=D6-1]",
            brokenBlank ? "Text {{blank:P6-131 broken and {{blank:P6-132}}" : $"Text {{{{blank:{first}}}}} and {{{{blank:{second}}}}} end."
        };
        l.Add("[/DOCUMENT]");
        l.AddRange(Q("P6-131", 131, withStem: false));
        l.AddRange(Q("P6-132", 132, withStem: false));
        l.Add("[/GROUP]");
        l.Add("[/PART]");
        l.Add("[/EXAM]");
        return l;
    }

    [Fact]
    public void Blank_marker_outside_part6_is_rejected()
    {
        var lines = new List<string>
        {
            "[EXAM id=T tagsv=1 title=\"Mini\" duration=75 total=1]",
            "[PART no=7 from=147 to=147 shuffle-items=off]",
            "[GROUP id=G7]",
            "[DOCUMENT id=D7]",
            "Text with {{blank:P7-147}} marker.",
            "[/DOCUMENT]"
        };
        lines.AddRange(Q("P7-147", 147));
        lines.Add("[/GROUP]");
        lines.Add("[/PART]");
        lines.Add("[/EXAM]");
        ShouldFailWith(Run(lines), "E_BLANK_UNEXPECTED");
    }

    [Fact]
    public void Group_without_document_or_empty_document_is_rejected()
    {
        var lines = new List<string>
        {
            "[EXAM id=T tagsv=1 title=\"Mini\" duration=75 total=1]",
            "[PART no=7 from=147 to=147 shuffle-items=off]",
            "[GROUP id=G7]"
        };
        lines.AddRange(Q("P7-147", 147));
        lines.Add("[/GROUP]");
        lines.Add("[/PART]");
        lines.Add("[/EXAM]");
        ShouldFailWith(Run(lines), "E_GROUP_NO_DOCUMENT");

        lines = new List<string>
        {
            "[EXAM id=T tagsv=1 title=\"Mini\" duration=75 total=1]",
            "[PART no=7 from=147 to=147 shuffle-items=off]",
            "[GROUP id=G7]",
            "[DOCUMENT id=D7]",
            "[/DOCUMENT]"
        };
        lines.AddRange(Q("P7-147", 147));
        lines.Add("[/GROUP]");
        lines.Add("[/PART]");
        lines.Add("[/EXAM]");
        ShouldFailWith(Run(lines), "E_DOCUMENT_EMPTY");
    }

    // ---------- ảnh ----------

    private static ImportResult Part7WithImage(DocxTestBuilder b, System.Action<DocxTestBuilder> imageInserter)
    {
        b.Line("[EXAM id=T tagsv=1 title=\"Mini\" duration=75 total=1]");
        b.Line("[PART no=7 from=147 to=147 shuffle-items=off]");
        b.Line("[GROUP id=G7]");
        b.Line("[DOCUMENT id=D7]");
        b.Line("Some text.");
        imageInserter(b);
        b.Line("[/DOCUMENT]");
        b.Lines(Q("P7-147", 147));
        b.Line("[/GROUP]");
        b.Line("[/PART]");
        b.Line("[/EXAM]");
        return ExamDocxImporter.Parse(b.Build(), new ExamImportOptions { RequireAllParts = false });
    }

    [Fact]
    public void Inline_image_is_accepted_floating_and_linked_images_are_rejected()
    {
        Part7WithImage(new DocxTestBuilder(), b => b.ImageLine(DocxTestBuilder.Png(1))).Success.ShouldBeTrue();
        ShouldFailWith(Part7WithImage(new DocxTestBuilder(), b => b.ImageLine(DocxTestBuilder.Png(1), anchored: true)), "E_IMAGE_UNSUPPORTED");
        ShouldFailWith(Part7WithImage(new DocxTestBuilder(), b => b.ImageLine(DocxTestBuilder.Png(1), linked: true)), "E_IMAGE_UNSUPPORTED");
    }

    [Fact]
    public void Image_with_corrupt_data_or_wrong_format_is_rejected()
    {
        ShouldFailWith(Part7WithImage(new DocxTestBuilder(), b => b.ImageLine(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 })), "E_IMAGE_UNSUPPORTED");
    }

    [Fact]
    public void Image_outside_document_is_rejected()
    {
        var b = new DocxTestBuilder();
        b.Line("[EXAM id=T tagsv=1 title=\"Mini\" duration=75 total=1]");
        b.Line("[PART no=5 from=101 to=101 shuffle-items=off]");
        b.Line("[QUESTION id=Q1 no=101 shuffle-options=on]");
        b.Line("Stem");
        b.ImageLine(DocxTestBuilder.Png(3));
        b.Line("[OPTION id=A]a");
        b.Line("[OPTION id=B]b");
        b.Line("[OPTION id=C]c");
        b.Line("[OPTION id=D]d");
        b.Line("[ANSWER ref=A]");
        b.Line("[EXPLANATION]");
        b.Line("why");
        b.Line("[/EXPLANATION]");
        b.Line("[/QUESTION]");
        b.Line("[/PART]");
        b.Line("[/EXAM]");
        ShouldFailWith(ExamDocxImporter.Parse(b.Build(), new ExamImportOptions { RequireAllParts = false }), "E_IMAGE_PLACEMENT");
    }

    // ---------- tệp, tính toàn vẹn (all-or-nothing ở mức parser) ----------

    [Fact]
    public void Non_docx_inputs_are_rejected()
    {
        ShouldFailWith(ExamDocxImporter.Parse(new MemoryStream(new byte[] { 1, 2, 3, 4, 5 })), "E_FILE_INVALID");
        ShouldFailWith(ExamDocxImporter.Parse(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("PK this is not really a zip file at all"))), "E_FILE_INVALID");
        ShouldFailWith(ExamDocxImporter.Parse(new MemoryStream()), "E_FILE_INVALID");
    }

    [Fact]
    public void Error_at_end_of_file_returns_no_exam_at_all()
    {
        var ok = ExamDocxImporter.Parse(BuildFull100(), new ExamImportOptions());
        ok.Success.ShouldBeTrue();

        var bad = ExamDocxImporter.Parse(BuildFull100(dropLastAnswer: true), new ExamImportOptions());
        bad.Success.ShouldBeFalse();
        bad.Exam.ShouldBeNull();
        var e = bad.Errors.Single();
        e.Code.ShouldBe("E_ANSWER_MISSING");
        e.QuestionNo.ShouldBe(200);
    }

    [Fact]
    public void Multiple_independent_errors_are_all_reported()
    {
        var lines = MiniPart5();
        lines.RemoveAt(lines.FindIndex(x => x.StartsWith("[OPTION id=D]")));
        lines[lines.FindLastIndex(x => x.StartsWith("[ANSWER"))] = "[ANSWER ref=Z]";
        var r = Run(lines);
        ShouldFailWith(r, "E_OPTION_COUNT");
        Codes(r).ShouldContain("E_ANSWER_NOT_FOUND");
    }

    [Fact]
    public void Unsupported_tags_version_is_rejected()
    {
        var lines = MiniPart5();
        lines[0] = "[EXAM id=T tagsv=2 title=\"Mini\" duration=75 total=2]";
        ShouldFailWith(Run(lines), "E_TAGSV_UNSUPPORTED");
    }
}
