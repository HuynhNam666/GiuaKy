using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace _2380601390.Exams.Import;

/// <summary>Kiểm tra ràng buộc ngữ nghĩa của ParsedExam. Không sửa dữ liệu, chỉ báo lỗi.</summary>
public static class ExamValidator
{
    private static readonly Regex BlankOpenRx = new Regex(@"\{\{\s*blank", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BlankRx = new Regex(@"\{\{\s*blank\s*:\s*([A-Za-z0-9_-]{1,40})\s*\}\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<ImportError> Validate(ParsedExam exam, ExamImportOptions opt)
    {
        var errors = new List<ImportError>();

        if (opt.ExpectedTotalQuestions.HasValue && exam.TotalQuestions != opt.ExpectedTotalQuestions.Value)
        {
            errors.Add(ImportError.Create("E_TOTAL_CONFIG",
                "khai báo total=" + exam.TotalQuestions + " nhưng cấu hình đề yêu cầu " + opt.ExpectedTotalQuestions.Value + " câu.",
                paragraph: exam.Paragraph, tag: "EXAM"));
        }

        if (opt.RequireAllParts)
        {
            foreach (var no in new[] { 5, 6, 7 })
            {
                if (exam.Parts.All(p => p.PartNo != no))
                {
                    errors.Add(ImportError.Create("E_PART_MISSING", "thiếu Part " + no + " (đề phải có đủ Part 5, 6, 7).", paragraph: exam.Paragraph));
                }
            }
        }

        CheckUniqueIds(exam, errors);

        var counted = 0;
        foreach (var part in exam.Parts)
        {
            counted += part.QuestionsInOrder.Count();
            CheckPart(part, errors);
        }

        if (counted != exam.TotalQuestions)
        {
            errors.Add(ImportError.Create("E_TOTAL_MISMATCH",
                "khai báo total=" + exam.TotalQuestions + " nhưng đếm được " + counted + " câu.",
                paragraph: exam.Paragraph, tag: "EXAM"));
        }

        return errors;
    }

    private static void CheckUniqueIds(ParsedExam exam, List<ImportError> errors)
    {
        var groups = new Dictionary<string, ParsedGroup>();
        var docs = new Dictionary<string, ParsedDocument>();
        var questions = new Dictionary<string, ParsedQuestion>();

        foreach (var part in exam.Parts)
        {
            foreach (var g in part.Groups)
            {
                if (g.Code != null)
                {
                    if (groups.TryGetValue(g.Code, out var first))
                        errors.Add(ImportError.Create("E_DUP_ID", "id nhóm '" + g.Code + "' trùng với nhóm ở đoạn " + first.Paragraph + ".", paragraph: g.Paragraph, partNo: part.PartNo, groupCode: g.Code));
                    else groups[g.Code] = g;
                }

                foreach (var d in g.Documents)
                {
                    if (d.Code == null) continue;
                    if (docs.TryGetValue(d.Code, out var first))
                        errors.Add(ImportError.Create("E_DUP_ID", "id tài liệu '" + d.Code + "' trùng với tài liệu ở đoạn " + first.Paragraph + ".", paragraph: d.Paragraph, partNo: part.PartNo, groupCode: g.Code));
                    else docs[d.Code] = d;
                }
            }

            foreach (var q in part.QuestionsInOrder)
            {
                if (q.Code == null) continue;
                if (questions.TryGetValue(q.Code, out var first))
                    errors.Add(ImportError.Create("E_DUP_ID", "id câu hỏi '" + q.Code + "' trùng với câu ở đoạn " + first.Paragraph + ".", paragraph: q.Paragraph, partNo: part.PartNo, questionNo: q.No));
                else questions[q.Code] = q;
            }
        }
    }

    private static ImportError QErr(string code, string text, ParsedPart part, ParsedGroup group, ParsedQuestion q) =>
        ImportError.Create(code, text, paragraph: q.Paragraph, partNo: part.PartNo, groupCode: group?.Code, questionNo: q.No == 0 ? (int?)null : q.No);

    private static void CheckPart(ParsedPart part, List<ImportError> errors)
    {
        // Số câu, thứ tự và phạm vi: theo đúng thứ tự xuất hiện trong file phải là from, from+1, ..., to.
        var seen = new Dictionary<int, ParsedQuestion>();
        var all = part.QuestionsInOrder.ToList();
        var expected = part.From;
        foreach (var q in all)
        {
            if (q.No == 0) continue;
            if (seen.TryGetValue(q.No, out var firstDup))
            {
                errors.Add(QErr("E_DUP_NO", "số câu " + q.No + " bị trùng (cũng xuất hiện ở đoạn " + firstDup.Paragraph + ").", part, null, q));
            }
            else
            {
                seen[q.No] = q;
                if (q.No < part.From || q.No > part.To)
                {
                    errors.Add(QErr("E_NO_RANGE", "số câu " + q.No + " nằm ngoài phạm vi Part " + part.PartNo + " (" + part.From + "–" + part.To + ").", part, null, q));
                }
                else if (q.No != expected)
                {
                    errors.Add(QErr("E_NO_SEQUENCE", "Part " + part.PartNo + ": cần câu " + expected + " nhưng gặp câu " + q.No + ".", part, null, q));
                }
            }

            expected = q.No + 1;
        }

        var span = part.To - part.From + 1;
        if (all.Count != span)
        {
            errors.Add(ImportError.Create("E_PART_COUNT",
                "Part " + part.PartNo + " khai báo " + part.From + "–" + part.To + " (" + span + " câu) nhưng đếm được " + all.Count + " câu.",
                paragraph: part.Paragraph, partNo: part.PartNo));
        }

        foreach (var q in part.Questions) CheckQuestion(part, null, q, errors);

        foreach (var g in part.Groups)
        {
            if (part.PartNo == 6 && g.ShuffleQuestions)
            {
                errors.Add(ImportError.Create("E_SHUFFLE_FORBIDDEN", "Part 6 không cho hoán vị câu trong nhóm (shuffle-questions phải là off).",
                    paragraph: g.Paragraph, partNo: part.PartNo, groupCode: g.Code));
            }

            if (g.Documents.Count == 0)
            {
                errors.Add(ImportError.Create("E_GROUP_NO_DOCUMENT", "nhóm chưa có [DOCUMENT] nào.", paragraph: g.Paragraph, partNo: part.PartNo, groupCode: g.Code));
            }

            if (g.Questions.Count == 0)
            {
                errors.Add(ImportError.Create("E_GROUP_EMPTY", "nhóm chưa có [QUESTION] nào.", paragraph: g.Paragraph, partNo: part.PartNo, groupCode: g.Code));
            }

            foreach (var d in g.Documents)
            {
                if (d.Blocks.Count == 0)
                {
                    errors.Add(ImportError.Create("E_DOCUMENT_EMPTY", "tài liệu '" + d.Code + "' không có văn bản hoặc ảnh.", paragraph: d.Paragraph, partNo: part.PartNo, groupCode: g.Code));
                }
            }

            foreach (var q in g.Questions) CheckQuestion(part, g, q, errors);
            CheckBlanks(part, g, errors);
        }
    }

    private static void CheckQuestion(ParsedPart part, ParsedGroup group, ParsedQuestion q, List<ImportError> errors)
    {
        if (q.Options.Count != 4)
        {
            errors.Add(QErr("E_OPTION_COUNT", "câu " + q.No + " (id " + q.Code + ") có " + q.Options.Count + " lựa chọn, yêu cầu đúng 4 (A–D).", part, group, q));
        }

        if (string.IsNullOrEmpty(q.AnswerRef))
        {
            errors.Add(QErr("E_ANSWER_MISSING", "thiếu [ANSWER] cho câu " + q.No + ".", part, group, q));
        }
        else if (q.Options.All(o => o.Code != q.AnswerRef))
        {
            var inRange = q.AnswerRef == "A" || q.AnswerRef == "B" || q.AnswerRef == "C" || q.AnswerRef == "D";
            errors.Add(QErr("E_ANSWER_NOT_FOUND",
                inRange ? "đáp án '" + q.AnswerRef + "' không có trong các lựa chọn của câu."
                        : "đáp án '" + q.AnswerRef + "' không nằm trong A–D.", part, group, q));
        }

        if (string.IsNullOrWhiteSpace(q.Explanation))
        {
            errors.Add(QErr("E_EXPLANATION_MISSING", "thiếu [EXPLANATION] (gợi ý/giải thích) cho câu " + q.No + ".", part, group, q));
        }

        if (part.PartNo != 6 && string.IsNullOrWhiteSpace(q.Stem))
        {
            errors.Add(QErr("E_STEM_MISSING", "câu " + q.No + " thiếu nội dung câu hỏi (chỉ Part 6 được để trống).", part, group, q));
        }
    }

    private static void CheckBlanks(ParsedPart part, ParsedGroup g, List<ImportError> errors)
    {
        var texts = g.Documents
            .SelectMany(d => d.Blocks.Where(b => b.Type == ContentBlockType.Text).Select(b => new { Doc = d, Block = b }))
            .ToList();

        if (part.PartNo != 6)
        {
            foreach (var t in texts.Where(t => BlankOpenRx.IsMatch(t.Block.Text)))
            {
                errors.Add(ImportError.Create("E_BLANK_UNEXPECTED", "đánh dấu {{blank:...}} chỉ được dùng trong Part 6.",
                    paragraph: t.Block.Paragraph, partNo: part.PartNo, groupCode: g.Code));
            }

            return;
        }

        var refs = new List<string>();
        foreach (var t in texts)
        {
            var opens = BlankOpenRx.Matches(t.Block.Text).Count;
            var valid = BlankRx.Matches(t.Block.Text);
            if (opens != valid.Count)
            {
                errors.Add(ImportError.Create("E_BLANK_SYNTAX",
                    "đánh dấu chỗ trống sai cú pháp (dạng đúng: {{blank:id-câu}}).",
                    paragraph: t.Block.Paragraph, partNo: part.PartNo, groupCode: g.Code));
            }

            refs.AddRange(valid.Cast<Match>().Select(m => m.Groups[1].Value));
        }

        var codes = g.Questions.Where(q => q.Code != null).Select(q => q.Code).ToList();
        var refSeen = new HashSet<string>();
        foreach (var r in refs)
        {
            if (!codes.Contains(r))
            {
                errors.Add(ImportError.Create("E_BLANK_REF",
                    "chỗ trống tham chiếu câu '" + r + "' không có trong nhóm.",
                    paragraph: g.Paragraph, partNo: part.PartNo, groupCode: g.Code));
            }
            else if (!refSeen.Add(r))
            {
                errors.Add(ImportError.Create("E_BLANK_DUP",
                    "câu '" + r + "' được đánh dấu chỗ trống nhiều lần.",
                    paragraph: g.Paragraph, partNo: part.PartNo, groupCode: g.Code));
            }
        }

        foreach (var q in g.Questions.Where(q => q.Code != null))
        {
            if (!refs.Contains(q.Code))
            {
                errors.Add(QErr("E_BLANK_MISSING", "câu " + q.No + " (id " + q.Code + ") không được đánh dấu {{blank}} trong bài đọc.", part, g, q));
            }
        }

        var order = refs.Where(r => codes.Contains(r)).Distinct().ToList();
        var expectedOrder = codes.Where(order.Contains).ToList();
        if (!order.SequenceEqual(expectedOrder))
        {
            errors.Add(ImportError.Create("E_BLANK_ORDER",
                "thứ tự chỗ trống trong bài đọc (" + string.Join(", ", order) + ") không khớp thứ tự câu hỏi (" + string.Join(", ", expectedOrder) + ").",
                paragraph: g.Paragraph, partNo: part.PartNo, groupCode: g.Code));
        }
    }
}

/// <summary>Điểm vào duy nhất: .docx -> kết quả import (hoặc danh sách lỗi). Không ghi DB, không ghi đĩa.</summary>
public static class ExamDocxImporter
{
    public static ImportResult Parse(System.IO.Stream docx, ExamImportOptions options = null)
    {
        options = options ?? new ExamImportOptions();
        var read = DocxReader.Read(docx);
        if (read.Errors.Count > 0 && read.Items.Count == 0)
        {
            return new ImportResult { Errors = read.Errors };
        }

        var parsed = TagParser.Parse(read.Items, options);
        var errors = new List<ImportError>(read.Errors);
        errors.AddRange(parsed.Errors);

        // Giai đoạn 2 chỉ chạy khi cấu trúc thẻ đã đúng, để tránh lỗi dây chuyền do thiếu/đóng sai thẻ.
        if (errors.Count == 0 && parsed.Exam != null)
        {
            errors.AddRange(ExamValidator.Validate(parsed.Exam, options));
        }

        return new ImportResult { Errors = errors, Exam = errors.Count == 0 ? parsed.Exam : null };
    }
}
