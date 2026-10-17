using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using _2380601390.Exams;

namespace _2380601390.Exams.Teacher.Dto;

public class ImportExamInput
{
    [Required, StringLength(260)]
    public string FileName { get; set; }

    [Required]
    public byte[] Content { get; set; }
}

public class ImportErrorDto
{
    public string Code { get; set; }
    public string Message { get; set; }
    public int? Paragraph { get; set; }
    public int? Table { get; set; }
    public int? PartNo { get; set; }
    public string GroupCode { get; set; }
    public int? QuestionNo { get; set; }
    public string Tag { get; set; }
}

public class ImportExamResultDto
{
    public bool Success { get; set; }

    /// <summary>Có giá trị khi Success = true.</summary>
    public long? ExamId { get; set; }
    public long? ExamVersionId { get; set; }
    public int? VersionNo { get; set; }
    public string Title { get; set; }
    public int QuestionCount { get; set; }
    public int ImageCount { get; set; }

    /// <summary>Khi có lỗi: không có dữ liệu nào được lưu.</summary>
    public List<ImportErrorDto> Errors { get; set; } = new List<ImportErrorDto>();
}

public class ExamVersionListItemDto
{
    public long ExamId { get; set; }
    public long VersionId { get; set; }
    public string Code { get; set; }
    public string Title { get; set; }
    public int VersionNo { get; set; }
    public ExamVersionStatus Status { get; set; }
    public int DurationMinutes { get; set; }
    public int TotalQuestions { get; set; }
    public string SourceFileName { get; set; }
    public DateTime CreationTime { get; set; }
}
