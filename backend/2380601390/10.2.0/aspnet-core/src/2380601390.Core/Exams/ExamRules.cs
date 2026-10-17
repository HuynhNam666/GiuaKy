using _2380601390.Exams.Import;

namespace _2380601390.Exams;

/// <summary>Quy tắc chung của đề TOEIC Reading dùng khi import, sửa và xuất bản.</summary>
public static class ExamRules
{
    /// <summary>Số câu của một đề (giả định 100, cần giảng viên xác nhận).</summary>
    public const int ExpectedTotalQuestions = 100;

    public const int MaxDurationMinutes = 300;

    public static ExamImportOptions ValidationOptions() => new ExamImportOptions
    {
        RequireAllParts = true,
        ExpectedTotalQuestions = ExpectedTotalQuestions
    };
}
