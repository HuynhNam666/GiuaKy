using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using _2380601390.Exams.Import;

namespace _2380601390.Exams.Teacher.Dto;

public static class ImportErrorDtoExtensions
{
    public static ImportErrorDto ToDto(this ImportError e) => new ImportErrorDto
    {
        Code = e.Code,
        Message = e.Message,
        Paragraph = e.Paragraph,
        Table = e.Table,
        PartNo = e.PartNo,
        GroupCode = e.GroupCode,
        QuestionNo = e.QuestionNo,
        Tag = e.Tag
    };
}

// ---------- xem trước (DTO giảng viên: có đáp án đúng và giải thích) ----------

public class ExamPreviewDto
{
    public long VersionId { get; set; }
    public long ExamId { get; set; }
    public string Code { get; set; }
    public string Title { get; set; }
    public int VersionNo { get; set; }
    public ExamVersionStatus Status { get; set; }
    public int DurationMinutes { get; set; }
    public int TotalQuestions { get; set; }
    public string SourceFileName { get; set; }
    public bool CanEdit { get; set; }
    public List<PartPreviewDto> Parts { get; set; } = new List<PartPreviewDto>();

    /// <summary>Kết quả kiểm tra lại toàn bộ phiên bản (rỗng = hợp lệ).</summary>
    public List<ImportErrorDto> Issues { get; set; } = new List<ImportErrorDto>();
}

public class PartPreviewDto
{
    public long Id { get; set; }
    public int PartNo { get; set; }
    public int FromNo { get; set; }
    public int ToNo { get; set; }
    public bool ShuffleItems { get; set; }
    public List<QuestionPreviewDto> Questions { get; set; } = new List<QuestionPreviewDto>();
    public List<GroupPreviewDto> Groups { get; set; } = new List<GroupPreviewDto>();
}

public class GroupPreviewDto
{
    public long Id { get; set; }
    public string Code { get; set; }
    public bool ShuffleQuestions { get; set; }
    public List<DocumentPreviewDto> Documents { get; set; } = new List<DocumentPreviewDto>();
    public List<QuestionPreviewDto> Questions { get; set; } = new List<QuestionPreviewDto>();
}

public class DocumentPreviewDto
{
    public long Id { get; set; }
    public string Code { get; set; }
    public string Title { get; set; }
    public List<BlockPreviewDto> Blocks { get; set; } = new List<BlockPreviewDto>();
}

public class BlockPreviewDto
{
    public long Id { get; set; }
    public ContentBlockType Type { get; set; }
    public string Text { get; set; }
    public long? MediaAssetId { get; set; }
    public string AltText { get; set; }
}

public class QuestionPreviewDto
{
    public long Id { get; set; }
    public string Code { get; set; }
    public int SourceNo { get; set; }
    public string Stem { get; set; }
    public string Explanation { get; set; }
    public bool ShuffleOptions { get; set; }
    public string CorrectOptionCode { get; set; }
    public List<OptionPreviewDto> Options { get; set; } = new List<OptionPreviewDto>();
}

public class OptionPreviewDto
{
    public long Id { get; set; }
    public string Code { get; set; }
    public string Text { get; set; }
}

// ---------- sửa ----------

public class OptionTextInput
{
    [Required, StringLength(8)]
    public string Code { get; set; }

    public string Text { get; set; }
}

public class UpdateQuestionInput
{
    public long QuestionId { get; set; }
    public string Stem { get; set; }
    public string Explanation { get; set; }
    public bool ShuffleOptions { get; set; }

    [Required]
    public string CorrectOptionCode { get; set; }

    [Required]
    public List<OptionTextInput> Options { get; set; }
}

public class UpdateGroupInput
{
    public long GroupId { get; set; }
    public bool ShuffleQuestions { get; set; }
}

public class UpdatePartInput
{
    public long PartId { get; set; }
    public bool ShuffleItems { get; set; }
}

public class UpdateBlockTextInput
{
    public long BlockId { get; set; }
    public string Text { get; set; }
}

public class UpdateSettingsInput
{
    public long VersionId { get; set; }

    [Required, StringLength(256)]
    public string Title { get; set; }

    public int DurationMinutes { get; set; }
}

/// <summary>Kết quả sau mỗi lần sửa: dữ liệu đã lưu + kiểm tra lại toàn bộ phiên bản.</summary>
public class EditResultDto
{
    public bool IsValid => Issues.Count == 0;
    public List<ImportErrorDto> Issues { get; set; } = new List<ImportErrorDto>();
}

public class PublishResultDto
{
    public bool Success { get; set; }
    public List<ImportErrorDto> Errors { get; set; } = new List<ImportErrorDto>();
}

public class VersionIdInput
{
    public long VersionId { get; set; }
}
