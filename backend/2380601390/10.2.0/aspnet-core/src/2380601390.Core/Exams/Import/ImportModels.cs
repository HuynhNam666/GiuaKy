using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace _2380601390.Exams.Import;

/// <summary>Một lỗi import, kèm vị trí khi xác định được.</summary>
public class ImportError
{
    public string Code { get; set; }
    public string Message { get; set; }
    public int? Paragraph { get; set; }
    public int? Table { get; set; }
    public int? PartNo { get; set; }
    public string GroupCode { get; set; }
    public int? QuestionNo { get; set; }
    public string Tag { get; set; }

    public static ImportError Create(
        string code, string text,
        int? paragraph = null, int? table = null,
        int? partNo = null, string groupCode = null, int? questionNo = null, string tag = null)
    {
        var loc = new List<string>();
        if (paragraph.HasValue && paragraph.Value > 0) loc.Add("Đoạn " + paragraph.Value);
        if (table.HasValue) loc.Add("Bảng " + table.Value);
        if (partNo.HasValue) loc.Add("Part " + partNo.Value);
        if (!string.IsNullOrEmpty(groupCode)) loc.Add("nhóm " + groupCode);
        if (questionNo.HasValue) loc.Add("câu " + questionNo.Value);

        return new ImportError
        {
            Code = code,
            Message = loc.Count > 0 ? string.Join(", ", loc) + ": " + text : text,
            Paragraph = paragraph.HasValue && paragraph.Value > 0 ? paragraph : null,
            Table = table,
            PartNo = partNo,
            GroupCode = groupCode,
            QuestionNo = questionNo,
            Tag = tag
        };
    }

    public override string ToString() => "[" + Code + "] " + Message;
}

public class ExamImportOptions
{
    /// <summary>Bắt buộc có đủ Part 5, 6, 7.</summary>
    public bool RequireAllParts { get; set; } = true;

    /// <summary>Số câu mà đề phải có (cấu hình đề). null = không kiểm tra riêng.</summary>
    public int? ExpectedTotalQuestions { get; set; }

    public long MaxImageBytes { get; set; } = 5 * 1024 * 1024;

    public int SupportedTagsVersion { get; set; } = 1;
}

public class ImportResult
{
    public ParsedExam Exam { get; set; }
    public List<ImportError> Errors { get; set; } = new List<ImportError>();

    /// <summary>Chỉ true khi không có lỗi nào; khi đó Exam luôn khác null.</summary>
    public bool Success => Errors.Count == 0 && Exam != null;
}

public class ParsedExam
{
    public string Id { get; set; }
    public string Title { get; set; }
    public int DurationMinutes { get; set; }
    public int TotalQuestions { get; set; }
    public int TagsVersion { get; set; }
    public int Paragraph { get; set; }
    public List<ParsedPart> Parts { get; set; } = new List<ParsedPart>();

    public IEnumerable<ParsedQuestion> AllQuestions =>
        Parts.SelectMany(p => p.Questions.Concat(p.Groups.SelectMany(g => g.Questions)));
}

public class ParsedPart
{
    public int PartNo { get; set; }
    public int From { get; set; }
    public int To { get; set; }
    public bool ShuffleItems { get; set; }
    public int Paragraph { get; set; }

    /// <summary>Câu độc lập (Part 5).</summary>
    public List<ParsedQuestion> Questions { get; set; } = new List<ParsedQuestion>();

    /// <summary>Nhóm bài đọc (Part 6, 7).</summary>
    public List<ParsedGroup> Groups { get; set; } = new List<ParsedGroup>();

    /// <summary>Các câu theo đúng thứ tự xuất hiện trong file.</summary>
    public IEnumerable<ParsedQuestion> QuestionsInOrder =>
        Questions.Concat(Groups.SelectMany(g => g.Questions));
}

public class ParsedGroup
{
    public string Code { get; set; }
    public bool ShuffleQuestions { get; set; }
    public int Paragraph { get; set; }
    public List<ParsedDocument> Documents { get; set; } = new List<ParsedDocument>();
    public List<ParsedQuestion> Questions { get; set; } = new List<ParsedQuestion>();
}

public class ParsedDocument
{
    public string Code { get; set; }
    public string Title { get; set; }
    public int Paragraph { get; set; }
    public List<ParsedBlock> Blocks { get; set; } = new List<ParsedBlock>();
}

public class ParsedBlock
{
    public ContentBlockType Type { get; set; }
    public string Text { get; set; }
    public ParsedImage Image { get; set; }
    public int Paragraph { get; set; }
}

public class ParsedImage
{
    public byte[] Data { get; set; }
    public string ContentType { get; set; }
    public string Sha256 { get; set; }
    public string AltText { get; set; }
}

public class ParsedQuestion
{
    public string Code { get; set; }
    public int No { get; set; }
    public bool ShuffleOptions { get; set; }
    public string Stem { get; set; }
    public List<ParsedOption> Options { get; set; } = new List<ParsedOption>();
    public string AnswerRef { get; set; }
    public string Explanation { get; set; }
    public int Paragraph { get; set; }
}

public class ParsedOption
{
    public string Code { get; set; }
    public string Text { get; set; }
    public int Paragraph { get; set; }
}
