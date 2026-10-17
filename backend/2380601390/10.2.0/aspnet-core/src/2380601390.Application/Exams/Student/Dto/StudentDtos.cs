using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace _2380601390.Exams.Student.Dto;

// QUY ƯỚC BẢO MẬT: DTO dành cho lúc làm bài KHÔNG có đáp án đúng, giải thích, mã câu hỏi nội bộ hay số câu gốc.
// Các trường đó chỉ xuất hiện trong AttemptResultDto, và chỉ sau khi đã nộp.

public class PublishedExamDto
{
    public long ExamVersionId { get; set; }
    public string Title { get; set; }
    public int DurationMinutes { get; set; }
    public int TotalQuestions { get; set; }

    /// <summary>Có giá trị nếu sinh viên đang làm dở đề này (còn hạn).</summary>
    public long? InProgressAttemptId { get; set; }
    public DateTime? InProgressExpiresAtUtc { get; set; }

    public long? LastAttemptId { get; set; }
    public decimal? LastScorePercent { get; set; }
}

public class StartAttemptInput
{
    public long ExamVersionId { get; set; }
}

public class AttemptDto
{
    public long AttemptId { get; set; }
    public long ExamVersionId { get; set; }
    public string Title { get; set; }
    public AttemptStatus Status { get; set; }
    public int TotalQuestions { get; set; }

    // Thời gian theo mốc của MÁY CHỦ (UTC). Client đếm ngược dựa trên ServerNowUtc, không dựa đồng hồ thiết bị.
    public DateTime StartedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime ServerNowUtc { get; set; }
    public int RemainingSeconds { get; set; }

    public List<AttemptGroupDto> Groups { get; set; } = new List<AttemptGroupDto>();
    public List<AttemptQuestionDto> Questions { get; set; } = new List<AttemptQuestionDto>();
}

public class AttemptGroupDto
{
    public long GroupId { get; set; }
    public int DisplayOrder { get; set; }
    public int PartNo { get; set; }
    public List<AttemptDocumentDto> Documents { get; set; } = new List<AttemptDocumentDto>();
}

public class AttemptDocumentDto
{
    public List<AttemptBlockDto> Blocks { get; set; } = new List<AttemptBlockDto>();
}

public class AttemptBlockDto
{
    /// <summary>"Text" hoặc "Image".</summary>
    public string Type { get; set; }

    /// <summary>Part 6: chỗ trống dạng {{blank:N}} với N = số thứ tự hiển thị của câu.</summary>
    public string Text { get; set; }

    /// <summary>Ảnh: tải qua GET /api/media/{sha256}.</summary>
    public string MediaSha256 { get; set; }
    public string AltText { get; set; }
}

public class AttemptQuestionDto
{
    /// <summary>Số thứ tự trình bày 1..N (không phải số câu gốc).</summary>
    public int Number { get; set; }
    public long QuestionId { get; set; }
    public int PartNo { get; set; }

    /// <summary>Null với Part 5 (câu độc lập).</summary>
    public long? GroupId { get; set; }
    public string Stem { get; set; }
    public List<AttemptOptionDto> Options { get; set; } = new List<AttemptOptionDto>();

    public long? SelectedOptionId { get; set; }
    public int Revision { get; set; }
}

public class AttemptOptionDto
{
    public long OptionId { get; set; }

    /// <summary>Nhãn hiển thị A–D theo thứ tự đã trộn; chấm điểm luôn theo OptionId, không theo nhãn.</summary>
    public string Label { get; set; }
    public string Text { get; set; }
}

public class SaveAnswerInput
{
    public long AttemptId { get; set; }
    public long QuestionId { get; set; }

    /// <summary>Null = hủy lựa chọn.</summary>
    public long? OptionId { get; set; }

    /// <summary>Client tăng dần theo từng câu; yêu cầu có revision không lớn hơn bản đã lưu sẽ bị bỏ qua.</summary>
    [Range(1, int.MaxValue)]
    public int Revision { get; set; }
}

public class SaveAnswerResultDto
{
    public bool Saved { get; set; }
    public int Revision { get; set; }
    public long? SelectedOptionId { get; set; }
    public DateTime ServerNowUtc { get; set; }
    public int RemainingSeconds { get; set; }
}

public class AttemptIdInput
{
    public long AttemptId { get; set; }
}

public class AttemptSummaryDto
{
    public long AttemptId { get; set; }
    public long ExamVersionId { get; set; }
    public string Title { get; set; }
    public AttemptStatus Status { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectCount { get; set; }
    public int WrongCount { get; set; }
    public int SkippedCount { get; set; }

    /// <summary>Tỷ lệ đúng 0–100 (đúng / tổng số câu). Chưa quy đổi sang thang TOEIC.</summary>
    public decimal ScorePercent { get; set; }
}

public class PartScoreDto
{
    public int PartNo { get; set; }
    public int Total { get; set; }
    public int Correct { get; set; }
}

public class AttemptResultDto : AttemptSummaryDto
{
    public List<PartScoreDto> Parts { get; set; } = new List<PartScoreDto>();
    public List<ResultQuestionDto> Questions { get; set; } = new List<ResultQuestionDto>();
}

public class ResultQuestionDto
{
    public int Number { get; set; }
    public int PartNo { get; set; }
    public long? GroupId { get; set; }
    public string Stem { get; set; }

    /// <summary>"Correct", "Wrong" hoặc "Skipped".</summary>
    public string Outcome { get; set; }
    public string Explanation { get; set; }
    public List<ResultOptionDto> Options { get; set; } = new List<ResultOptionDto>();
}

public class ResultOptionDto
{
    public long OptionId { get; set; }
    public string Label { get; set; }
    public string Text { get; set; }
    public bool IsCorrect { get; set; }
    public bool IsSelected { get; set; }
}
