using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace _2380601390.Exams;

/// <summary>Đề thi (vỏ chứa các phiên bản).</summary>
public class Exam : FullAuditedEntity<long>
{
    [Required, StringLength(64)]
    public string Code { get; set; }

    [Required, StringLength(256)]
    public string Title { get; set; }

    public virtual ICollection<ExamVersion> Versions { get; set; } = new List<ExamVersion>();
}

/// <summary>Phiên bản đề. Bất biến khi Status = Published.</summary>
public class ExamVersion : FullAuditedEntity<long>
{
    public long ExamId { get; set; }
    public virtual Exam Exam { get; set; }

    public int VersionNo { get; set; }
    public ExamVersionStatus Status { get; set; }
    public int DurationMinutes { get; set; }
    public int TotalQuestions { get; set; }
    public int TagsVersion { get; set; } = 1;
    public DateTime? PublishedAtUtc { get; set; }

    [StringLength(260)]
    public string SourceFileName { get; set; }

    [StringLength(64)]
    public string SourceFileSha256 { get; set; }

    public virtual ICollection<ExamPart> Parts { get; set; } = new List<ExamPart>();
}

public class ExamPart : Entity<long>
{
    public long ExamVersionId { get; set; }
    public virtual ExamVersion ExamVersion { get; set; }

    /// <summary>5, 6 hoặc 7.</summary>
    public int PartNo { get; set; }
    public int FromNo { get; set; }
    public int ToNo { get; set; }

    /// <summary>Part 5: hoán vị câu độc lập; Part 6/7: hoán vị nguyên nhóm.</summary>
    public bool ShuffleItems { get; set; }
    public int DisplayOrder { get; set; }

    public virtual ICollection<ReadingGroup> Groups { get; set; } = new List<ReadingGroup>();
    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
}

public class ReadingGroup : Entity<long>
{
    public long ExamPartId { get; set; }
    public virtual ExamPart ExamPart { get; set; }

    [Required, StringLength(64)]
    public string Code { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>Hoán vị câu bên trong nhóm (Part 6 luôn false).</summary>
    public bool ShuffleQuestions { get; set; }

    public virtual ICollection<ReadingDocument> Documents { get; set; } = new List<ReadingDocument>();
    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
}

public class ReadingDocument : Entity<long>
{
    public long ReadingGroupId { get; set; }
    public virtual ReadingGroup ReadingGroup { get; set; }

    [Required, StringLength(64)]
    public string Code { get; set; }

    [StringLength(256)]
    public string Title { get; set; }

    public int DisplayOrder { get; set; }

    public virtual ICollection<ContentBlock> Blocks { get; set; } = new List<ContentBlock>();
}

/// <summary>Một khối văn bản hoặc ảnh trong tài liệu, theo thứ tự DisplayOrder.</summary>
public class ContentBlock : Entity<long>
{
    public long ReadingDocumentId { get; set; }
    public virtual ReadingDocument ReadingDocument { get; set; }

    public int DisplayOrder { get; set; }
    public ContentBlockType BlockType { get; set; }

    /// <summary>Văn bản; Part 6 chứa đánh dấu {{blank:mã-câu}}.</summary>
    public string Text { get; set; }

    public long? MediaAssetId { get; set; }
    public virtual MediaAsset MediaAsset { get; set; }

    [StringLength(512)]
    public string AltText { get; set; }
}

public class MediaAsset : Entity<long>
{
    [Required, StringLength(64)]
    public string Sha256 { get; set; }

    [Required, StringLength(100)]
    public string ContentType { get; set; }

    public long SizeBytes { get; set; }

    public byte[] Data { get; set; }
}

public class Question : Entity<long>
{
    public long ExamVersionId { get; set; }
    public virtual ExamVersion ExamVersion { get; set; }

    public long ExamPartId { get; set; }
    public virtual ExamPart ExamPart { get; set; }

    /// <summary>NULL với Part 5 (câu độc lập).</summary>
    public long? ReadingGroupId { get; set; }
    public virtual ReadingGroup ReadingGroup { get; set; }

    /// <summary>Id ổn định khai báo trong file Word.</summary>
    [Required, StringLength(64)]
    public string Code { get; set; }

    /// <summary>Số câu gốc (ví dụ 131).</summary>
    public int SourceNo { get; set; }

    /// <summary>Thứ tự nguồn trong nhóm / trong part.</summary>
    public int SourceOrder { get; set; }

    /// <summary>Nội dung câu hỏi; có thể rỗng (Part 6).</summary>
    public string StemText { get; set; }

    public string Explanation { get; set; }
    public bool ShuffleOptions { get; set; }

    public long? CorrectOptionId { get; set; }
    public virtual QuestionOption CorrectOption { get; set; }

    public virtual ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
}

public class QuestionOption : Entity<long>
{
    public long QuestionId { get; set; }
    public virtual Question Question { get; set; }

    /// <summary>Mã ổn định A–D (không phải nhãn hiển thị).</summary>
    [Required, StringLength(8)]
    public string Code { get; set; }

    [Required]
    public string Text { get; set; }

    public int SourceOrder { get; set; }
}

public class ExamAttempt : Entity<long>
{
    public long UserId { get; set; }
    public long ExamVersionId { get; set; }
    public virtual ExamVersion ExamVersion { get; set; }

    public AttemptStatus Status { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }

    public int TotalQuestions { get; set; }
    public int? CorrectCount { get; set; }
    public int? WrongCount { get; set; }
    public int? SkippedCount { get; set; }

    /// <summary>Tỷ lệ đúng (0–100).</summary>
    public decimal? ScorePercent { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; }

    public virtual ICollection<AttemptGroup> Groups { get; set; } = new List<AttemptGroup>();
    public virtual ICollection<AttemptQuestion> Questions { get; set; } = new List<AttemptQuestion>();
}

public class AttemptGroup : Entity<long>
{
    public long ExamAttemptId { get; set; }
    public virtual ExamAttempt ExamAttempt { get; set; }

    public long ReadingGroupId { get; set; }
    public virtual ReadingGroup ReadingGroup { get; set; }

    public int DisplayOrder { get; set; }
}

/// <summary>Câu hỏi trong lượt thi: thứ tự đã trộn + đáp án đang chọn.</summary>
public class AttemptQuestion : Entity<long>
{
    public long ExamAttemptId { get; set; }
    public virtual ExamAttempt ExamAttempt { get; set; }

    public long QuestionId { get; set; }
    public virtual Question Question { get; set; }

    /// <summary>Số thứ tự trình bày 1..N.</summary>
    public int DisplayOrder { get; set; }

    public long? SelectedOptionId { get; set; }
    public virtual QuestionOption SelectedOption { get; set; }

    /// <summary>Revision client gửi lên; chỉ ghi khi lớn hơn giá trị hiện có.</summary>
    public int AnswerRevision { get; set; }

    public DateTime? AnsweredAtUtc { get; set; }

    /// <summary>Ghi khi chấm.</summary>
    public bool? IsCorrect { get; set; }

    public virtual ICollection<AttemptOption> Options { get; set; } = new List<AttemptOption>();
}

public class AttemptOption : Entity<long>
{
    public long AttemptQuestionId { get; set; }
    public virtual AttemptQuestion AttemptQuestion { get; set; }

    public long QuestionOptionId { get; set; }
    public virtual QuestionOption QuestionOption { get; set; }

    /// <summary>0..3 → nhãn A..D khi hiển thị.</summary>
    public int DisplayOrder { get; set; }
}
