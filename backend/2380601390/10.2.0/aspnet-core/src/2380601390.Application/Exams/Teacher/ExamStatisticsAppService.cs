using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.Domain.Entities;
using Abp.Domain.Repositories;
using _2380601390.Authorization;
using Microsoft.EntityFrameworkCore;

namespace _2380601390.Exams.Teacher;

public class WrongQuestionStatDto
{
    public int Rank { get; set; }

    /// <summary>Số câu gốc trong đề.</summary>
    public int QuestionNo { get; set; }
    public int PartNo { get; set; }
    public string Stem { get; set; }

    /// <summary>Số lượt chọn đáp án sai (bỏ qua KHÔNG tính là sai).</summary>
    public int WrongCount { get; set; }
    public int SkippedCount { get; set; }

    /// <summary>Số lượt đã chấm có câu này (= số lượt đã nộp của phiên bản).</summary>
    public int AttemptCount { get; set; }

    /// <summary>Tỷ lệ sai 0–100 = sai / số lượt đã nộp.</summary>
    public decimal WrongPercent { get; set; }
}

public class ExamStatisticsDto
{
    public long ExamVersionId { get; set; }
    public string Title { get; set; }
    public int VersionNo { get; set; }
    public ExamVersionStatus Status { get; set; }

    /// <summary>Lượt đã nộp (tay hoặc tự thu bài): những lượt này mới được tính vào điểm và Top 10.</summary>
    public int FinishedCount { get; set; }
    public int DistinctStudents { get; set; }

    /// <summary>Lượt đang làm (còn hạn): hiển thị riêng, không tính vào điểm trung bình.</summary>
    public int InProgressCount { get; set; }
    public int AutoSubmittedCount { get; set; }

    /// <summary>Điểm trung bình (% đúng) của các lượt đã nộp; null nếu chưa có lượt nào.</summary>
    public decimal? AveragePercent { get; set; }

    public List<WrongQuestionStatDto> TopWrong { get; set; } = new List<WrongQuestionStatDto>();
}

public interface IExamStatisticsAppService : Abp.Application.Services.IApplicationService
{
    Task<ExamStatisticsDto> GetAsync(long examVersionId);
}

/// <summary>
/// Thống kê theo từng PHIÊN BẢN đề (câu hỏi của các phiên bản khác nhau nên không gộp theo câu).
/// Nhiều lần làm: mọi lượt đã nộp đều được tính; một sinh viên có thể góp nhiều lượt.
/// Top 10 xếp theo số lượt sai giảm dần (cùng phiên bản nên cùng mẫu số, thứ tự cũng chính là theo tỷ lệ sai).
/// </summary>
[AbpAuthorize(PermissionNames.Pages_Exams_Statistics)]
public class ExamStatisticsAppService : _2380601390AppServiceBase, IExamStatisticsAppService
{
    private readonly IRepository<ExamVersion, long> _versions;
    private readonly IRepository<ExamAttempt, long> _attempts;
    private readonly IRepository<AttemptQuestion, long> _attemptQuestions;
    private readonly IRepository<Question, long> _questions;

    public ExamStatisticsAppService(
        IRepository<ExamVersion, long> versions,
        IRepository<ExamAttempt, long> attempts,
        IRepository<AttemptQuestion, long> attemptQuestions,
        IRepository<Question, long> questions)
    {
        _versions = versions;
        _attempts = attempts;
        _attemptQuestions = attemptQuestions;
        _questions = questions;
    }

    public async Task<ExamStatisticsDto> GetAsync(long examVersionId)
    {
        var v = await _versions.GetAll().AsNoTracking().Include(x => x.Exam).FirstOrDefaultAsync(x => x.Id == examVersionId);
        if (v == null) throw new EntityNotFoundException(typeof(ExamVersion), examVersionId);

        var now = DateTime.UtcNow;
        var attempts = _attempts.GetAll().AsNoTracking().Where(a => a.ExamVersionId == examVersionId);
        var finished = attempts.Where(a => a.Status == AttemptStatus.Submitted || a.Status == AttemptStatus.AutoSubmitted);

        var dto = new ExamStatisticsDto
        {
            ExamVersionId = v.Id,
            Title = v.Exam.Title,
            VersionNo = v.VersionNo,
            Status = v.Status,
            FinishedCount = await finished.CountAsync(),
            DistinctStudents = await finished.Select(a => a.UserId).Distinct().CountAsync(),
            InProgressCount = await attempts.CountAsync(a => a.Status == AttemptStatus.InProgress && a.ExpiresAtUtc > now),
            AutoSubmittedCount = await attempts.CountAsync(a => a.Status == AttemptStatus.AutoSubmitted)
        };

        if (dto.FinishedCount == 0) return dto;

        dto.AveragePercent = Math.Round(await finished.AverageAsync(a => a.ScorePercent ?? 0m), 1);

        var top = await _attemptQuestions.GetAll().AsNoTracking()
            .Where(x => x.ExamAttempt.ExamVersionId == examVersionId
                        && (x.ExamAttempt.Status == AttemptStatus.Submitted || x.ExamAttempt.Status == AttemptStatus.AutoSubmitted))
            .GroupBy(x => x.QuestionId)
            .Select(g => new
            {
                QuestionId = g.Key,
                Wrong = g.Count(x => x.IsCorrect == false),
                Skipped = g.Count(x => x.SelectedOptionId == null)
            })
            .Where(x => x.Wrong > 0)
            .OrderByDescending(x => x.Wrong)
            .ThenByDescending(x => x.Skipped)
            .ThenBy(x => x.QuestionId)
            .Take(10)
            .ToListAsync();

        var ids = top.Select(t => t.QuestionId).ToList();
        var info = (await _questions.GetAll().AsNoTracking()
            .Where(q => ids.Contains(q.Id))
            .Select(q => new { q.Id, q.SourceNo, PartNo = q.ExamPart.PartNo, q.StemText, GroupCode = q.ReadingGroup != null ? q.ReadingGroup.Code : null })
            .ToListAsync()).ToDictionary(q => q.Id);

        var rank = 0;
        foreach (var t in top)
        {
            var q = info[t.QuestionId];
            dto.TopWrong.Add(new WrongQuestionStatDto
            {
                Rank = ++rank,
                QuestionNo = q.SourceNo,
                PartNo = q.PartNo,
                Stem = string.IsNullOrWhiteSpace(q.StemText) ? "(chỗ trống nhóm " + q.GroupCode + ")" : q.StemText,
                WrongCount = t.Wrong,
                SkippedCount = t.Skipped,
                AttemptCount = dto.FinishedCount,
                WrongPercent = Math.Round(100m * t.Wrong / dto.FinishedCount, 1)
            });
        }

        return dto;
    }
}
